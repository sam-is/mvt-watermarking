using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using MvtWatermark.DebugClasses;
using MvtWatermark.NoDistortionWatermark;
using MvtWatermark.NoDistortionWatermark.Auxiliary;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using NetTopologySuite.IO.VectorTiles.Tiles.Changed;

namespace NetTopologySuite.IO.VectorTiles.Mapbox.Watermarking;

public class MapboxTileReaderWM
{
    private readonly GeometryFactory _factory;

    public MapboxTileReaderWM()
        : this(new GeometryFactory(new PrecisionModel(), 4326))
    {
    }

    public MapboxTileReaderWM(GeometryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates VectorTileTree from Dictionary(key = ulong tileId, value = Tile)
    /// </summary>
    /// <param name="TileDict"></param>
    /// <returns></returns>
    public VectorTileTree Read(Dictionary<ulong, Tile> TileDict)
    {
        var resultTree = new VectorTileTree();
        foreach (var tilePair in TileDict)
        {
            resultTree[tilePair.Key] = Read(tilePair.Value, tilePair.Key, null);
        }

        return resultTree;
    }

    /// <summary>
    /// Reads a Vector Tile stream.
    /// </summary>
    /// <param name="stream">Vector tile stream.</param>
    /// <param name="tileDefinition">Tile information.</param>
    /// <param name="idAttributeName">Optional. Specifies the name of the attribute that the vector tile feature's ID should be stored in the NetTopologySuite Features AttributeTable.</param>
    /// <returns></returns>
    public VectorTile Read(Tile tile, ulong tileId, string idAttributeName)
    {
        var tileDefinition = new Tiles.Tile(tileId); // TileId Хранит в себе всю нужную информацию о тайле
        var vectorTile = new VectorTile { TileId = tileDefinition.Id };
        foreach (var mbTileLayer in tile.Layers)
        {
            Debug.Assert(mbTileLayer.Version == 2U);

            var tgs = new TileGeometryTransform(tileDefinition, mbTileLayer.Extent);
            var layer = new Layer {Name = mbTileLayer.Name};
            foreach (var mbTileFeature in mbTileLayer.Features)
            {
                var feature = ReadFeature(tgs, mbTileLayer, mbTileFeature, idAttributeName);
                layer.Features.Add(feature);
            }
            vectorTile.Layers.Add(layer);
        }

        return vectorTile;
    }

    public int? ExtractWM(Tile tile, ulong tileId, NoDistortionWatermarkOptions options, short firstHalfOfTheKey)
    {
        int key = firstHalfOfTheKey;
        key = (key << 16) + (short)tileId;

        var keySequence = SequenceGenerator.GenerateSequence(key, options.Nb, options.D, options.M);

        var extractedWatermarkIntegers = new List<int>(); // в скобочках будет Lf видимо
        var tileDefinition = new Tiles.Tile(tileId); 
        foreach (var mbTileLayer in tile.Layers)
        {
            Debug.Assert(mbTileLayer.Version == 2U);

            var tgs = new TileGeometryTransform(tileDefinition, mbTileLayer.Extent);

            //int featureIndex = 0;

            foreach (var mbTileFeature in mbTileLayer.Features)
            {
                //Console.WriteLine($"\nТекущая Фича: {featureIndex++}"); // отладка
                var watermarkIntFromFeature = ExtractFromFeature(tgs, mbTileFeature, options, keySequence);
                if (watermarkIntFromFeature != null)
                {
                    extractedWatermarkIntegers.Add(Convert.ToInt32(watermarkIntFromFeature));
                }
            }
        }

        // Работа с полученным списком
        if (extractedWatermarkIntegers.Count == 0)
            return null; // либо можно вернуть -1

        var groupsWithCounts = from s in extractedWatermarkIntegers
                               group s by s into g
                               select new
                               {
                                   Item = g.Key,
                                   Count = g.Count()
                               };
        var groupsSorted = groupsWithCounts.OrderByDescending(g => g.Count);
        var mostFrequestWatermarkInt = groupsSorted.First().Item;

        return mostFrequestWatermarkInt;
    }

    private IFeature ReadFeature(TileGeometryTransform tgs, Tile.Layer mbTileLayer, Tile.Feature mbTileFeature, string idAttributeName)
    {
        var geometry = ReadGeometry(tgs, mbTileFeature.Type, mbTileFeature.Geometry);
        var attributes = ReadAttributeTable(mbTileFeature, mbTileLayer.Keys, mbTileLayer.Values);

        //Check to see if an id value is already captured in the attributes, if not, add it.
        if (!string.IsNullOrEmpty(idAttributeName) && !mbTileLayer.Keys.Contains(idAttributeName))
        {
            var id = mbTileFeature.Id;
            attributes.Add(idAttributeName, id);
        }

        return new Feature(geometry, attributes);
    }

    private Geometry ReadGeometry(TileGeometryTransform tgs, Tile.GeomType type, IList<uint> geometry)
    {
        switch (type)
        {
            case Tile.GeomType.Point:
                return ReadPoint(tgs, geometry);

            case Tile.GeomType.LineString:
                return ReadLineString(tgs, geometry);

            case Tile.GeomType.Polygon:
                return ReadPolygon(tgs, geometry);
        }

        return null;
    }

    /// <summary>
    /// // Извлечение из фичи, если в ней лежит лайнстринг
    /// </summary>
    /// <param name="tgs"></param>
    /// <param name="mbTileFeature"></param>
    /// <param name="options"></param>
    /// <param name="keySequence"></param>
    /// <returns></returns>
    private int? ExtractFromFeature(TileGeometryTransform tgs, Tile.Feature mbTileFeature, 
        NoDistortionWatermarkOptions options, int[] keySequence)
    {
        if (mbTileFeature.Type == Tile.GeomType.LineString)
        {
            var watermarkInt = ReadLineStringWM(tgs, mbTileFeature.Geometry, options, keySequence);
            return watermarkInt;
        }
        return null;
    }

    /// <summary>
    /// Извлекает ЦВЗ из одной фичи с лайнстрингом
    /// </summary>
    /// <param name="tgs"></param>
    /// <param name="geometry"></param>
    /// <param name="options"></param>
    /// <param name="keySequence"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private int? ReadLineStringWM(TileGeometryTransform tgs, IList<uint> geometry, NoDistortionWatermarkOptions options, int[] keySequence)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        //int watermarkInt = -1; 
        int? watermarkInt = null;
        switch (options.AtypicalEncodingType){
            case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt:
                watermarkInt = ExtractWMFromSingleLinestringMtLtLt(tgs, geometry, ref currentIndex, ref currentX, ref currentY, options, keySequence);
                break;
            case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtMt:
                throw new NotImplementedException();
            case NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands:
                watermarkInt = ExtractWMFromSingleLinestringNLt(tgs, geometry, ref currentIndex, ref currentX, ref currentY, options, keySequence);
                break;
        }
         
        return watermarkInt;
    }

    private Geometry ReadPoint(TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, forPoint:true);
        return CreatePuntal(sequences);
    }

    private Geometry ReadLineString(TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY);
        return CreateLineal(sequences);
    }

    private Geometry ReadPolygon(TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, 1);
        return CreatePolygonal(sequences);
    }

    private Geometry CreatePuntal(CoordinateSequence[] sequences)
    {
        if (sequences == null || sequences.Length == 0)
            return null;

        var points = new Point[sequences.Length];
        for (var i = 0; i < sequences.Length; i++)
            points[i] = _factory.CreatePoint(sequences[i]);

        if (points.Length == 1)
            return points[0];

        return _factory.CreateMultiPoint(points);
    }

    private Geometry CreateLineal(CoordinateSequence[] sequences)
    {
        if (sequences == null || sequences.Length == 0)
            return null;

        var lineStrings = new LineString[sequences.Length];
        for (var i = 0; i < sequences.Length; i++)
            lineStrings[i] = _factory.CreateLineString(sequences[i]);

        if (lineStrings.Length == 1)
            return lineStrings[0];

        return _factory.CreateMultiLineString(lineStrings);
    }

    private Geometry CreatePolygonal(CoordinateSequence[] sequences)
    {
        var polygons = new List<Polygon>();

        LinearRing shell = null;
        var holes = new List<LinearRing>();

        for (var i = 0; i < sequences.Length; i++)
        {
            var ring = _factory.CreateLinearRing(sequences[i]);

            // Shell rings should be CW (https://docs.mapbox.com/vector-tiles/specification/#winding-order)
            if (!ring.IsCCW)
            {
                if (shell != null)
                {
                    polygons.Add(_factory.CreatePolygon(shell, holes.ToArray()));
                    holes.Clear();
                }
                shell = ring;
            }
            // Hole rings should be CCW https://docs.mapbox.com/vector-tiles/specification/#winding-order
            else
            {
                if (shell == null)
                {
                    if (sequences.Length == 1)
                    {
                        // WARNING: this is not according to the spec but tiles exists like this in the wild
                        // that are rendered just fine by other tools, we can ignore them if we want to but
                        // should not throw an exception. The solution preferred here is to just read them
                        // but reverse them so the user gets what they expect according to the spec.
                        shell = ring.Reverse() as LinearRing;
                    }
                    else
                    {
                        throw new InvalidOperationException("No shell defined.");
                    }
                }
                else
                {
                    holes.Add(ring);
                }
            }
        }

        polygons.Add(_factory.CreatePolygon(shell, holes.ToArray()));

        if (polygons.Count == 1)
            return polygons[0];

        return _factory.CreateMultiPolygon(polygons.ToArray());
    }

    /// <summary>
    /// Extracting watermark from Linestring encoded as MoveTo-LineTo-LineTo
    /// </summary>
    /// <param name="tgs"></param>
    /// <param name="geometry"></param>
    /// <param name="currentIndex"></param>
    /// <param name="currentX"></param>
    /// <param name="currentY"></param>
    /// <param name="options"></param>
    /// <param name="keySequence"></param>
    /// <returns></returns>
    private int? ExtractWMFromSingleLinestringMtLtLt(
        TileGeometryTransform tgs, IList<uint> geometry,
        ref int currentIndex, ref int currentX, ref int currentY, NoDistortionWatermarkOptions options, int[] keySequence)
    {
        // сюда запишем индексы сегментов с нетипичной геометрией
        var realSegments = new List<bool>();
        var realSegmentsReversedLineString = new List<bool>();
        var currentPosition = (currentX, currentY);

        var (command, count) = ParseCommandInteger(geometry[currentIndex++]); // после команды currentIndex = 1
        Debug.Assert(command == MapboxCommandType.MoveTo);
        Debug.Assert(count == 1, "Первая команда MoveTo имеет счётчик команд не 1");

        var shouldTryReversed = false;

        // Read the current position
        currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex); // после команды currentIndex = 3
        var savedPosition = currentPosition;

        // для проверки, что новый MoveTo не смещает курсор (почти) относительно предыдущего положения
        var lastPosition1 = currentPosition;
        var lastPosition2 = currentPosition;

        while (currentIndex < geometry.Count)
        {
            (command, count) = ParseCommandInteger(geometry[currentIndex++]);
            if (command == MapboxCommandType.MoveTo)
            {
                Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

                lastPosition2 = currentPosition;

                (command, count) = ParseCommandInteger(geometry[currentIndex++]);

                if (command != MapboxCommandType.LineTo)
                {
                    return null;
                }
                else if (count < 2) // это условие, возможно, не нужно
                {
                    shouldTryReversed = true;
                    break;
                }

                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

                if (lastPosition1 != currentPosition)
                {
                    shouldTryReversed = true;
                    break;
                }

                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

                if (lastPosition2 != currentPosition)
                    return null;

                lastPosition1 = currentPosition;

                realSegments.Add(true);
                count -= 2;

                //Console.WriteLine($"currentPosition: {currentPosition}"); // отладка
            }
            else
            {
                Debug.Assert(command == MapboxCommandType.LineTo, "команда не MoveTo и не LineTo");
            }

            // Read and add offsets
            for (var i = 0; i < count; i++)
            {
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                realSegments.Add(false);

                lastPosition1 = currentPosition;
            }
        }


        if (shouldTryReversed)
        {
            currentIndex = 3;
            currentPosition = savedPosition;

            lastPosition1 = currentPosition;
            lastPosition2 = currentPosition;

            var rNTGstart = false;

            while (currentIndex < geometry.Count)
            {
                (command, count) = ParseCommandInteger(geometry[currentIndex++]);
                if (command == MapboxCommandType.MoveTo)
                {
                    Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

                    if (lastPosition2 == currentPosition && rNTGstart)
                    {
                        realSegmentsReversedLineString.Add(true);
                    }

                    (command, count) = ParseCommandInteger(geometry[currentIndex++]);

                    if (command != MapboxCommandType.LineTo)
                    {
                        return null;
                    }
                }
                else
                {
                    Debug.Assert(command == MapboxCommandType.LineTo, "команда не MoveTo и не LineTo");
                }

                if (count < 1)
                    return null;

                for (var i = 0; i < count - 1; i++)
                {
                    lastPosition1 = currentPosition;

                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    realSegmentsReversedLineString.Add(false);
                }

                lastPosition2 = currentPosition;

                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                realSegmentsReversedLineString.Add(false);

                if (currentPosition != lastPosition1)
                    return null;
                else 
                    rNTGstart = true;
            }

            realSegmentsReversedLineString.Reverse();
            realSegments = realSegmentsReversedLineString;
        }


        var realSegmentsNum = realSegments.Count;
        var realSegmentsInONEElemSegment = realSegmentsNum / options.D;

        // предусмотреть возможность отражения лайнстринга
        var extractedWatermarkInts = new List<int>();

        if (realSegments.Count < options.D) // если вотермарки не обнаружено (количество реальных сегментов меньше количества элементарных)
            return null;

        //Console.WriteLine($"realSegmentsInONEElemSegment: {realSegmentsInONEElemSegment}"); // отладка
        //Console.WriteLine($"keySequence: {ConsoleWriter.GetArrayStr<int>(keySequence)}"); // отладка

        for (var i = 0; i < options.D/2; i++)
        {
            for (var j = 0; j < realSegmentsInONEElemSegment; j++)
            {
                if (realSegments[realSegmentsInONEElemSegment * i + j])
                {
                    //Console.WriteLine($"current keySequence[{i}]: {keySequence[i]}"); // отладка

                    extractedWatermarkInts.Add(keySequence[i]);
                    break;
                }
            }
        }

        //Console.WriteLine($"\tExtractedWatermarkInts: {ConsoleWriter.GetIEnumerableStr<int>(extractedWatermarkInts)}"); // отладка

        if (extractedWatermarkInts.Count == 0) return null;

        var groupsWithCounts = from s in extractedWatermarkInts
                               group s by s into g
                               select new
                               {
                                   Item = g.Key,
                                   Count = g.Count()
                               };
        var groupsSorted = groupsWithCounts.OrderByDescending(g => g.Count);
        var mostFrequestWatermarkInt = groupsSorted.First().Item;

        // update current position values
        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;

        return mostFrequestWatermarkInt;
    }

    private int? ExtractWMFromSingleLinestringNLt(
        TileGeometryTransform tgs, IList<uint> geometry,
        ref int currentIndex, ref int currentX, ref int currentY, NoDistortionWatermarkOptions options, int[] keySequence)
    {
        // сюда запишем индексы сегментов с нетипичной геометрией
        var realSegments = new List<bool>();

        // (currentX, currentY) = (0, 0), currentIndex = 0
        var currentPosition = (currentX, currentY);

        // для проверки, что новый MoveTo не смещает курсор (почти) относительно предыдущего положения
        var lastPosition = currentPosition;

        var isFirstSegment = true;

        while (currentIndex < geometry.Count)
        {
            //Console.WriteLine("Парсим MoveTo-LineTo"); // отладка

            var (command, count) = ParseCommandInteger(geometry[currentIndex++]);
            Debug.Assert(command == MapboxCommandType.MoveTo);
            Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

            //Console.WriteLine($"lastPosition = {lastPosition}, currentPosition = {currentPosition}"); // отладка
            if (!isFirstSegment && lastPosition != currentPosition)
            {
                return null; 
                // Если MoveTo не в ту же самую точку, то ЦВЗ больше не ищем и возвращаем
            }

            (command, count) = ParseCommandInteger(geometry[currentIndex++]);
            Debug.Assert(command == MapboxCommandType.LineTo, $"Assertion: MapboxCommandType = {command}");
            Debug.Assert(count >= 1, $"Assertion: LineTo count = {count}");
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

            if (!isFirstSegment) {
                realSegments.Add(true);
            }
            else
            {
                realSegments.Add(false);
            }
            isFirstSegment = false;


            for (var i = 1; i < count; i++)
            {
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                realSegments.Add(false);

                lastPosition = currentPosition;
            }
        }

        var realSegmentsNum = realSegments.Count;
        var realSegmentsInONEElemSegment = realSegmentsNum / options.D;

        // предусмотреть возможность отражения лайнстринга
        var extractedWatermarkInts = new List<int>();

        var extractedWatermarkIntsSecondHalf = new List<int>();

        if (realSegments.Count < options.D) // если вотермарки не обнаружено (количество реальных сегментов меньше количества элементарных)
            return null;

        //Console.WriteLine($"\trealSegmentsInONEElemSegment: {realSegmentsInONEElemSegment}"); // отладка
        //Console.WriteLine($"keySequence: {ConsoleWriter.GetArrayStr<int>(keySequence)}"); // отладка

        // работает также с отражённым лайнстрингом
        for (var i = 0; i < options.D / 2; i++)
        {
            for (var j = 0; j < realSegmentsInONEElemSegment; j++)
            {
                if (realSegments[realSegmentsInONEElemSegment * i + j]) 
                {
                    //Console.WriteLine($"current keySequence[{i}]: {keySequence[i]}"); // отладка

                    extractedWatermarkInts.Add(keySequence[i]);
                    break;
                }

                if (realSegments[realSegments.Count - 1 - (realSegmentsInONEElemSegment * i + j)])
                {
                    //Console.WriteLine($"current (Second Half) keySequence[{i}]: {keySequence[i]}"); // отладка

                    extractedWatermarkIntsSecondHalf.Add(keySequence[i]);
                    break;
                }
            }
        }

        //Console.WriteLine($"\t\tExtractedWatermarkInts: {ConsoleWriter.GetIEnumerableStr<int>(extractedWatermarkInts)}"); // отладка


        //bool wasReversed = false;

        if (extractedWatermarkInts.Count == 0)
        {
            if (extractedWatermarkIntsSecondHalf.Count == 0)
                return null;
            else
                extractedWatermarkInts = extractedWatermarkIntsSecondHalf;
                //wasReversed = true;
        }


        var groupsWithCounts = from s in extractedWatermarkInts
                               group s by s into g
                               select new
                               {
                                   Item = g.Key,
                                   Count = g.Count()
                               };
        var groupsSorted = groupsWithCounts.OrderByDescending(g => g.Count);
        var mostFrequestWatermarkInt = groupsSorted.First().Item;

        // update current position values
        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;

        return mostFrequestWatermarkInt;
    }

    private CoordinateSequence[] ReadCoordinateSequences(
        TileGeometryTransform tgs, IList<uint> geometry,
        ref int currentIndex, ref int currentX, ref int currentY, int buffer = 0, bool forPoint = false)
    {

        (var command, var count) = ParseCommandInteger(geometry[currentIndex]);
        Debug.Assert(command == MapboxCommandType.MoveTo);
        if (count > 1)
        {
            currentIndex++;
            return ReadSinglePointSequences(tgs, geometry, count, ref currentIndex, ref currentX, ref currentY);
        } // если количество MoveTo больше, чем один

        var sequences = new List<CoordinateSequence>();
        // (currentX, currentY) = (0, 0), currentIndex = 0
        var currentPosition = (currentX, currentY);
        while (currentIndex < geometry.Count)
        {
            (command, count) = ParseCommandInteger(geometry[currentIndex++]); // после команды currentIndex = 1
            Debug.Assert(command == MapboxCommandType.MoveTo);
            Debug.Assert(count == 1);

            // Read the current position
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex); // после команды currentIndex = 3

            if (!forPoint)
            {
                // Read the next command (should be LineTo)
                (command, count) = ParseCommandInteger(geometry[currentIndex++]); // после команды currentIndex = 4
                if (command != MapboxCommandType.LineTo)
                    count = 0;
            }
            else
            {
                count = 0;
            }

            // Create sequence, add starting point
            var sequence = _factory.CoordinateSequenceFactory.Create(1 + count + buffer, 2);
            var sequenceIndex = 0;
            TransformOffsetAndAddToSequence(tgs, currentPosition, sequence, sequenceIndex++);

            // Read and add offsets
            for (var i = 1; i <= count; i++)
            {
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                TransformOffsetAndAddToSequence(tgs, currentPosition, sequence, sequenceIndex++);
            }



            // Check for ClosePath command
            if (currentIndex < geometry.Count)
            {
                (command, _) = ParseCommandInteger(geometry[currentIndex]);
                if (command == MapboxCommandType.ClosePath)
                {
                    Debug.Assert(buffer > 0); 
                    // в случае лайнстринга тут будет ошибка
                    sequence.SetOrdinate(sequenceIndex, Ordinate.X, sequence.GetOrdinate(0, Ordinate.X));
                    sequence.SetOrdinate(sequenceIndex, Ordinate.Y, sequence.GetOrdinate(0, Ordinate.Y));

                    currentIndex++;
                    sequenceIndex++;
                }
            }



            Debug.Assert(sequenceIndex == sequence.Count);

            sequences.Add(sequence);
        }

        // update current position values
        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;

        return sequences.ToArray();
    }

    private CoordinateSequence[] ReadSinglePointSequences(TileGeometryTransform tgs, IList<uint> geometry,
        int numSequences, ref int currentIndex, ref int currentX, ref int currentY)
    {
        var res = new CoordinateSequence[numSequences];
        var currentPosition = (currentX, currentY);
        for (var i = 0; i < numSequences; i++)
        {
            res[i] = _factory.CoordinateSequenceFactory.Create(1, 2);

            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
            TransformOffsetAndAddToSequence(tgs, currentPosition, res[i], 0);
        }

        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;
        return res;
    }

    private void TransformOffsetAndAddToSequence(TileGeometryTransform tgs, (int x, int y) localPosition, CoordinateSequence sequence, int index)
    {
        var (longitude, latitude) = tgs.TransformInverse(localPosition.x, localPosition.y);
        sequence.SetOrdinate(index, Ordinate.X, longitude);
        sequence.SetOrdinate(index, Ordinate.Y, latitude);
    }

    private (int, int) ParseOffset((int x, int y) currentPosition, IList<uint> parameterIntegers, ref int offset)
    {
        var size = parameterIntegers.Count;
        //Console.WriteLine($"size = {size}"); // отладка

        return (currentPosition.x + Decode(parameterIntegers[offset++]),
                currentPosition.y + Decode(parameterIntegers[offset++]));
    }

    private static int Decode(uint parameterInteger)
    {
        return ((int) (parameterInteger >> 1) ^ ((int)-(parameterInteger & 1)));
    }

    private static (MapboxCommandType, int) ParseCommandInteger(uint commandInteger)
    {
        return unchecked(((MapboxCommandType) (commandInteger & 0x07U), (int)(commandInteger >> 3)));

    }



    private static IAttributesTable ReadAttributeTable(Tile.Feature mbTileFeature, List<string> keys, List<Tile.Value> values)
    {
        var att = new AttributesTable();

        for (var i = 0; i < mbTileFeature.Tags.Count; i += 2)
        {
            var key = keys[(int)mbTileFeature.Tags[i]];
            var value = values[(int)mbTileFeature.Tags[i + 1]];
            if (value.HasBoolValue)
                att.Add(key, value.BoolValue);
            else if (value.HasDoubleValue)
                att.Add(key, value.DoubleValue);
            else if (value.HasFloatValue)
                att.Add(key, value.FloatValue);
            else if (value.HasIntValue)
                att.Add(key, value.IntValue);
            else if (value.HasSIntValue)
                att.Add(key, value.SintValue);
            else if (value.HasStringValue)
                att.Add(key, value.StringValue);
            else if (value.HasUIntValue)
                att.Add(key, value.UintValue);
            else
                att.Add(key, null);
        }

        return att;
    }
}
