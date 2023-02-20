using System;
using System.Collections;
using System.Collections.Generic;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using Mapbox = NetTopologySuite.IO.VectorTiles.Mapbox;
using NetTopologySuite.IO.VectorTiles.Tiles.WebMercator;
using MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts;
using System.Linq;

namespace MvtWatermark.NoDistortionWatermark.Auxiliary;

// see: https://github.com/mapbox/vector-tile-spec/tree/master/2.1
public class NonStaticMapboxTileWriterWm
{
    private struct LastCommandInfo
    {
        public int Index { get; set; }

        public Mapbox.MapboxCommandType Type { get; set; }
    }

    private bool _hasSuccessfullyEmbededIntoSingleLineString;
    private bool _hasSuccessfullyEmbededIntoSingleTile;

    /// <summary>
    /// Creates and return Dictionary in format (tileId: TileWithEmbededWatermark) from VectorTileTree. 
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="watermarkString">Watermark BitArray</param>
    /// <param name="firstHalfOfTheKey">(Int16) first half of the key for generating Sk</param>
    /// <param name="options">All the parameters to embed the watermark</param>
    /// <param name="extent">The extent.</param>
    /// <remarks>The "Embed" method in NoDistortionWatermark then transforms it into VectorTileTree</remarks>
    public Dictionary<ulong, Mapbox.Tile> WriteWm(VectorTileTree tree, BitArray message,
        short firstHalfOfTheKey, NoDistortionWatermarkOptions options, uint extent = 4096)
    {
        var result = new Dictionary<ulong, Mapbox.Tile>();

        if (message.Count < tree.Count() * options.Nb)
        {
            throw new ArgumentException("Not enough bits in the watermark message", 
                $"Bits' number: {message.Count}, minimal required bits number: {tree.Count() * options.Nb}");
        }

        _hasSuccessfullyEmbededIntoSingleTile = false;

        var watermarkString = new BitArray(message);
        var watermarkStringFragment = new BitArray(options.Nb);

        var embededMessageFiller = new BitArray(tree.Count() * options.Nb); // отладка
        var embededMessageIndex = 0; // отладка

        for (var i = 0; i < options.Nb; i++)
        {
            watermarkStringFragment[i] = watermarkString[i];
        }

        // переменные для норм сообщений в исключениях
        var tileNumber = 0; // текущий номер тайла в дереве (фактически это Dictionary, и тайлы хранятся в нём в порядке добавления)
        var currentFragmentStartIndex = 0; // индекс начала текущей подпоследовательности (фрагмента) ЦВЗ

        foreach (var tileIndex in tree)
        {
            if (_hasSuccessfullyEmbededIntoSingleTile)
            {
                for (var i = 0; i < options.Nb; i++)
                {
                    watermarkStringFragment[i] = watermarkString[i];
                }
                currentFragmentStartIndex += options.Nb;
            }

            _hasSuccessfullyEmbededIntoSingleTile = false;
            Mapbox.Tile resultTile = WriteWm(tree[tileIndex], watermarkStringFragment, firstHalfOfTheKey, tileIndex, 
                options, tileNumber, currentFragmentStartIndex, extent);

            result.Add(tileIndex, resultTile);

            if (_hasSuccessfullyEmbededIntoSingleTile)
            {
                watermarkString.RightShift(options.Nb);

                watermarkStringFragment.CopyNbBitsTo(embededMessageFiller, embededMessageIndex * options.Nb, options.Nb); // отладка
                Console.WriteLine($"В тайл успешно встроен фрагмент ЦВЗ"); // отладка
                embededMessageIndex++; // отладка
            }
            else // отладка
            {
                Console.WriteLine($"Не получилось встроить фрагмент ЦВЗ в тайл"); // отладка
            }

            tileNumber++;
        }

        var embededMessage = new BitArray(embededMessageIndex * options.Nb); // отладка
        embededMessageFiller.CopyNbBitsTo(embededMessage, 0, embededMessage.Count); // отладка

        var embededMessageString = ""; // отладка
        foreach (var elem in embededMessage) // отладка
        {
            embededMessageString += $"{elem} "; // отладка
        }
        Console.WriteLine($"Встроили: {embededMessageString}"); // отладка

        return result;
    }

    /// <summary>
    /// Возвращает мапбоксовый тайл для последующего добавления его в словарь (tileId : Mapbox.Tile)
    /// </summary>
    /// <param name="vectorTile">The vector tile.</param>
    /// <param name="watermarkString">Watermark BitArray</param>
    /// <param name="firstHalfOfTheKey">(Int16) first half of the key for generating Sk</param>
    /// <param name="tileId">а это наверное не надо передавать, tileId есть в свойствах vectorTile</param>
    /// <param name="options">All the parameters to embed the watermark</param>
    /// <param name="extent">The extent.</param>
    /// <param name="idAttributeName">The name of an attribute property to use as the ID for the Feature. Vector tile feature ID's should be integer or ulong numbers.</param>
    public Mapbox.Tile WriteWm(VectorTile vectorTile, BitArray watermarkString, short firstHalfOfTheKey,
        ulong tileId, NoDistortionWatermarkOptions options, int tileNumber, int currentFragmentStartIndex, uint extent = 4096, string idAttributeName = "id")
    {
        var watermarkInt = WatermarkTransform.GetIntFromBitArray(watermarkString); // Фрагмент ЦВЗ в int
        if (watermarkInt == 0)
            throw new ArgumentException("Одна или несколько подпоследовательностей состоят целиком из нулей, их невозможно встроить", 
                $"Индекс тайла в дереве: {tileNumber}; Индекс начала подпоследовательности: {currentFragmentStartIndex}");

        int key = firstHalfOfTheKey;
        key = (key << 16) + (short)vectorTile.TileId;

        var keySequence = SequenceGenerator.GenerateSequence(key, options.Nb, options.D, options.M);

        var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(vectorTile.TileId);
        var tgt = new TileGeometryTransform(tile, extent);

        var mapboxTile = new Mapbox.Tile();
        foreach (var localLayer in vectorTile.Layers)
        {
            var layer = new Mapbox.Tile.Layer { Version = 2, Name = localLayer.Name, Extent = extent };

            var keys = new Dictionary<string, uint>();
            var values = new Dictionary<Mapbox.Tile.Value, uint>();

            var embedingIndex = 0; // для работы с Lf

            foreach (var localLayerFeature in localLayer.Features)
            {
                var feature = new Mapbox.Tile.Feature();

                // Encode geometry
                switch (localLayerFeature.Geometry)
                {
                    case IPuntal puntal:
                        feature.Type = Mapbox.Tile.GeomType.Point;
                        feature.Geometry.AddRange(Encode(puntal, tgt));
                        break;
                    case ILineal lineal:
                        feature.Type = Mapbox.Tile.GeomType.LineString;

                        // для реализации параметра Lf
                        if (embedingIndex < options.Lf)
                        {
                            feature.Geometry.AddRange(Encode(lineal, tgt, watermarkInt, options, keySequence)); // ЦВЗ только в лайнстринги запихивается
                            if (_hasSuccessfullyEmbededIntoSingleLineString)
                            {
                                _hasSuccessfullyEmbededIntoSingleTile = true;
                                embedingIndex++;
                            }// для реализации параметра Lf
                        }
                        else
                        {
                            feature.Geometry.AddRange(Encode(lineal, tgt));
                        }
                        break;
                    case IPolygonal polygonal:
                        feature.Type = Mapbox.Tile.GeomType.Polygon;
                        feature.Geometry.AddRange(Encode(polygonal, tgt, tile.Zoom));
                        break;
                    default:
                        feature.Type = Mapbox.Tile.GeomType.Unknown;
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
    public Mapbox.Tile GetMapboxTileFromVectorTile(VectorTile vectorTile, uint extent = 4096, string idAttributeName = "id")
    {
        var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(vectorTile.TileId);
        var tgt = new TileGeometryTransform(tile, extent);

        var mapboxTile = new Mapbox.Tile();
        foreach (var localLayer in vectorTile.Layers)
        {
            var layer = new Mapbox.Tile.Layer { Version = 2, Name = localLayer.Name, Extent = extent };

            var keys = new Dictionary<string, uint>();
            var values = new Dictionary<Mapbox.Tile.Value, uint>();

            foreach (var localLayerFeature in localLayer.Features)
            {
                var feature = new Mapbox.Tile.Feature();

                // Encode geometry
                switch (localLayerFeature.Geometry)
                {
                    case IPuntal puntal:
                        feature.Type = Mapbox.Tile.GeomType.Point;
                        feature.Geometry.AddRange(Encode(puntal, tgt));
                        break;
                    case ILineal lineal:
                        feature.Type = Mapbox.Tile.GeomType.LineString;
                        feature.Geometry.AddRange(Encode(lineal, tgt));
                        break;
                    case IPolygonal polygonal:
                        feature.Type = Mapbox.Tile.GeomType.Polygon;
                        feature.Geometry.AddRange(Encode(polygonal, tgt, tile.Zoom));
                        break;
                    default:
                        feature.Type = Mapbox.Tile.GeomType.Unknown;
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

    private void AddAttributes(List<uint> tags, Dictionary<string, uint> keys,
        Dictionary<Mapbox.Tile.Value, uint> values, IAttributesTable attributes)
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

            tags.Add(keys.AddOrGet(key));
            tags.Add(values.AddOrGet(tileValue));
        }
    }

    private Mapbox.Tile.Value ToTileValue(object value)
    {
        switch (value)
        {
            case bool boolValue:
                return new Mapbox.Tile.Value { BoolValue = boolValue };

            case sbyte sbyteValue:
                return new Mapbox.Tile.Value { IntValue = sbyteValue };
            case short shortValue:
                return new Mapbox.Tile.Value { IntValue = shortValue };
            case int intValue:
                return new Mapbox.Tile.Value { IntValue = intValue };
            case long longValue:
                return new Mapbox.Tile.Value { IntValue = longValue };

            case byte byteValue:
                return new Mapbox.Tile.Value { UintValue = byteValue };
            case ushort ushortValue:
                return new Mapbox.Tile.Value { UintValue = ushortValue };
            case uint uintValue:
                return new Mapbox.Tile.Value { UintValue = uintValue };
            case ulong ulongValue:
                return new Mapbox.Tile.Value { UintValue = ulongValue };

            case double doubleValue:
                return new Mapbox.Tile.Value { DoubleValue = doubleValue };
            case float floatValue:
                return new Mapbox.Tile.Value { FloatValue = floatValue };

            case string stringValue:
                return new Mapbox.Tile.Value { StringValue = stringValue };
        }

        return null;
    }

    private IEnumerable<uint> Encode(IPuntal puntal, TileGeometryTransform tgt)
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
        yield return GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, parameters.Count / 2);
        foreach (var parameter in parameters)
            yield return parameter;

    }

    private IEnumerable<uint> Encode(ILineal lineal, TileGeometryTransform tgt)
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
    /// Кодирует LineString в MVT
    /// </summary>
    /// <param name="lineal"></param>
    /// <param name="tgt"></param>
    /// <param name="watermarkInt"></param>
    /// <param name="options"></param>
    /// <param name="keySequence"></param>
    /// <returns></returns>
    private IEnumerable<uint> Encode(ILineal lineal, TileGeometryTransform tgt, int watermarkInt,
        NoDistortionWatermarkOptions options, IReadOnlyList<int> keySequence)
    // фрагмент ЦВЗ для каждого тайла надо определять как-то
    {
        var geometry = (Geometry)lineal;
        int currentX = 0, currentY = 0;

        switch (options.AtypicalEncodingType)
        {
            case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt:
                for (var i = 0; i < geometry.NumGeometries; i++)
                {
                    var lineString = (LineString)geometry.GetGeometryN(i);
                    foreach (var encoded in EncodeWithWatermarkMtLtLt(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, watermarkInt,
                        options, keySequence))
                        yield return encoded;
                }
                break;
            case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtMt:
                throw new NotImplementedException();
            case NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands:
                for (var i = 0; i < geometry.NumGeometries; i++)
                {
                    var lineString = (LineString)geometry.GetGeometryN(i);
                    foreach (var encoded in EncodeWithWatermarkNLt(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, watermarkInt,
                        options, keySequence))
                        yield return encoded;
                }
                break;
        }
    }

    private IEnumerable<uint> Encode(IPolygonal polygonal, TileGeometryTransform tgt, int zoom)
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

    /// <summary>
    /// Encodes geometry with watermark (Стойкий ЦВЗ, алгоритм встраивания в половину )
    /// </summary>
    private IEnumerable<uint> EncodeWithWatermarkMtLtLt(CoordinateSequence sequence, TileGeometryTransform tgt,
        ref int currentX, ref int currentY, int watermarkInt, NoDistortionWatermarkOptions options, IReadOnlyList<int> keySequence)
    {
        // how many parameters for LineTo command
        var count = sequence.Count;
        var encoded = new List<uint>();

        var xHolder = currentX;
        var yHolder = currentY;

        // весь этот кусок кода нужен для того, чтобы посчитать количество реальных сегментов
        var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        var realSegments = 0;
        for (var i = 1; i < count; i++)
        {
            position = tgt.Transform(sequence, i, ref currentX, ref currentY);

            if (position.x != 0 || position.y != 0)
            {
                realSegments++;
            }
        }

        currentX = xHolder; currentY = yHolder;

        if (realSegments < options.D)
        {
            _hasSuccessfullyEmbededIntoSingleLineString = false; // для реализации параметра Lf

            return Encode(sequence, tgt, ref currentX, ref currentY);
        }

        if (options.SecondHalfOfLineStringIsUsed)
        {
            sequence = sequence.Reversed();
        }

        var realSegmentsInOneElemSegment = realSegments / options.D;

        var lsArray = GenerateSequenceLs(realSegmentsInOneElemSegment, options.Ls);

        var lastLineToCount = 0;
        var currentRealSegment = 0;

        // Стартовая команда: первый MoveTo
        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
        position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        encoded.Add(GenerateParameterInteger(position.x));
        encoded.Add(GenerateParameterInteger(position.y));

        var encodedIndex = 2; // под индексами 0 - 2 добавили MoveTo и параметры, остановка на втором параметре MoveTo

        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, lastLineToCount));
        encodedIndex++; // encodedIndex = 3
        var lastLineToCommand = encodedIndex; // индекс последнего LineTo CommandInteger

        for (var i = 1; i < count; i++)
        {
            position = tgt.Transform(sequence, i, ref currentX, ref currentY);

            if (position.x != 0 || position.y != 0)
            {
                var currentElementarySegment = currentRealSegment / realSegmentsInOneElemSegment;
                var realSegmentIndexInElementary = currentRealSegment - realSegmentsInOneElemSegment * currentElementarySegment;

                if (currentElementarySegment < keySequence.Count
                    // currentRealSegment на первом шаге = 0, currentElementarySegment тоже = 0
                    && keySequence[currentElementarySegment] == watermarkInt
                    && lsArray[realSegmentIndexInElementary] == 1)
                {
                    lastLineToCount = 1;
                    encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));
                    encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, lastLineToCount));
                    position = tgt.Transform(sequence, i - 1, ref currentX, ref currentY);
                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));
                    position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                    encodedIndex += 6;
                    lastLineToCommand = encodedIndex - 2; // отнимаем два параметра
                }
                encoded.Add(GenerateParameterInteger(position.x));
                encoded.Add(GenerateParameterInteger(position.y));
                encodedIndex += 2;

                lastLineToCount++;
                encoded[lastLineToCommand] = GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, lastLineToCount);

                currentRealSegment++;
            }
        }

        // A line has 1 MoveTo and 1 LineTo command.
        // A line is valid if it has at least 2 points
        if (encoded.Count - 2 < 4)
        {
            encoded.Clear();
        }

        _hasSuccessfullyEmbededIntoSingleLineString = true; // для реализации параметра Lf

        return encoded;
    }

    // Не работает нормально, пока не используем
    private IEnumerable<uint> EncodeWithWatermarkMtLtMt(CoordinateSequence sequence, TileGeometryTransform tgt,
        ref int currentX, ref int currentY, int watermarkInt, NoDistortionWatermarkOptions options, IReadOnlyList<int> keySequence)
    {
        // how many parameters for LineTo command
        var count = sequence.Count;
        var encoded = new List<uint>();

        var xHolder = currentX; var yHolder = currentY;

        // весь этот кусок кода нужен для того, чтобы посчитать количество реальных сегментов
        var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        var realSegments = 0;
        for (var i = 1; i < count; i++)
        {
            position = tgt.Transform(sequence, i, ref currentX, ref currentY);

            if (position.x != 0 || position.y != 0)
            {
                realSegments++;
            }
        }

        currentX = xHolder; currentY = yHolder;

        if (realSegments < options.D)
        {
            return Encode(sequence, tgt, ref currentX, ref currentY);
            //throw new Exception("Элементарных сегментов больше, чем реальных. Встраивание невозможно.");
        }

        var realSegmentsInOneElemSegment = realSegments / options.D;

        var lsArray = GenerateSequenceLs(realSegmentsInOneElemSegment, options.Ls);

        var encodedIndex = 0; // под индексами 0 - 2 добавили MoveTo и параметры, остановка на втором параметре MoveTo

        var lastLineToCount = 0;
        var currentRealSegment = 0;
        var currentElementarySegment = 0;

        LastCommandInfo lastCommand; // = new LastCommandInfo { Index = 0, Type = Mapbox.MapboxCommandType.MoveTo }; // индекс последней команды

        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
        position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        encoded.Add(GenerateParameterInteger(position.x));
        encoded.Add(GenerateParameterInteger(position.y));

        if (!(keySequence[currentElementarySegment] == watermarkInt && lsArray[0] == 1))
        {
            encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, 1));
            encodedIndex = 3;
            lastCommand = new LastCommandInfo { Index = encodedIndex, Type = Mapbox.MapboxCommandType.LineTo };
        }
        else
        {
            encodedIndex = 2;
            lastCommand = new LastCommandInfo { Index = encodedIndex, Type = Mapbox.MapboxCommandType.MoveTo };
        }

        // 0-й отсчёт - это первый MoveTo
        for (var i = 1; i < count; i++)
        {
            position = tgt.Transform(sequence, i, ref currentX, ref currentY);

            if (position.x != 0 || position.y != 0)
            {
                currentElementarySegment = currentRealSegment / realSegmentsInOneElemSegment;

                var realSegmentIndexInElementary = currentRealSegment - realSegmentsInOneElemSegment * currentElementarySegment;

                if (currentElementarySegment < keySequence.Count
                    // currentRealSegment на первом шаге = 0, currentElementarySegment тоже = 0
                    && keySequence[currentElementarySegment] == watermarkInt // тут проблема с индексами (уже нет)
                    && lsArray[realSegmentIndexInElementary] == 1)
                {
                    if (lastCommand.Index == encodedIndex)
                    {
                        encoded[lastCommand.Index] = lastCommand.Type switch
                        {
                            Mapbox.MapboxCommandType.LineTo => GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo,
                                1),
                            Mapbox.MapboxCommandType.MoveTo => GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo,
                                2),
                            _ => encoded[lastCommand.Index]
                        };
                        encodedIndex += 9;
                    }
                    else
                    {
                        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
                        encodedIndex += 10;
                    }

                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));

                    encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, 1));
                    position = tgt.Transform(sequence, i - 1, ref currentX, ref currentY);
                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));

                    position = tgt.Transform(sequence, i, ref currentX, ref currentY);
                    encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));

                    encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, 1));

                    lastLineToCount = 0; // Сколько параметров для последнего LineTo


                    lastCommand.Index = encodedIndex;
                    lastCommand.Type = Mapbox.MapboxCommandType.LineTo;
                }
                else
                {
                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));
                    encodedIndex += 2;

                    lastLineToCount++;
                    encoded[lastCommand.Index] = GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, lastLineToCount); // это нужно делать 1 раз
                }

                currentRealSegment++;
            }
        }

        if (encodedIndex == lastCommand.Index)
        {
            encoded.RemoveAt(encodedIndex);
        }

        // A line has 1 MoveTo and 1 LineTo command.
        // A line is valid if it has at least 2 points
        if (encoded.Count - 2 < 4)
        {
            encoded.Clear();
        }

        return encoded;
    }


    /// <summary>
    /// Встраивание с нетипичной геометрией вида New LineString
    /// </summary>
    /// <param name="sequence"></param>
    /// <param name="tgt"></param>
    /// <param name="currentX"></param>
    /// <param name="currentY"></param>
    /// <param name="watermarkInt"></param>
    /// <param name="options"></param>
    /// <param name="keySequence"></param>
    /// <returns></returns>
    private IEnumerable<uint> EncodeWithWatermarkNLt(CoordinateSequence sequence, TileGeometryTransform tgt,
        ref int currentX, ref int currentY, int watermarkInt, NoDistortionWatermarkOptions options, IReadOnlyList<int> keySequence)
    {
        // how many parameters for LineTo command
        var count = sequence.Count;
        var encoded = new List<uint>();

        var xHolder = currentX;
        var yHolder = currentY;

        var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        var realSegments = 0;
        for (var i = 1; i < count; i++)
        {
            position = tgt.Transform(sequence, i, ref currentX, ref currentY);

            if (position.x != 0 || position.y != 0)
            {
                realSegments++;
            }
        }

        currentX = xHolder; currentY = yHolder;

        if (realSegments < options.D)
        {
            _hasSuccessfullyEmbededIntoSingleLineString = false; // для реализации параметра Lf

            return Encode(sequence, tgt, ref currentX, ref currentY);
        }

        if (options.SecondHalfOfLineStringIsUsed)
        {
            sequence = sequence.Reversed();
        }


        var realSegmentsInOneElemSegment = realSegments / options.D;

        var lsArray = GenerateSequenceLs(realSegmentsInOneElemSegment, options.Ls);

        var encodedIndex = 0; // под индексами 0 - 2 добавили MoveTo и параметры, остановка на втором параметре MoveTo

        var lastLineToCount = 0;
        var currentRealSegment = 0;

        LastCommandInfo lastCommand; // = new LastCommandInfo { Index = 0, Type = Mapbox.MapboxCommandType.MoveTo }; // индекс последней команды

        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
        position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        encoded.Add(GenerateParameterInteger(position.x));
        encoded.Add(GenerateParameterInteger(position.y));

        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, 1));
        encodedIndex = 3;
        lastCommand = new LastCommandInfo { Index = encodedIndex, Type = Mapbox.MapboxCommandType.LineTo };

        // 0-й отсчёт - это первый MoveTo
        for (var i = 1; i < count; i++)
        {
            position = tgt.Transform(sequence, i, ref currentX, ref currentY);

            if (position.x != 0 || position.y != 0)
            {
                var currentElementarySegment = currentRealSegment / realSegmentsInOneElemSegment;

                var realSegmentIndexInElementary = currentRealSegment - realSegmentsInOneElemSegment * currentElementarySegment;

                if (currentElementarySegment < keySequence.Count
                    // currentRealSegment на первом шаге = 0, currentElementarySegment тоже = 0
                    && keySequence[currentElementarySegment] == watermarkInt // тут проблема с индексами (уже нет)
                    && lsArray[realSegmentIndexInElementary] == 1)
                {
                    if (lastCommand.Index == encodedIndex)
                    {
                        encoded[lastCommand.Index] = GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1);
                        encodedIndex += 3;
                    }
                    else
                    {
                        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
                        encodedIndex += 4;
                    }

                    encoded.Add(GenerateParameterInteger(0));
                    encoded.Add(GenerateParameterInteger(0));

                    encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, 1));

                    lastLineToCount = 0; // Сколько команд для последнего LineTo

                    lastCommand.Index = encodedIndex;
                    lastCommand.Type = Mapbox.MapboxCommandType.LineTo;
                }
                encoded.Add(GenerateParameterInteger(position.x));
                encoded.Add(GenerateParameterInteger(position.y));
                encodedIndex += 2;

                lastLineToCount++;
                encoded[lastCommand.Index] = GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, lastLineToCount); // это нужно делать 1 раз

                currentRealSegment++;
            }
        }

        // A line has 1 MoveTo and 1 LineTo command.
        // A line is valid if it has at least 2 points
        if (encoded.Count - 2 < 4)
        {
            encoded.Clear();
        }

        _hasSuccessfullyEmbededIntoSingleLineString = true; // для реализации параметра Lf

        return encoded;
    }


    private IEnumerable<uint> Encode(CoordinateSequence sequence, TileGeometryTransform tgt,
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
        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.MoveTo, 1));
        var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
        encoded.Add(GenerateParameterInteger(position.x));
        encoded.Add(GenerateParameterInteger(position.y));

        // Add LineTo command (stub)
        var lineToCount = 0;
        encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, lineToCount));
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
            encoded[3] = GenerateCommandInteger(Mapbox.MapboxCommandType.LineTo, lineToCount);

        // Validate encoded data
        if (ring)
        {
            // A ring has 1 MoveTo and 1 LineTo command.
            // A ring is only valid if we have at least 3 points, otherwise collapse
            if (encoded.Count - 2 >= 6)
                encoded.Add(GenerateCommandInteger(Mapbox.MapboxCommandType.ClosePath, 1));
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
    private uint GenerateCommandInteger(Mapbox.MapboxCommandType command, int count)
    { // CommandInteger = (id & 0x7) | (count << 3)
        return (uint)(((int)command & 0x7) | (count << 3));
    }

    /// <summary>
    /// Generates a parameter integer.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private uint GenerateParameterInteger(int value)
    { // ParameterInteger = (value << 1) ^ (value >> 31)
        return (uint)((value << 1) ^ (value >> 31));
    }

    /// <summary>
    /// Checks to see if a geometries envelope is greater than 1 square pixel in size for a specified zoom leve.
    /// </summary>
    /// <param name="polygon">Polygon to test.</param>
    /// <param name="zoom">Zoom level </param>
    /// <returns></returns>
    private bool IsGreaterThanOnePixelOfTile(Geometry polygon, int zoom)
    {
        (var x1, var y1) = WebMercatorHandler.MetersToPixels(WebMercatorHandler.LatLonToMeters(polygon.EnvelopeInternal.MinY, polygon.EnvelopeInternal.MinX), zoom, 512);
        (var x2, var y2) = WebMercatorHandler.MetersToPixels(WebMercatorHandler.LatLonToMeters(polygon.EnvelopeInternal.MaxY, polygon.EnvelopeInternal.MaxX), zoom, 512);

        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);

        //Both must be greater than 0, and atleast one of them needs to be larger than 1. 
        return dx > 0 && dy > 0 && (dx > 1 || dy > 1);
    }

    private List<int> GenerateSequenceLs(int realSegmentsInOneElemSegment, int lsParameter)
    {
        //var random = new Random(key);
        var random = new Random(); // лучше передавать ключ

        //var lsParameter = random.Next(1, realSegmentsInOneElemSegment);

        var resultArr = new List<int>(realSegmentsInOneElemSegment);
        int randomIndex;

        for (var i = 0; i < realSegmentsInOneElemSegment; i++)
        {
            resultArr.Add(0);
        }

        if (lsParameter < 1)
            lsParameter = 1;
        else if (lsParameter > realSegmentsInOneElemSegment)
            lsParameter = realSegmentsInOneElemSegment;

        for (var i = 0; i < lsParameter; i++)
        {
            do
            {
                randomIndex = random.Next(0, realSegmentsInOneElemSegment);
            } while (resultArr[randomIndex] != 0);
            resultArr[randomIndex] = 1;
        }

        return resultArr;
    }
}