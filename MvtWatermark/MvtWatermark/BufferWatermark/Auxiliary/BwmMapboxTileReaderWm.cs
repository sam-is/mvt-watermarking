using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using MvtWatermark.NtsArtefacts;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;

namespace MvtWatermark.BufferWatermark.Auxiliary;

public class BwmMapboxTileReaderWm
{

    private readonly BufferWatermarkOptions _options;
    private readonly GeometryFactory _factory;

    public BwmMapboxTileReaderWm(BufferWatermarkOptions options)
        : this(new GeometryFactory(new PrecisionModel(), 4326))
    {
        _options = options;
    }

    public BwmMapboxTileReaderWm(GeometryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates VectorTileTree from Dictionary(key = ulong tileId, value = Tile)
    /// </summary>
    /// <param name="tileDict">Dictionary (ulong, Mapbox.Tile) that contains tile id as key and Mapbox vector tile as value</param>
    /// <returns></returns>
    public VectorTileTree Read(Dictionary<ulong, NetTopologySuite.IO.VectorTiles.Mapbox.Tile> tileDict)
    {
        var sortedTiles = new SortedDictionary<ulong, NetTopologySuite.IO.VectorTiles.Mapbox.Tile>(); // дефолтный компаратор работает по ключу (ulong tileId) в порядке возрастания
        foreach (var (tileIndex, tile) in tileDict)
        {
            sortedTiles[tileIndex] = tileDict[tileIndex];
        }

        var resultTree = new VectorTileTree();
        foreach (var tilePair in sortedTiles)
        {
            resultTree[tilePair.Key] = Read(tilePair.Value, tilePair.Key, null!);
        }

        return resultTree;
    }

    /// <summary>
    /// Returns a Vector Tile from a Mapbox Tile.
    /// </summary>
    /// <param name="tile">Mapbox vector tile</param>
    /// <param name="tileId">Tile id</param>
    /// <param name="idAttributeName">Optional. Specifies the name of the attribute that the vector tile feature's ID should be stored in the NetTopologySuite Features AttributeTable.</param>
    /// <returns></returns>
    public VectorTile Read(NetTopologySuite.IO.VectorTiles.Mapbox.Tile tile, ulong tileId, string idAttributeName)
    {
        var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId); // TileId Хранит в себе всю нужную информацию о тайле
        var vectorTile = new VectorTile { TileId = tileDefinition.Id };
        foreach (var mbTileLayer in tile.Layers)
        {
            Debug.Assert(mbTileLayer.Version == 2U);

            var tgs = new TileGeometryTransform(tileDefinition, mbTileLayer.Extent);
            var layer = new Layer { Name = mbTileLayer.Name };
            foreach (var mbTileFeature in mbTileLayer.Features)
            {
                var feature = ReadFeature(tgs, mbTileLayer, mbTileFeature, idAttributeName);
                layer.Features.Add(feature);
            }
            vectorTile.Layers.Add(layer);
        }
        return vectorTile;
    }

    public (BitArray extractedFragment, int[] extractedFragmentInfo) ExtractWm(NetTopologySuite.IO.VectorTiles.Mapbox.Tile tile, 
        ulong tileId, BufferWatermarkOptions options, int key1, int key2, BitArray embededFragmentInfo)
    {
        key1 = (key1 << 16) + (short)tileId; // !!! проверить что выражение может быть таким !!!
        key2 = (key2 << 16) + (short)tileId;

        // Генерация {Eta_k} происходит в классе DivideBySectors, Eta_k хаписываются в объекты структуры Sector.
        // Какой здесь ключ использовать? Отдельный ещё какой-то?
        List<Sector> sectors = SectorManager.DivideBySectors(Convert.ToUInt32(options.D), key2);

        // метод GenerateSequenceS переделан для данной СВИ

        var keySequence = SequenceGenerator.GenerateSequenceS(key1, options.Nb, options.D, options.M);

        var extractedFragmentInfo = new int[options.Nb]; // -1 если бит был встроен, но не смогли извлечь;
                                                         // 0, если не был встроен и не был извлечён;
                                                         // 1, если был встроен и извлечён;
                                                         // 2, если не был встроен, но был извлечён
        var fragmentBitsDetections = new int[options.Nb][];
        for (var i = 0; i < options.Nb; i++)
        {
            fragmentBitsDetections[i] = new int[2] { 0, 0};
        }

        var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId); // TileId Хранит в себе всю нужную информацию о тайле
        foreach (var mbTileLayer in tile.Layers)
        {
            Debug.Assert(mbTileLayer.Version == 2U);

            var tgs = new TileGeometryTransform(tileDefinition, mbTileLayer.Extent);
            foreach (var mbTileFeature in mbTileLayer.Features)
            {
                ReadGeometryWm(fragmentBitsDetections, tgs, sectors, keySequence, mbTileFeature.Type, mbTileFeature.Geometry);
            }
        }

        var extractedFragment = new bool[options.Nb];

        for (var i = 0; i < options.Nb; i++)
        {
            if (fragmentBitsDetections[i][0] == 0 && fragmentBitsDetections[i][1] == 0)
            {
                if (embededFragmentInfo[i])
                {
                    extractedFragmentInfo[i] = -1;
                }
                else
                {
                    extractedFragmentInfo[i] = 0;
                }
                extractedFragment[i] = false;
                //continue;
            }
            else
            {
                extractedFragment[i] = fragmentBitsDetections[i][0] < fragmentBitsDetections[i][1] ? true : false;
                if (embededFragmentInfo[i])
                {
                    extractedFragmentInfo[i] = 1;
                }
                else
                {
                    extractedFragmentInfo[i] = 2;
                }
            }
        }

        var extractedFragmentBitArr = new BitArray(extractedFragment);

        return (extractedFragmentBitArr, extractedFragmentInfo);
    }

    private IFeature ReadFeature(MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, 
        NetTopologySuite.IO.VectorTiles.Mapbox.Tile.Layer mbTileLayer,
        NetTopologySuite.IO.VectorTiles.Mapbox.Tile.Feature mbTileFeature, string idAttributeName)
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

    /// <summary>
    /// Читаем геометрию
    /// </summary>
    /// <param name="tgs"></param>
    /// <param name="type"></param>
    /// <param name="geometry"></param>
    /// <returns></returns>
    private Geometry ReadGeometry(MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, 
        NetTopologySuite.IO.VectorTiles.Mapbox.Tile.GeomType type, IList<uint> geometry)
    {
        switch (type)
        {
            case NetTopologySuite.IO.VectorTiles.Mapbox.Tile.GeomType.Point:
                return ReadPoint(tgs, geometry);

            case NetTopologySuite.IO.VectorTiles.Mapbox.Tile.GeomType.LineString:
                return ReadLineString(tgs, geometry);

            case NetTopologySuite.IO.VectorTiles.Mapbox.Tile.GeomType.Polygon:
                return ReadPolygon(tgs, geometry);
        }

        return null;
    }

    private void ReadGeometryWm(int[][] fragmentBitsDetections, MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, List<Sector> sectors, int[] keySequence,
        NetTopologySuite.IO.VectorTiles.Mapbox.Tile.GeomType type, IList<uint> geometry)
    {
        if (type is NetTopologySuite.IO.VectorTiles.Mapbox.Tile.GeomType.LineString)
        {
            var currentIndex = 0;
            var currentX = 0;
            var currentY = 0;
            ReadCoordinateSequencesWm(fragmentBitsDetections, tgs, sectors, keySequence, geometry, ref currentIndex, ref currentX, ref currentY);
        }
    }

    private Geometry ReadPoint(MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; 
        var currentX = 0; 
        var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, forPoint:true);
        return CreatePuntal(sequences);
    }

    /// <summary>
    /// Продолжаем читать уж лайнстринг
    /// </summary>
    /// <param name="tgs"></param>
    /// <param name="geometry"></param>
    /// <returns></returns>
    private Geometry ReadLineString(MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; 
        var currentX = 0; 
        var currentY = 0;
        var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY);
        return CreateLineal(sequences);
    }

    private Geometry ReadPolygon(MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry)
    {
        var currentIndex = 0; 
        var currentX = 0; 
        var currentY = 0;
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
    /// метод извлечения из линий
    /// </summary>
    /// <param name="tgs"></param>
    /// <param name="geometry"></param>
    /// <param name="currentIndex"></param>
    /// <param name="currentX"></param>
    /// <param name="currentY"></param>
    /// <param name="buffer"></param>
    /// <returns></returns>
    private void ReadCoordinateSequencesWm(int[][] fragmentBitsDetections,
        MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, List<Sector> sectors, int[] keySequence, IList<uint> geometry,
        ref int currentIndex, ref int currentX, ref int currentY)
    {

        //var pixelCoordinatesList = new List<List<int>>();
        (var command, var count) = ParseCommandInteger(geometry[currentIndex]);
        Debug.Assert(command == MapboxCommandType.MoveTo);

        // (currentX, currentY) = (0, 0), currentIndex = 0
        var currentPosition = (currentX, currentY);
        while (currentIndex < geometry.Count)
        {
            var mapboxPixelCoordinates = new List<(long x, long y, bool outsideExtentFlag, bool outsideBufferEdgeFlag)>();
            //pixelCoordinates.Add((currentPosition.currentX, tgs.Extent - currentPosition.currentY));

            (command, count) = ParseCommandInteger(geometry[currentIndex++]); // после команды currentIndex = 1
            Debug.Assert(command == MapboxCommandType.MoveTo);
            Debug.Assert(count == 1);

            // Read the current position
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex); // после команды currentIndex = 3

            var positionInSpace = CheckPositionInSpace(currentPosition, tgs.Extent);
            //mapboxPixelCoordinates.Add((currentPosition.currentX, tgs.Extent - currentPosition.currentY, CheckIfCoordinateIsOutside(currentPosition, tgs.Extent), CheckIfCoordinateIsOnBufferEdge(currentPosition, tgs.Extent)));
            mapboxPixelCoordinates.Add((currentPosition.currentX, tgs.Extent - currentPosition.currentY, positionInSpace.outside, positionInSpace.outsideBufferEdge));

            // именно в этом методе мы получаем координаты точек в пикселях. Здесь нужно создавать сначала списки точек, из них -
            // списки отрезков и передавать в отдельный метод, который проверяет их наклон и сопоставляет со списком секторов.
            // Всё это скорее всего будет записываться в один большой список соответствия, сколько единичек и нулей записано для
            // такого-то бита ЦВЗ. Но это, похоже, ведёт к тому, что экземпляры класса ридера будут создаваться отдельные для каждого тайла.

            // Read the next command (should be LineTo)
            (command, count) = ParseCommandInteger(geometry[currentIndex++]); // после команды currentIndex = 4
            if (command != MapboxCommandType.LineTo)
                count = 0;

            // Read and add offsets
            for (var i = 1; i <= count; i++)
            {
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                positionInSpace = CheckPositionInSpace(currentPosition, tgs.Extent);
                mapboxPixelCoordinates.Add((currentPosition.currentX, tgs.Extent - currentPosition.currentY, positionInSpace.outside, positionInSpace.outsideBufferEdge));
            }

            //Debug.Assert(sequenceIndex == sequence.Count);

            ExtractFromCoordinates(mapboxPixelCoordinates, fragmentBitsDetections, sectors, keySequence);
        }

        // update current position values
        currentX = currentPosition.currentX;
        currentY = currentPosition.currentY;

        // а лишнее в этом классе желательно посносить. Мы же в итоге не будем формировать векторные тайлы из мапбоксовских,
        // нам нужна только извлечённая из ЦВЗ информация
    }

    private void ExtractFromCoordinates(List<(long x, long y, bool outsideExtentFlag, bool outsideBufferEdgeFlag)> mapboxPixelCoordinates, 
        int[][] fragmentBitsDetections, List<Sector> sectors, int[] keySequence)
    {
        var segmentsInfo = new List<(bool crosses, bool outsideBufferEdge, long dx, long dy, bool firstInside)>(mapboxPixelCoordinates.Count - 1); // по умолчанию false ведь все?
        for (var i = 0; i < mapboxPixelCoordinates.Count - 1; i++)
        {
            var crosses = false;
            var outsideBufferEdge = false;
            long dx = 0;
            long dy = 0;
            var firstInside = false;

            //if (coordsOutsideFlags[i] ^ coordsOutsideFlags[i + 1])
            //{
            //    segmentsCrossing[i] = true;
            //}

            if (!mapboxPixelCoordinates[i].outsideExtentFlag && mapboxPixelCoordinates[i + 1].outsideExtentFlag)
            {
                crosses = true;
                dx = mapboxPixelCoordinates[i + 1].x - mapboxPixelCoordinates[i].x;
                dy = mapboxPixelCoordinates[i + 1].y - mapboxPixelCoordinates[i].y;
                firstInside = true;
                if (mapboxPixelCoordinates[i + 1].outsideBufferEdgeFlag)
                    outsideBufferEdge = true;
            }
            else if (mapboxPixelCoordinates[i].outsideExtentFlag && !mapboxPixelCoordinates[i + 1].outsideExtentFlag)
            {
                crosses = true;
                dx = mapboxPixelCoordinates[i].x - mapboxPixelCoordinates[i + 1].x;
                dy = mapboxPixelCoordinates[i].y - mapboxPixelCoordinates[i + 1].y;
                if (mapboxPixelCoordinates[i].outsideBufferEdgeFlag)
                    outsideBufferEdge = true;
            }
            segmentsInfo.Add((crosses, outsideBufferEdge, dx, dy, firstInside));
        }

        for (var i = 0; i < segmentsInfo.Count; i++)
        {
            if (segmentsInfo[i].crosses)
            {
                var atan = Math.Atan2((double)segmentsInfo[i].dy, (double)segmentsInfo[i].dx);
                if (atan < 0)
                    atan = 2 * Math.PI + atan;
                for (var j = 0; j < sectors.Count; j++)
                {
                    if (atan >= sectors[j].LowerBorder && atan < sectors[j].UpperBorder)
                    {
                        if (segmentsInfo[i].outsideBufferEdge) 
                        {
                            if (sectors[j].EtaK)
                            {
                                fragmentBitsDetections[keySequence[j]][1]++;
                            }
                            else
                            {
                                fragmentBitsDetections[keySequence[j]][0]++;
                            }
                        }
                        else
                        {
                            if (!sectors[j].EtaK)
                            {
                                fragmentBitsDetections[keySequence[j]][1]++;
                            }
                            else
                            {
                                fragmentBitsDetections[keySequence[j]][0]++;
                            }
                        }

                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Читаем последовательности координат
    /// </summary>
    /// <param name="tgs"></param>
    /// <param name="geometry"></param>
    /// <param name="currentIndex"></param>
    /// <param name="currentX"></param>
    /// <param name="currentY"></param>
    /// <param name="buffer"></param>
    /// <param name="forPoint"></param>
    /// <returns></returns>
    private CoordinateSequence[] ReadCoordinateSequences(
        MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry,
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

    private CoordinateSequence[] ReadSinglePointSequences(MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, IList<uint> geometry,
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

    private void TransformOffsetAndAddToSequence(MvtWatermark.NtsArtefacts.TileGeometryTransform tgs, (int x, int y) localPosition, CoordinateSequence sequence, int index)
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




    private static IAttributesTable ReadAttributeTable(NetTopologySuite.IO.VectorTiles.Mapbox.Tile.Feature mbTileFeature,
        List<string> keys, List<NetTopologySuite.IO.VectorTiles.Mapbox.Tile.Value> values)
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

    /*
    private bool CheckIfCoordinateIsOnBufferEdge((long x, long y) coordinate, uint extent)
    {
        if (coordinate.x == extent + _options.Buffer || coordinate.y == extent + _options.Buffer 
            || coordinate.x == -_options.Buffer || coordinate.y == -_options.Buffer)
            return true;

        return false;
    }

    private bool CheckIfCoordinateIsOutside((int x, int y) coordinate, uint extent)
    {
        if (coordinate.x > extent || coordinate.y > extent || coordinate.x < 0 || coordinate.y < 0)
            return true;

        return false;
    }
    */

    private (bool outside, bool outsideBufferEdge) CheckPositionInSpace((int x, int y) coordinate, uint extent)
    {
        var outside = false;
        var outsideBufferEdge = false;

        if (coordinate.x >= extent || coordinate.y >= extent || coordinate.x <= 0 || coordinate.y <= 0)
            outside = true;
        else return (false, false);

        if (coordinate.x > extent + _options.Buffer || coordinate.y > extent + _options.Buffer
            || coordinate.x < -_options.Buffer || coordinate.y < -_options.Buffer)
            outsideBufferEdge = true;
        return (outside, outsideBufferEdge);
    }
}
