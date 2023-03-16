using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;

namespace MvtWatermark.NoDistortionWatermark.Auxiliary;

public class MapboxTileReaderWm
{
    private readonly GeometryFactory _factory;

    public MapboxTileReaderWm()
        : this(new GeometryFactory(new PrecisionModel(), 4326))
    {
    }

    public MapboxTileReaderWm(GeometryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates VectorTileTree from Dictionary(key = ulong tileId, value = Tile)
    /// </summary>
    /// <param name="tileDict">Dictionary (ulong, Mapbox.Tile) that contains tile id as key and Mapbox vector tile as value</param>
    /// <returns></returns>
    public VectorTileTree Read(Dictionary<ulong, Tile> tileDict)
    {
        var resultTree = new VectorTileTree();
        foreach (var tilePair in tileDict)
        {
            resultTree[tilePair.Key] = Read(tilePair.Value, tilePair.Key, null!);
        }

        return resultTree;
    }

    /// <summary>
    /// Reads a Vector Tile stream.
    /// </summary>
    /// <param name="tile">Mapbox vector tile</param>
    /// <param name="tileId">Tile id</param>
    /// <param name="idAttributeName">Optional. Specifies the name of the attribute that the vector tile feature's ID should be stored in the NetTopologySuite Features AttributeTable.</param>
    /// <returns></returns>
    public VectorTile Read(Tile tile, ulong tileId, string idAttributeName)
    {
        var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId); // TileId Хранит в себе всю нужную информацию о тайле
        var vectorTile = new VectorTile { TileId = tileDefinition.Id };
        foreach (var mbTileLayer in tile.Layers)
        {
            Debug.Assert(mbTileLayer.Version == 2U);

            var tgs = new NtsArtefacts.TileGeometryTransform(tileDefinition, mbTileLayer.Extent);
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

    /// <summary>
    /// Extracts watermark from Mapbox tile as integer
    /// </summary>
    /// <param name="tile"></param>
    /// <param name="tileId"></param>
    /// <param name="options"></param>
    /// <param name="firstHalfOfTheKey"></param>
    /// <returns></returns>
    public int? ExtractWm(Tile tile, ulong tileId, NoDistortionWatermarkOptions options, short firstHalfOfTheKey)
    {
        int key = firstHalfOfTheKey;
        key = (key << 16) + (short)tileId;

        var keySequence = SequenceGenerator.GenerateSequence(key, options.Nb, options.D, options.M);

        var extractedWatermarkIntegers = new List<int>(); // в скобочках будет Lf видимо
        var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId); 
        foreach (var mbTileLayer in tile.Layers)
        {
            Debug.Assert(mbTileLayer.Version == 2U);

            var tgs = new NtsArtefacts.TileGeometryTransform(tileDefinition, mbTileLayer.Extent);

            foreach (var mbTileFeature in mbTileLayer.Features)
            {
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

        Console.WriteLine($"Most frequent watermark in Vector tile: {mostFrequestWatermarkInt}\n\n"); // ОТЛАДКА

        return mostFrequestWatermarkInt;
    }

    private IFeature ReadFeature(NtsArtefacts.TileGeometryTransform tgs, Tile.Layer mbTileLayer, Tile.Feature mbTileFeature, string idAttributeName)
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

    private Geometry ReadGeometry(NtsArtefacts.TileGeometryTransform tgs, Tile.GeomType type, IList<uint> geometry)
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
    private int? ExtractFromFeature(NtsArtefacts.TileGeometryTransform tgs, Tile.Feature mbTileFeature, 
        NoDistortionWatermarkOptions options, int[] keySequence)
    {
        if (mbTileFeature.Type == Tile.GeomType.LineString)
        {
            var watermarkInt = ReadLineStringWm(tgs, mbTileFeature.Geometry, options, keySequence);
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
    private int? ReadLineStringWm(NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry, NoDistortionWatermarkOptions options, int[] keySequence)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        return options.AtypicalEncodingType switch
        {
            NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt =>
                ExtractWatermarkFromSingleLinestringMtLtLt(geometry, ref currentIndex, ref currentX, ref currentY, options, keySequence),
            NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtMt =>
                throw new NotImplementedException(),
            NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands =>
                ExtractWatermarkFromSingleLinestringNLt(geometry, ref currentIndex, ref currentX, ref currentY, options, keySequence),
            _ => null
        };
    }

    private Geometry ReadPoint(NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, forPoint:true);
        return CreatePuntal(sequences);
    }

    private Geometry ReadLineString(NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; var currentX = 0; var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY);
        return CreateLineal(sequences);
    }

    private Geometry ReadPolygon(NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry)
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
    private int? ExtractWatermarkFromSingleLinestringMtLtLt(IList<uint> geometry,
        ref int currentIndex, ref int currentX, ref int currentY, NoDistortionWatermarkOptions options, IReadOnlyList<int> keySequence)
    {
        var mapboxCommandString = ""; // ОТЛАДКА


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


        mapboxCommandString += $" || Command: {command}, count: {count}; Coords: "; // ОТЛАДКА
        mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА
        var firstMoveToCommandString = mapboxCommandString; // ОТЛАДКА


        while (currentIndex < geometry.Count)
        {
            (command, count) = ParseCommandInteger(geometry[currentIndex++]);


            mapboxCommandString += $" || Command: {command}, count: {count}; Coords: "; // ОТЛАДКА


            if (command == MapboxCommandType.MoveTo)
            {
                Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);


                mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА


                lastPosition2 = currentPosition;

                (command, count) = ParseCommandInteger(geometry[currentIndex++]);

                if (command != MapboxCommandType.LineTo)
                {
                    return null;
                }
                // если количество LineTo меньше двух
                if (count < 2) // это условие, возможно, не нужно
                {
                    shouldTryReversed = true;
                    break;
                }

                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);


                mapboxCommandString += $" || Command: {command}, count: {count}; Coords: "; // ОТЛАДКА
                mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА


                if (lastPosition1 != currentPosition)
                {
                    shouldTryReversed = true;
                    break;
                }

                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);


                mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА


                if (lastPosition2 != currentPosition)
                    return null;

                lastPosition1 = currentPosition;

                realSegments.Add(true);
                count -= 2;
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


                mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА
            }
        }


        if (shouldTryReversed)
        {
            //Console.BackgroundColor = ConsoleColor.DarkBlue; // ОТЛАДКА
            //Console.WriteLine("\n     REVERSED!"); // ОТЛАДКА
            //Console.BackgroundColor = ConsoleColor.Black; // ОТЛАДКА
            mapboxCommandString = firstMoveToCommandString; // ОТЛАДКА


            currentIndex = 3;
            currentPosition = savedPosition;

            lastPosition1 = currentPosition;
            lastPosition2 = currentPosition;

            var rNtgStart = false;

            while (currentIndex < geometry.Count)
            {
                (command, count) = ParseCommandInteger(geometry[currentIndex++]);


                mapboxCommandString += $" || Command: {command}, count: {count}; Coords: "; // ОТЛАДКА


                if (command == MapboxCommandType.MoveTo)
                {
                    Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);


                    mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА


                    if (lastPosition2 == currentPosition && rNtgStart)
                    {
                        realSegmentsReversedLineString.Add(true);
                    }

                    (command, count) = ParseCommandInteger(geometry[currentIndex++]);


                    mapboxCommandString += $" || Command: {command}, count: {count}; Coords: "; // ОТЛАДКА


                    if (command != MapboxCommandType.LineTo)
                    {
                        //Console.WriteLine(mapboxCommandString); // ОТЛАДКА
                        //Console.WriteLine($"\t\t---command ({command}) != LineTo"); // ОТЛАДКА


                        return null;
                    }
                }
                else
                {
                    Debug.Assert(command == MapboxCommandType.LineTo, "команда не MoveTo и не LineTo");
                }

                if (count < 1)
                {
                    //Console.WriteLine(mapboxCommandString); // ОТЛАДКА
                    //Console.WriteLine($"\t\t+++count ({count}) < 1"); // ОТЛАДКА


                    return null;
                }

                for (var i = 0; i < count - 1; i++)
                {
                    lastPosition1 = currentPosition;

                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    realSegmentsReversedLineString.Add(false);


                    mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА
                }

                lastPosition2 = currentPosition;

                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                realSegmentsReversedLineString.Add(false);


                mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА


                if (currentPosition != lastPosition1 && currentIndex != geometry.Count)
                {
                    //Console.WriteLine(mapboxCommandString); // ОТЛАДКА
                    //Console.WriteLine($"\t\t===currentPosition ({currentPosition}) != lastPosition1 ({lastPosition1})"); // ОТЛАДКА
                    //Console.WriteLine($"\t\t===current index: ({currentIndex}) ; count: ({geometry.Count})"); // ОТЛАДКА


                    return null;
                }

                rNtgStart = true;
            }

            realSegmentsReversedLineString.Reverse();
            realSegments = realSegmentsReversedLineString;
        }
        else // ОТЛАДКА
        {
            //Console.BackgroundColor = ConsoleColor.Magenta; // ОТЛАДКА
            //Console.WriteLine("\nNOT reversed!"); // ОТЛАДКА
            //Console.BackgroundColor = ConsoleColor.Black; // ОТЛАДКА
        }


        //Console.WriteLine(mapboxCommandString); // ОТЛАДКА
        //Console.WriteLine($"realSegments: {ConsoleWriter.GetIEnumerableStr(realSegments)}"); // ОТЛАДКА


        var realSegmentsInOneElementarySegmentNumber = realSegments.Count / options.D;

        // предусмотреть возможность отражения лайнстринга
        var extractedWatermarkInts = new List<int>();

        if (realSegments.Count < options.D) // если вотермарки не обнаружено (количество реальных сегментов меньше количества элементарных)
            return null;

        for (var i = 0; i < options.D/2; i++)
        {
            for (var j = 0; j < realSegmentsInOneElementarySegmentNumber; j++)
            {
                if (realSegments[realSegmentsInOneElementarySegmentNumber * i + j])
                {
                    extractedWatermarkInts.Add(keySequence[i]);
                    break;
                }
            }
        }

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

    private int? ExtractWatermarkFromSingleLinestringNLt(IList<uint> geometry,
        ref int currentIndex, ref int currentX, ref int currentY, NoDistortionWatermarkOptions options, IReadOnlyList<int> keySequence)
    {
        var mapboxCommandString = ""; // ОТЛАДКА


        // сюда запишем индексы сегментов с нетипичной геометрией
        var realSegments = new List<bool>();

        // (currentX, currentY) = (0, 0), currentIndex = 0
        var currentPosition = (currentX, currentY);

        // для проверки, что новый MoveTo не смещает курсор (почти) относительно предыдущего положения
        var lastPosition = currentPosition;

        var isFirstSegment = true;

        while (currentIndex < geometry.Count)
        {
            var (command, count) = ParseCommandInteger(geometry[currentIndex++]);
            Debug.Assert(command == MapboxCommandType.MoveTo);
            Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);


            mapboxCommandString += $" || Command: {command}, count: {count}; Coords: "; // ОТЛАДКА
            mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА

            
            if (!isFirstSegment && lastPosition != currentPosition)
            {
                //Console.WriteLine("\n[НЕ УДАЛОСЬ ИЗВЛЕЧЬ] LineString"); // ОТЛАДКА
                //Console.WriteLine($"\t\tlastPosition ({lastPosition}) != currentPosition ({currentPosition})"); // ОТЛАДКА
                //Console.WriteLine($"\t{mapboxCommandString}\n"); // ОТЛАДКА


                return null;
                // Если MoveTo не в ту же самую точку, то ЦВЗ больше не ищем и возвращаем
            }


            (command, count) = ParseCommandInteger(geometry[currentIndex++]);
            Debug.Assert(command == MapboxCommandType.LineTo, $"Assertion: MapboxCommandType = {command}");
            Debug.Assert(count >= 1, $"Assertion: LineTo count = {count}");
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

            lastPosition = currentPosition;


            mapboxCommandString += $" || Command: {command}, count: {count}; Coords: "; // ОТЛАДКА
            mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА


            realSegments.Add(!isFirstSegment);
            // В первом сегменте не может быть нетипичной геометрии, поэтому добавляется false.
            // В других же случаях новый MoveTo, прошедший проверки выше, означает наличие нетипичной геометрии, поэтому добавляется true. 
            
            isFirstSegment = false;

            for (var i = 1; i < count; i++)
            {
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                realSegments.Add(false);

                lastPosition = currentPosition;


                mapboxCommandString += $"{currentPosition} "; // ОТЛАДКА
            }
        }

        var realSegmentsInOneElementarySegmentNumber = realSegments.Count / options.D;

        // предусмотреть возможность отражения лайнстринга
        var extractedWatermarkInts = new List<int>();
        var extractedWatermarkIntsSecondHalf = new List<int>();

        if (realSegments.Count < options.D)
        {  // если вотермарки не обнаружено (количество реальных сегментов меньше количества элементарных)
            //Console.WriteLine("\n[НЕ УДАЛОСЬ ИЗВЛЕЧЬ] LineString"); // ОТЛАДКА
            //Console.WriteLine($"\t\trealSegments.Count ({realSegments.Count}) < options.D ({options.D})"); // ОТЛАДКА
            //Console.WriteLine($"\t{mapboxCommandString}\n"); // ОТЛАДКА


            return null;
        }

        // работает также с отражённым лайнстрингом
        for (var i = 0; i < options.D / 2; i++)
        {
            for (var j = 0; j < realSegmentsInOneElementarySegmentNumber; j++)
            {
                if (realSegments[realSegmentsInOneElementarySegmentNumber * i + j]) 
                {
                    extractedWatermarkInts.Add(keySequence[i]);
                    break;
                }

                /*
                if (realSegments[realSegments.Count - 1 - (realSegmentsInOneElementarySegmentNumber * i + j)])
                {
                    extractedWatermarkIntsSecondHalf.Add(keySequence[i]);
                    break;
                }
                */
            }
        }

        realSegments.RemoveAt(0);
        realSegments.Add(false);
        for (var i = 0; i < options.D / 2; i++)
        {
            for (var j = 0; j < realSegmentsInOneElementarySegmentNumber; j++)
            {
                if (realSegments[realSegments.Count - 1 - (realSegmentsInOneElementarySegmentNumber * i + j)])
                {
                    extractedWatermarkIntsSecondHalf.Add(keySequence[i]);
                    break;
                }
            }
        }

        /*
        if (extractedWatermarkInts.Count == 0)
        {
            if (extractedWatermarkIntsSecondHalf.Count == 0)
                return null;
            extractedWatermarkInts = extractedWatermarkIntsSecondHalf;
            //wasReversed = true;
        }
        */

        //Console.WriteLine("\nLineString"); // ОТЛАДКА

        if (extractedWatermarkInts.Count < extractedWatermarkIntsSecondHalf.Count)
        {
            extractedWatermarkInts = extractedWatermarkIntsSecondHalf;

            //Console.WriteLine("     REVERSED!"); // ОТЛАДКА
            realSegments.Reverse(); // ОТЛАДКА

            // а может быть бахнуть вывод в консоль всех мапбоксовых команд?
        }

        //Console.WriteLine(mapboxCommandString); // ОТЛАДКА
        //Console.WriteLine($"realSegments: { ConsoleWriter.GetIEnumerableStr(realSegments)}"); // ОТЛАДКА


        if (extractedWatermarkInts.Count == 0) return null;

        Console.WriteLine($"Extracted Watermark Ints: {ConsoleWriter.GetIEnumerableStr(extractedWatermarkInts)}"); // ОТЛАДКА

        var groupsWithCounts = from s in extractedWatermarkInts
                               group s by s into g
                               select new
                               {
                                   Item = g.Key,
                                   Count = g.Count()
                               };
        var groupsSorted = groupsWithCounts.OrderByDescending(g => g.Count);
        var mostFrequestWatermarkInt = groupsSorted.First().Item;

        //Console.WriteLine($"Most frequent watermark: {mostFrequestWatermarkInt}"); // ОТЛАДКА

        // update current position values
        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;

        return mostFrequestWatermarkInt;
    }

    private CoordinateSequence[] ReadCoordinateSequences(
        NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry,
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

    private CoordinateSequence[] ReadSinglePointSequences(NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry,
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

    private void TransformOffsetAndAddToSequence(NtsArtefacts.TileGeometryTransform tgs, (int x, int y) localPosition, CoordinateSequence sequence, int index)
    {
        var (longitude, latitude) = tgs.TransformInverse(localPosition.x, localPosition.y);
        sequence.SetOrdinate(index, Ordinate.X, longitude);
        sequence.SetOrdinate(index, Ordinate.Y, latitude);
    }

    private (int, int) ParseOffset((int x, int y) currentPosition, IList<uint> parameterIntegers, ref int offset)
    {
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
