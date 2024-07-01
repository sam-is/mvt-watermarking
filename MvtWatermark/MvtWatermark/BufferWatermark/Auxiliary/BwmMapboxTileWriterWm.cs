using System;
using System.Collections.Generic;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Tiles.WebMercator;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.Collections;
using System.Linq;
using NetTopologySuite.Geometries.Implementation;
//using MvtWatermark.NoDistortionWatermark.Auxiliary;
//using MvtWatermark.NtsArtefacts;

namespace MvtWatermark.BufferWatermark.Auxiliary;

// see: https://github.com/mapbox/vector-tile-spec/tree/master/2.1
public class BwmMapboxTileWriterWm
{
    public List<bool> BitFlags { get => _bitFlags; }

    private BufferWatermarkOptions _options;
    //private bool _hasSuccessfullyEmbededIntoSingleTile;
    private List<bool> _bitFlags;
    private int[] _keySequence; // думаю она будет хранить номера бит фрагмента ЦВЗ, каждый по m раз, а всего элементов D.
                                // Элементы соответствуют секторам от 0 до 360 градусов против часовой стрелки
    private List<Sector> _sectors;
    private int _buffer;

    private BitArray? _embededBitsFromMessage;
    private BitArray? _embededBitsFromFragment;
    private int _embededMessageIndex;

    /*
    public BitArray? EmbededBitsFromMessage
    {
        get => _embededBitsFromMessage;
    }
    */

    public BwmMapboxTileWriterWm(BufferWatermarkOptions options)
    {
        _options = options;
        _buffer = options.Buffer; // ПЕРЕДЕЛАТЬ, отдельное поле для буфера не нужно!
    }

    public Dictionary<ulong, Tile> WriteWm(VectorTileTree tree, BitArray message,
        short key1, short key2, out BitArray embededBitsFromMessage, uint extent = 4096)
    {
        // здесь тайловый словарь сортируется
        var sortedTiles = new SortedDictionary<ulong, VectorTile>(); // дефолтный компаратор работает по ключу (ulong tileId) в порядке возрастания
        foreach (var tileIndex in tree)
        {
            sortedTiles[tileIndex] = tree[tileIndex];
        }

        var result = new Dictionary<ulong, Tile>();

        if (message.Count < sortedTiles.Count() * _options.Nb)
        {
            throw new ArgumentException("Not enough bits in the watermark message",
                $"Bits' number: {message.Count}, minimal required bits number: {sortedTiles.Count() * _options.Nb}");
        }

        _embededBitsFromMessage = new BitArray(tree.Count() * _options.Nb, false); // здесь будем хранить инфу о встроенных и невстроенных битах ЦВЗ
        _embededBitsFromFragment = new BitArray(_options.Nb, false);

        //_hasSuccessfullyEmbededIntoSingleTile = false;

        var watermarkString = new BitArray(message); // это нужно, чтобы оригинальный BitArray message не изменялся при RightShift
        var watermarkStringFragment = new BitArray(_options.Nb);

        _embededMessageIndex = 0;

        // переменные для норм сообщений в исключениях
        //var tileNumber = 0; // текущий номер тайла в дереве (фактически это Dictionary, и тайлы хранятся в нём в порядке добавления)
        //var wmStartIndex = 0;

        foreach (var (tileIndex, vectorTile) in sortedTiles)
        {
            for (var i = 0; i < _options.Nb; i++)
            {
                watermarkStringFragment[i] = watermarkString[i];
                _embededBitsFromFragment[i] = false;
            }

            Tile resultTile = WriteWm(vectorTile, watermarkStringFragment, key1, key2, tileIndex,
                extent);
            // В данном случае обычной записи через WriteWm -> EncodeWm скорее всего не получится,
            // так как мы будем двигать точки в процессе. Поэтому надо оставить ниже обычный метод Write

            watermarkString.RightShift(_options.Nb); 
            // тут вроде всё правильно. Получается, BitArray справа налево идёт, то есть с самого младшего бита,
            // как и должно быть?

            _embededBitsFromFragment.CopyNbBitsTo(_embededBitsFromMessage, _embededMessageIndex * _options.Nb, _options.Nb);

            //watermarkStringFragment.CopyNbBitsTo(embededMessageFiller, _embededMessageIndex * _options.Nb, _options.Nb);
            _embededMessageIndex++;

            result.Add(tileIndex, resultTile);

            //tileNumber++;
            //wmStartIndex += _options.Nb;
        }

        embededBitsFromMessage = _embededBitsFromMessage;

        return result;
    }

    private Tile WriteWm(VectorTile vectorTile, BitArray watermarkStringFragment, int key1, int key2,
        ulong tileId, uint extent = 4096, string idAttributeName = "id")
    {
        //var watermarkInt = WatermarkTransform.GetIntFromBitArray(watermarkString); // Фрагмент ЦВЗ в int

        key1 = (key1 << 16) + (short)vectorTile.TileId;
        key2 = (key2 << 16) + (short)vectorTile.TileId;

        // Генерация {Eta_k} происходит в классе DivideBySectors, Eta_k хаписываются в объекты структуры Sector.
        // Какой здесь ключ использовать? Отдельный ещё какой-то?
        _sectors = SectorManager.DivideBySectors(Convert.ToUInt32(_options.D), key2); 

        // метод GenerateSequenceS переделан для данной СВИ
        _keySequence = SequenceGenerator.GenerateSequenceS(key1, _options.Nb, _options.D, _options.M);

        // список флагов для каждого бита фрагмента ЦВЗ. Изначально все флаги выставлены в False.
        // Если есть хотя бы один отрезок, попадающий в секторы бита и подходящий для изменения, флаг помечается True.
        // Если все флаги будут True, _hasSuccessfullyEmbededIntoSingleTile будет выставлен в True.
        //_bitFlags = new List<bool>(new bool[_options.Nb]); // Создаём List из массива булей. Bool по умолчанию False.
                                                             // Хотя возможно лучше было это всё форычем заполнить
                                                             // без таких выкидонов

        var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(vectorTile.TileId);

        var mapboxTile = new Tile();

        var tgt = new NtsArtefacts.TileGeometryTransform(tile, extent);

        foreach (var localLayer in vectorTile.Layers)
        {
            var layer = new Tile.Layer { Version = 2, Name = localLayer.Name, Extent = extent };

            var keys = new Dictionary<string, uint>();
            var values = new Dictionary<Tile.Value, uint>();

            //var embedingIndex = 0; // для работы с Lf

            foreach (var localLayerFeature in localLayer.Features)
            {
                var feature = new Tile.Feature();

                // Encode geometry
                switch (localLayerFeature.Geometry)
                {
                    case IPuntal puntal:
                        feature.Type = Tile.GeomType.Point;
                        feature.Geometry.AddRange(Encode(puntal, tgt));
                        break;
                    case ILineal lineal:
                        feature.Type = Tile.GeomType.LineString;
                        feature.Geometry.AddRange(EncodeWm(lineal, watermarkStringFragment, tgt));
                        break;
                    case IPolygonal polygonal:
                        feature.Type = Tile.GeomType.Polygon;
                        feature.Geometry.AddRange(Encode(polygonal, tgt, tile.Zoom));
                        break;
                    default:
                        feature.Type = Tile.GeomType.Unknown;
                        break;
                }

                // If geometry collapsed during encoding, we don't add the feature at all
                if (feature.Geometry.Count == 0)
                    continue;

                // Translate attributes for feature
                AddAttributes(feature.Tags, keys, values, localLayerFeature.Attributes);

                //Try and retrieve an ID from the attributes.
                var id = localLayerFeature.Attributes.GetOptionalValue(idAttributeName);

                //Converting ID to string, then trying to parse. This will handle situations will ignore situations where the ID value is not actually an integer or ulong number.
                if (id != null && ulong.TryParse(id.ToString(), out var idVal))
                {
                    feature.Id = idVal;
                }

                // Add feature to layer
                layer.Features.Add(feature);
            }

            layer.Keys.AddRange(keys.Keys);
            layer.Values.AddRange(values.Keys);

            mapboxTile.Layers.Add(layer);
        }

        return mapboxTile;
    }

    /// <summary>
    /// Возвращает Mapbox Tile, полученный из VectorTile
    /// </summary>
    /// <param name="vectorTile"></param>
    /// <param name="extent"></param>
    /// <param name="idAttributeName"></param>
    /// <returns></returns>
    public Tile GetMapboxTileFromVectorTile(VectorTile vectorTile, uint extent = 4096, string idAttributeName = "id")
    {
        var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(vectorTile.TileId);
        var tgt = new MvtWatermark.NtsArtefacts.TileGeometryTransform(tile, extent);

        var mapboxTile = new Tile();
        foreach (var localLayer in vectorTile.Layers)
        {
            var layer = new Tile.Layer { Version = 2, Name = localLayer.Name, Extent = extent };
            Console.WriteLine(layer.Name); // ОТЛАДКА

            var keys = new Dictionary<string, uint>();
            var values = new Dictionary<Tile.Value, uint>();

            foreach (var localLayerFeature in localLayer.Features)
            {
                var feature = new Tile.Feature();

                // Encode geometry
                switch (localLayerFeature.Geometry)
                {
                    case IPuntal puntal:
                        feature.Type = Tile.GeomType.Point;
                        feature.Geometry.AddRange(Encode(puntal, tgt));
                        break;
                    case ILineal lineal:
                        feature.Type = Tile.GeomType.LineString;
                        feature.Geometry.AddRange(Encode(lineal, tgt));
                        break;
                    case IPolygonal polygonal:
                        feature.Type = Tile.GeomType.Polygon;
                        feature.Geometry.AddRange(Encode(polygonal, tgt, tile.Zoom));
                        break;
                    default:
                        feature.Type = Tile.GeomType.Unknown;
                        break;
                }

                // If geometry collapsed during encoding, we don't add the feature at all
                if (feature.Geometry.Count == 0)
                    continue;

                // Translate attributes for feature
                AddAttributes(feature.Tags, keys, values, localLayerFeature.Attributes);

                //Try and retrieve an ID from the attributes.
                var id = localLayerFeature.Attributes.GetOptionalValue(idAttributeName);

                //Converting ID to string, then trying to parse. This will handle situations will ignore situations where the ID value is not actually an integer or ulong number.
                if (id != null && ulong.TryParse(id.ToString(), out var idVal))
                {
                    feature.Id = idVal;
                }

                // Add feature to layer
                layer.Features.Add(feature);
            }

            layer.Keys.AddRange(keys.Keys);
            layer.Values.AddRange(values.Keys);

            mapboxTile.Layers.Add(layer);
        }
        return mapboxTile;
    }

    /// <summary>
    /// Creates and returnes Mapbox Tile from VectorTile.
    /// </summary>
    /// <param name="vectorTile">The vector tile.</param>
    /// <param name="extent">The extent.</param>
    /// <param name="idAttributeName">The name of an attribute property to use as the ID for the Feature. Vector tile feature ID's should be integer or ulong numbers.</param>
    public Tile Write(VectorTile vectorTile, uint extent = 4096, string idAttributeName = "id")
    {
        var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(vectorTile.TileId);
        var tgt = new MvtWatermark.NtsArtefacts.TileGeometryTransform(tile, extent);

        var mapboxTile = new Tile();
        foreach (var localLayer in vectorTile.Layers)
        {
            var layer = new Tile.Layer { Version = 2, Name = localLayer.Name, Extent = extent };

            var keys = new Dictionary<string, uint>();
            var values = new Dictionary<Tile.Value, uint>();

            foreach (var localLayerFeature in localLayer.Features)
            {
                var feature = new Tile.Feature();

                // Encode geometry
                switch (localLayerFeature.Geometry)
                {
                    case IPuntal puntal:
                        feature.Type = Tile.GeomType.Point;
                        feature.Geometry.AddRange(Encode(puntal, tgt));
                        break;
                    case ILineal lineal:
                        feature.Type = Tile.GeomType.LineString;
                        feature.Geometry.AddRange(Encode(lineal, tgt));
                        break;
                    case IPolygonal polygonal:
                        feature.Type = Tile.GeomType.Polygon;
                        feature.Geometry.AddRange(Encode(polygonal, tgt, tile.Zoom));
                        break;
                    default:
                        feature.Type = Tile.GeomType.Unknown;
                        break;
                }

                // If geometry collapsed during encoding, we don't add the feature at all
                if (feature.Geometry.Count == 0)
                    continue;

                // Translate attributes for feature
                AddAttributes(feature.Tags, keys, values, localLayerFeature.Attributes);

                //Try and retrieve an ID from the attributes.
                var id = localLayerFeature.Attributes.GetOptionalValue(idAttributeName);

                //Converting ID to string, then trying to parse. This will handle situations will ignore situations where the ID value is not actually an integer or ulong number.
                if (id != null && ulong.TryParse(id.ToString(), out var idVal))
                {
                    feature.Id = idVal;
                }

                // Add feature to layer
                layer.Features.Add(feature);
            }

            layer.Keys.AddRange(keys.Keys);
            layer.Values.AddRange(values.Keys);

            mapboxTile.Layers.Add(layer);
        }

        //ProtoBuf.Serializer.Serialize<Tile>(stream, mapboxTile);
        return mapboxTile;
    }

    private static void AddAttributes(List<uint> tags, Dictionary<string, uint> keys,
        Dictionary<Tile.Value, uint> values, IAttributesTable attributes)
    {
        if (attributes == null || attributes.Count == 0)
            return;

        var aKeys = attributes.GetNames();
        var aValues = attributes.GetValues();

        for (var a = 0; a < aKeys.Length; a++)
        {
            var key = aKeys[a];
            if (string.IsNullOrEmpty(key)) continue;

            var tileValue = ToTileValue(aValues[a]);
            if (tileValue == null) continue;

            //tags.Add(keys.AddOrGet(key));
            //tags.Add(values.AddOrGet(tileValue));
            tags.Add(MvtWatermark.NtsArtefacts.DictionaryExtensions.AddOrGet<string>(keys, key));
            tags.Add(MvtWatermark.NtsArtefacts.DictionaryExtensions.AddOrGet<Tile.Value>(values, tileValue));
        }
    }

    private static Tile.Value ToTileValue(object value)
    {
        switch (value)
        {
            case bool boolValue:
                return new Tile.Value { BoolValue = boolValue };

            case sbyte sbyteValue:
                return new Tile.Value { IntValue = sbyteValue };
            case short shortValue:
                return new Tile.Value { IntValue = shortValue };
            case int intValue:
                return new Tile.Value { IntValue = intValue };
            case long longValue:
                return new Tile.Value { IntValue = longValue };

            case byte byteValue:
                return new Tile.Value { UintValue = byteValue };
            case ushort ushortValue:
                return new Tile.Value { UintValue = ushortValue };
            case uint uintValue:
                return new Tile.Value { UintValue = uintValue };
            case ulong ulongValue:
                return new Tile.Value { UintValue = ulongValue };

            case double doubleValue:
                return new Tile.Value { DoubleValue = doubleValue };
            case float floatValue:
                return new Tile.Value { FloatValue = floatValue };

            case string stringValue:
                return new Tile.Value { StringValue = stringValue };
        }

        return null;
    }

    private static IEnumerable<uint> Encode(IPuntal puntal, MvtWatermark.NtsArtefacts.TileGeometryTransform tgt)
    {
        const int coordinateIndex = 0;

        var geometry = (Geometry)puntal;
        int currentX = 0, currentY = 0;

        var parameters = new List<uint>();
        for (var i = 0; i < geometry.NumGeometries; i++)
        {
            var point = (Point)geometry.GetGeometryN(i);
            (var x, var y) = tgt.Transform(point.CoordinateSequence, coordinateIndex, ref currentX, ref currentY);
            if (i == 0 || x > 0 || y > 0)
            {
                parameters.Add(GenerateParameterInteger(x));
                parameters.Add(GenerateParameterInteger(y));
            }
        }

        // Return result
        yield return GenerateCommandInteger(MapboxCommandType.MoveTo, parameters.Count / 2);
        foreach (var parameter in parameters)
            yield return parameter;

    }

    private static IEnumerable<uint> Encode(ILineal lineal, MvtWatermark.NtsArtefacts.TileGeometryTransform tgt)
    {
        var geometry = (Geometry)lineal;
        int currentX = 0, currentY = 0;
        for (var i = 0; i < geometry.NumGeometries; i++)
        {
            var lineString = (LineString)geometry.GetGeometryN(i);
            foreach (var encoded in Encode(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, false))
                yield return encoded;
        }
    }

    /// <summary>
    /// Encode with watermark fragment
    /// </summary>
    /// <param name="lineal"></param>
    /// <param name="tgt"></param>
    /// <returns></returns>
    private IEnumerable<uint> EncodeWm(ILineal lineal, BitArray watermarkStringFragment, NtsArtefacts.TileGeometryTransform tgt)
    {
        var geometry = (Geometry)lineal;
        int currentX = 0, currentY = 0;
        for (var i = 0; i < geometry.NumGeometries; i++)
        {
            var lineString = (LineString)geometry.GetGeometryN(i);
            foreach (var encoded in EncodeWm(lineString.CoordinateSequence, watermarkStringFragment, tgt, ref currentX, ref currentY))
                yield return encoded;
        }
    }

    private static IEnumerable<uint> Encode(IPolygonal polygonal, 
        MvtWatermark.NtsArtefacts.TileGeometryTransform tgt, int zoom)
    {
        var geometry = (Geometry)polygonal;

        //Test the whole polygon geometry is larger than a single pixel.
        if (IsGreaterThanOnePixelOfTile(geometry, zoom))
        {
            int currentX = 0, currentY = 0;
            for (var i = 0; i < geometry.NumGeometries; i++)
            {
                var polygon = (Polygon)geometry.GetGeometryN(i);

                //Test that individual polygons are larger than a single pixel.
                if (!IsGreaterThanOnePixelOfTile(polygon, zoom))
                    continue;

                foreach (var encoded in Encode(polygon.Shell.CoordinateSequence, tgt, ref currentX, ref currentY, true, false))
                    yield return encoded;
                foreach (var hole in polygon.InteriorRings)
                {
                    foreach (var encoded in Encode(hole.CoordinateSequence, tgt, ref currentX, ref currentY, true, true))
                        yield return encoded;
                }
            }
        }
    }

    private IEnumerable<uint> EncodeWm(CoordinateSequence sequence, BitArray watermarkStringFragment,
        NtsArtefacts.TileGeometryTransform tgt,
        ref int currentX, ref int currentY)
    {
        var copyCurrentX = currentX;
        var copyCurrentY = currentY;

        sequence = GetCoordinateSequenceWm(sequence, watermarkStringFragment, tgt, copyCurrentX, copyCurrentY);

        //currentX = curX;
        //currentY = curY;

        // how many parameters for LineTo command
        var count = sequence.Count;

        var encoded = new List<uint>();

        // Start point
        encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
        var position = tgt.TransformExtended(sequence, 0, ref currentX, ref currentY);
        encoded.Add(GenerateParameterInteger(position.dx));
        encoded.Add(GenerateParameterInteger(position.dy));

        // Add LineTo command (stub)
        var lineToCount = 0;
        encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, lineToCount));
        for (var i = 1; i < count; i++)
        {
            position = tgt.TransformExtended(sequence, i, ref currentX, ref currentY);

            if (position.dx != 0 || position.dy != 0)
            {
                encoded.Add(GenerateParameterInteger(position.dx));
                encoded.Add(GenerateParameterInteger(position.dy));
                lineToCount++;
            }
        }
        if (lineToCount > 0)
            encoded[3] = GenerateCommandInteger(MapboxCommandType.LineTo, lineToCount);

        // Validate encoded data
        // A line has 1 MoveTo and 1 LineTo command.
        // A line is valid if it has at least 2 points
        if (encoded.Count - 2 < 4)
            encoded.Clear();

        return encoded;
    }

    private CoordinateSequence GetCoordinateSequenceWm(CoordinateSequence sequence, BitArray watermarkStringFragment,
        NtsArtefacts.TileGeometryTransform tgt, int currentX, int currentY)
    {
        // пока для удобства отдельными циклами всё, но потом надо сделать в одном-двух в новом методе, чтобы уменьшить
        // вычислительную сложность алгоритма
        var mapboxPixelCoordinates = new List<(int x, int y)>();

        // !!! для удобства восприятия и работы с угловой окружностью преображаем y-координаты: y = extent - y
        // а потом надо обратно
        for (var i = 0; i < sequence.Count; i++)
        {
            var position = tgt.TransformExtended(sequence, i, ref currentX, ref currentY);
            //mapboxPixelCoordinates.Add((position.x, position.y));
            mapboxPixelCoordinates.Add((position.x, (int)tgt.Extent - position.y)); // long -> int , это наверное неправильно..
        }

        /*
        for (var i = 0; i < mapboxPixelCoordinates.Count; i++)
        {
            // если координата за пределами тайла
            if (CheckIfCoordinateIsOutside(mapboxPixelCoordinates[i], tgt.Extent))
            {
                bool? leftCoordinateIsOutside = null;
                bool? rightCoordinateIsOutside = null;
                if (i != 0)
                    leftCoordinateIsOutside = CheckIfCoordinateIsOutside(mapboxPixelCoordinates[i - 1], tgt.Extent);
                if (i != mapboxPixelCoordinates.Count - 1)
                    rightCoordinateIsOutside = CheckIfCoordinateIsOutside(mapboxPixelCoordinates[i + 1], tgt.Extent);


            }
        }
        */

        var coordsOutsideFlags = new bool[mapboxPixelCoordinates.Count];

        for (var i = 0; i < mapboxPixelCoordinates.Count; i++)
        {
            // если координата за пределами тайла
            coordsOutsideFlags[i] = CheckIfCoordinateIsOutside(mapboxPixelCoordinates[i], tgt.Extent);
        }


        //var segmentsCrossing = new bool[mapboxPixelCoordinates.Count - 1]; // по умолчанию false ведь все?
        var segmentsInfo = new List<(bool crosses, int dx, int dy, bool firstInside)>(mapboxPixelCoordinates.Count - 1); // по умолчанию false ведь все?
        for (var i = 0; i < mapboxPixelCoordinates.Count - 1; i++)
        {
            //if ((coordsOutsideFlags[i] && !coordsOutsideFlags[i + 1]) || 
            //    (!coordsOutsideFlags[i] && coordsOutsideFlags[i + 1]))
            var crosses = false;
            var dx = 0;
            var dy = 0;
            var firstInside = false;

            //if (coordsOutsideFlags[i] ^ coordsOutsideFlags[i + 1])
            //{
            //    segmentsCrossing[i] = true;
            //}

            if (!coordsOutsideFlags[i] && coordsOutsideFlags[i + 1])
            {
                crosses = true;
                dx = mapboxPixelCoordinates[i + 1].x - mapboxPixelCoordinates[i].x;
                dy = mapboxPixelCoordinates[i + 1].y - mapboxPixelCoordinates[i].y;
                firstInside = true;
            }
            else if (coordsOutsideFlags[i] && !coordsOutsideFlags[i + 1])
            {
                crosses = true;
                dx = mapboxPixelCoordinates[i].x - mapboxPixelCoordinates[i + 1].x;
                dy = mapboxPixelCoordinates[i].y - mapboxPixelCoordinates[i + 1].y;
                //firstInside = false;
            }
            segmentsInfo.Add((crosses, dx, dy, firstInside));
        }

        List<(int x, int y)> resultCoordinatesList = MoveCoordinates(mapboxPixelCoordinates, watermarkStringFragment, segmentsInfo, (int)tgt.Extent);
        var resultCoordinates = new Coordinate[resultCoordinatesList.Count];
        for (var i = 0; i < resultCoordinatesList.Count; i++)
        {
            //resultCoordinateSequence[i].y = (int)tgt.Extent - resultCoordinateSequence[i].y;
            //resultCoordinates[i] = (resultCoordinatesList[i].x, (int)tgt.Extent - resultCoordinatesList[i].y);

            // y возвращаем обратно к системе координат mapbox
            (var pixelX, var pixelY) = (resultCoordinatesList[i].x, (int)tgt.Extent - resultCoordinatesList[i].y);
            (var lon, var lat) = tgt.TransformInverse(pixelX, pixelY);
            resultCoordinates[i] = new Coordinate(lon, lat);

            //resultCoordinates[i] = new Coordinate(resultCoordinatesList[i].x, (int)tgt.Extent - resultCoordinatesList[i].y);
        }

        var resultCoordinateSequence = new CoordinateArraySequence(resultCoordinates);
        return resultCoordinateSequence;
    }

    private List<(int x, int y)> MoveCoordinates(List<(int x, int y)> mapboxPixelCoordinates, 
        BitArray watermarkStringFragment, List<(bool crosses, int dx, int dy, bool firstInside)> segmentsInfo, int extent)
    {
        var newCoords = new List<(int x, int y)>(mapboxPixelCoordinates);
        var difference = 0; // индексовая разница между mapboxPixelCoordinates и newCoords (т.к. мы добавляем в newCoords доп. точки)
        for (var i = 0; i < segmentsInfo.Count; i++) // Count должен обновляться во время итераций, здесь не должно быть ошибки 
        {
            if (segmentsInfo[i].crosses)
            {
                //if (segmentsInfo[i].firstInside)
                var atan = Math.Atan2((double)segmentsInfo[i].dy, (double)segmentsInfo[i].dx);
                if (atan < 0) // atan2 возвращает значения, считая по часовой стрелке для нижнего полукруга, то есть например не 3Pi/2, а -Pi/2
                    atan = 2 * Math.PI + atan;
                for (var j = 0; j < _sectors.Count; j++)
                {
                    if (atan >= _sectors[j].LowerBorder && atan < _sectors[j].UpperBorder)
                    {
                        _embededBitsFromFragment[_keySequence[j]] = true;
                        if (!(Convert.ToBoolean(watermarkStringFragment[_keySequence[j]]) ^ _sectors[j].EtaK)) // если 1/1 или 0/0 то двигаем
                        {
                            if (segmentsInfo[i].firstInside) {
                                newCoords[i + 1] = MoveCoordinate(mapboxPixelCoordinates[i + 1 - difference].x,
                                    mapboxPixelCoordinates[i + 1 - difference].y, segmentsInfo[i].dx, segmentsInfo[i].dy, extent);
                                // производим добавление новой точки только для firstInside
                                if (segmentsInfo.ElementAtOrDefault(i + 1).crosses)
                                {
                                    newCoords.Insert(i + 2, mapboxPixelCoordinates[i + 1 - difference]);
                                    segmentsInfo.Insert(i + 1, (false, 0, 0, false));
                                    difference++;
                                }
                            }
                            else {
                                newCoords[i] = MoveCoordinate(mapboxPixelCoordinates[i - difference].x,
                                    mapboxPixelCoordinates[i - difference].y, segmentsInfo[i].dx, segmentsInfo[i].dy, extent);
                            }
                        }

                        break;
                    }
                }
            }
        }

        return newCoords;
    }

    /// <summary>
    /// Отвечает за сдвиг одной точки за границы буфера
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="dx"></param>
    /// <param name="dy"></param>
    /// <param name="extent"></param>
    /// <returns></returns>
    private (int newX, int newY) MoveCoordinate(int x, int y, int dx, int dy, int extent)
    {
        
        int newX, newY;

        if (dx == 0)
        {
            if (y < 0)
            {
                newY = -_buffer - 1;
            }
            //else if (y > extent)
            else
            {
                newY = extent + _buffer + 1;
            }
            newX = x;

            return (newX, newY);
        }
        else if (dy == 0)
        {
            if (x < 0)
            {
                newX = -_buffer - 1;
            }
            //else if (x > extent)
            else
            {
                newX = extent + _buffer + 1;
            }
            newY = y;

            return (newX, newY);
        }

        var x0 = x - dx;
        var y0 = y - dy;
        var gcd = FindGCD(dx, dy); // ищет для абсолютных значений чисел
        var dxQuantum = dx / gcd; // не могут быть нулями, ведь мы уже рассмотрели эти случаи выше
        var dyQuantum = dy / gcd;
        //var xMultiplier = 0;
        //var yMultiplier = 0;
        var multiplier = 1;

        var changeByX = false;

        var x1 = dx > 0? extent + _buffer : -_buffer;
        var y1 = (double)dy / dx * (x1 - x0) + y0;
        if (y1 >= -_buffer && y1 <= extent + _buffer) // а что по знакам? Они правильные? (вроде должны быть правильными,
                                                      // тк dxQuantum и x1 - x совпадают по знаку)
        {
            multiplier = (int)Math.Ceiling((double)(x1 - x) / dxQuantum); // если цейлируется целое число,
                                                                          // multiplier по идее нужно еще увеличить на 1
            changeByX = true;
        }
        else if (y1 < -_buffer)
        {
            // если y1 пересекает нижнюю границу буфера, то и dyQuantum < 0 (т. к. gcd > 0, а dy < 0)
            multiplier = (int)Math.Ceiling((double)(-_buffer - y) / dyQuantum); // координата y тоже меньше нуля
        }
        else // if (y1 > extent + _buffer)
        {
            // если y1 пересекает ВЕРХНЮЮ границу буфера, то и dyQuantum > 0 (т. к. gcd > 0 и dy > 0)
            multiplier = (int)Math.Ceiling((double)(extent + _buffer - y) / dyQuantum); // координата y больше нуля
        }
        // В обоих случаях результат операции [(граница буфера - текущая координата)/квантованная координата]
        // неотрицательный. Поэтому Ceil в обоих случаях, а также в случае с X. И везде multiplier неотрицательный.

        if (multiplier == 0) 
            multiplier = 1; // если multiplier получился нулевым, значит внешняя точка отрезка лежит прямо на границе буфера.
                            // Тогда просто сдвигаем на минимальные доступные значения, то есть dxQuantum и dyQuantum
        newX = x + dxQuantum * multiplier;
        newY = y + dyQuantum * multiplier;
        // !!! надо бахнуть проверку: если точка находится НА буфере, то multiplier++

        if ((changeByX && newX >= -_buffer && newX <= extent + _buffer) || (!changeByX && newY >= -_buffer && newY <= extent + _buffer))
        {
            newX += dxQuantum;
            newY += dyQuantum;
        }

        return (newX, newY);

        //if (x < 0)
        //{
        //    xMultiplier = (int)Math.Ceiling((double)((-_buffer - x) / dxQuantum)); // проверить знаки, они правильные?
        //}
        //else if (x > extent)
        //{
        //    xMultiplier = (int)Math.Ceiling((double)((_buffer + extent - x) / dxQuantum)); // проверить знаки, они правильные?
        //}
        //else // если x в пределах нуля и экстента, но набирает скорость быстрее, чем y
        //{
        //    // короче сложно всё это.
        //}
    }

    private int FindGCD(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    // проверка координаты, находится ли она за пределами границ тайла
    private bool CheckIfCoordinateIsOutside((int x, int y) coordinate, uint extent)
    {
        if (coordinate.x >= extent || coordinate.y >= extent || coordinate.x <= 0 || coordinate.y <= 0)
            return true;

        return false;
    }

    private static IEnumerable<uint> Encode(CoordinateSequence sequence, 
        MvtWatermark.NtsArtefacts.TileGeometryTransform tgt,
        ref int currentX, ref int currentY,
        bool ring = false, bool ccw = false)
    {
        // how many parameters for LineTo command
        var count = sequence.Count;

        // if we have a ring we need to check orientation
        if (ring)
        {
            if (ccw != NetTopologySuite.Algorithm.Orientation.IsCCW(sequence))
            {
                sequence = sequence.Copy();
                CoordinateSequences.Reverse(sequence);
            }
        }
        var encoded = new List<uint>();

        // Start point
        encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
        var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        encoded.Add(GenerateParameterInteger(position.x));
        encoded.Add(GenerateParameterInteger(position.y));

        // Add LineTo command (stub)
        var lineToCount = 0;
        encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, lineToCount));
        for (var i = 1; i < count; i++)
        {
            position = tgt.Transform(sequence, i, ref currentX, ref currentY);

            if (position.x != 0 || position.y != 0)
            {
                encoded.Add(GenerateParameterInteger(position.x));
                encoded.Add(GenerateParameterInteger(position.y));
                lineToCount++;
            }
        }
        if (lineToCount > 0)
            encoded[3] = GenerateCommandInteger(MapboxCommandType.LineTo, lineToCount);

        // Validate encoded data
        if (ring)
        {
            // A ring has 1 MoveTo and 1 LineTo command.
            // A ring is only valid if we have at least 3 points, otherwise collapse
            if (encoded.Count - 2 >= 6)
                encoded.Add(GenerateCommandInteger(MapboxCommandType.ClosePath, 1));
            else
                encoded.Clear();
        }
        else
        {
            // A line has 1 MoveTo and 1 LineTo command.
            // A line is valid if it has at least 2 points
            if (encoded.Count - 2 < 4)
                encoded.Clear();
        }

        return encoded;
    }

    /// <summary>
    /// Generates a command integer.
    /// </summary>
    private static uint GenerateCommandInteger(MapboxCommandType command, int count)
    { // CommandInteger = (id & 0x7) | (count << 3)
        return (uint)(((int)command & 0x7) | (count << 3));
    }

    /// <summary>
    /// Generates a parameter integer.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static uint GenerateParameterInteger(int value)
    { // ParameterInteger = (value << 1) ^ (value >> 31)
        return (uint)((value << 1) ^ (value >> 31));
    }

    /// <summary>
    /// Checks to see if a geometries envelope is greater than 1 square pixel in size for a specified zoom leve.
    /// </summary>
    /// <param name="polygon">Polygon to test.</param>
    /// <param name="zoom">Zoom level </param>
    /// <returns></returns>
    private static bool IsGreaterThanOnePixelOfTile(Geometry polygon, int zoom)
    {
        (double x1, double y1) = WebMercatorHandler.MetersToPixels(WebMercatorHandler.LatLonToMeters(polygon.EnvelopeInternal.MinY, polygon.EnvelopeInternal.MinX), zoom, 512);
        (double x2, double y2) = WebMercatorHandler.MetersToPixels(WebMercatorHandler.LatLonToMeters(polygon.EnvelopeInternal.MaxY, polygon.EnvelopeInternal.MaxX), zoom, 512);

        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);

        //Both must be greater than 0, and atleast one of them needs to be larger than 1. 
        return dx > 0 && dy > 0 && (dx > 1 || dy > 1);
    }
}
