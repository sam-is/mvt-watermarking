using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using MvtWatermark.DebugClasses;
using MvtWatermark.NoDistortionWatermark;
using MvtWatermark.NoDistortionWatermark.Auxiliary;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles.Tiles.WebMercator;

//namespace MvtWatermark.NoDistortionWatermark
namespace NetTopologySuite.IO.VectorTiles.Mapbox.Watermarking
{
    // see: https://github.com/mapbox/vector-tile-spec/tree/master/2.1
    public static class MapboxTileWriterWM
    {
        private struct LastCommandInfo
        {
            public int Index { get; set; }

            public MapboxCommandType Type { get; set; }
        }

        private static bool s_hasSuccessfullyEmbeded = false;

        /// <summary>
        /// Creates and return Dictionary in format (tileId: TileWithEmbededWatermark) from VectorTileTree. 
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="WatermarkString">Watermark BitArray</param>
        /// <param name="key">key for generating Sk</param>
        /// <param name="options">All the parameters to embed the watermark</param>
        /// <param name="extent">The extent.</param>
        /// <remarks>The "Embed" method in NoDistortionWatermark then transforms it into VectorTileTree</remarks>
        public static Dictionary<ulong, Tile> WriteWM(this VectorTileTree tree, BitArray WatermarkString, 
            short firstHalfOfTheKey, NoDistortionWatermarkOptions options, uint extent = 4096)
        {
            var result = new Dictionary<ulong, Tile>();

            // узнать как оно обходит, в каком порядке. Это важно для порядка деления message по тайлам.
            foreach (var tileIndex in tree) 
            {
                result.Add(tileIndex, tree[tileIndex].WriteWM(WatermarkString, firstHalfOfTheKey, tileIndex, options, extent)); 
            }

            return result;
        }

        /// <summary>
        /// Возвращает мапбоксовый тайл для последующего добавления его в словарь (tileId : Mapbox.Tile)
        /// </summary>
        /// <param name="vectorTile">The vector tile.</param>
        /// <param name="WatermarkString">Watermark BitArray</param>
        /// <param name="key">key for generating Sk</param>
        /// <param name="tileId">а это наверное не надо передавать, tileId есть в свойствах vectorTile</param>
        /// <param name="options">All the parameters to embed the watermark</param>
        /// <param name="extent">The extent.</param>
        /// <param name="idAttributeName">The name of an attribute property to use as the ID for the Feature. Vector tile feature ID's should be integer or ulong numbers.</param>
        public static Tile WriteWM(this VectorTile vectorTile, BitArray WatermarkString, short firstHalfOfTheKey,
            ulong tileId, NoDistortionWatermarkOptions options, uint extent = 4096, string idAttributeName = "id")
        {
            var WatermarkInt = WatermarkTransform.getIntFromBitArray(WatermarkString); // Фрагмент ЦВЗ в int

            int key = firstHalfOfTheKey;
            key = (key << 16) + (short)vectorTile.TileId;

            var keySequence = SequenceGenerator.GenerateSequence(key, options.Nb, options.D, options.M);

            var tile = new Tiles.Tile(vectorTile.TileId);
            var tgt = new TileGeometryTransform(tile, extent);

            var mapboxTile = new Mapbox.Tile();
            foreach (var localLayer in vectorTile.Layers)
            {
                var layer = new Mapbox.Tile.Layer { Version = 2, Name = localLayer.Name, Extent = extent };

                var keys = new Dictionary<string, uint>();
                var values = new Dictionary<Tile.Value, uint>();

                int embedingIndex = 0; // для работы с Lf

                foreach (var localLayerFeature in localLayer.Features)
                {
                    var feature = new Mapbox.Tile.Feature();

                    // Encode geometry
                    switch (localLayerFeature.Geometry)
                    {
                        case IPuntal puntal:
                            feature.Type = Tile.GeomType.Point;
                            feature.Geometry.AddRange(Encode(puntal, tgt));
                            break;
                        case ILineal lineal:
                            feature.Type = Tile.GeomType.LineString;

                            // для реализации параметра Lf
                            if (embedingIndex < options.Lf) 
                            {
                                feature.Geometry.AddRange(Encode(lineal, tgt, WatermarkInt, options, keySequence)); // ЦВЗ только в лайнстринги запихивается
                                if (s_hasSuccessfullyEmbeded) // для реализации параметра Lf
                                    embedingIndex++;
                            }
                            else
                            {
                                feature.Geometry.AddRange(Encode(lineal, tgt));
                            }
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
                    if (id != null && ulong.TryParse(id.ToString(), out ulong idVal))
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
        /// <param name="stream"></param>
        /// <param name="extent"></param>
        /// <param name="idAttributeName"></param>
        /// <returns></returns>
        public static Mapbox.Tile GetMapboxTileFromVectorTile(this VectorTile vectorTile, uint extent = 4096, string idAttributeName = "id")
        {
            var tile = new Tiles.Tile(vectorTile.TileId);
            var tgt = new TileGeometryTransform(tile, extent);

            var mapboxTile = new Mapbox.Tile();
            foreach (var localLayer in vectorTile.Layers)
            {
                var layer = new Mapbox.Tile.Layer { Version = 2, Name = localLayer.Name, Extent = extent };

                var keys = new Dictionary<string, uint>();
                var values = new Dictionary<Tile.Value, uint>();

                foreach (var localLayerFeature in localLayer.Features)
                {
                    var feature = new Mapbox.Tile.Feature();

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
                    if (id != null && ulong.TryParse(id.ToString(), out ulong idVal))
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

                tags.Add(keys.AddOrGet(key));
                tags.Add(values.AddOrGet(tileValue));
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

        private static IEnumerable<uint> Encode(IPuntal puntal, TileGeometryTransform tgt)
        {
            const int CoordinateIndex = 0;

            var geometry = (Geometry)puntal;
            int currentX = 0, currentY = 0;

            var parameters = new List<uint>();
            for (int i = 0; i < geometry.NumGeometries; i++)
            {
                var point = (Point)geometry.GetGeometryN(i);
                (int x, int y) = tgt.Transform(point.CoordinateSequence, CoordinateIndex, ref currentX, ref currentY);
                if (i == 0 || x > 0 || y > 0)
                {
                    parameters.Add(GenerateParameterInteger(x));
                    parameters.Add(GenerateParameterInteger(y));
                }
            }

            // Return result
            yield return GenerateCommandInteger(MapboxCommandType.MoveTo, parameters.Count / 2);
            foreach (uint parameter in parameters)
                yield return parameter;

        }

        private static IEnumerable<uint> Encode(ILineal lineal, TileGeometryTransform tgt)
        {
            var geometry = (Geometry)lineal;
            int currentX = 0, currentY = 0;
            for (int i = 0; i < geometry.NumGeometries; i++)
            {
                var lineString = (LineString)geometry.GetGeometryN(i);
                foreach (uint encoded in Encode(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, false))
                    yield return encoded;
            }
        }

        /// <summary>
        /// Кодирует LineString в MVT
        /// </summary>
        /// <param name="lineal"></param>
        /// <param name="tgt"></param>
        /// <param name="WatermarkInt"></param>
        /// <param name="options"></param>
        /// <param name="KeySequence"></param>
        /// <returns></returns>
        private static IEnumerable<uint> Encode(ILineal lineal, TileGeometryTransform tgt, int WatermarkInt,
            NoDistortionWatermarkOptions options, int[] KeySequence)
        // фрагмент ЦВЗ для каждого тайла надо определять как-то
        {
            var geometry = (Geometry)lineal;
            int currentX = 0, currentY = 0;

            //Console.WriteLine("Проверияем тип нетипичной конструкции"); // отладка

            switch (options.AtypicalEncodingType)
            {
                case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt:
                    //Console.WriteLine("MtLtLt"); // отладка
                    for (int i = 0; i < geometry.NumGeometries; i++)
                    {
                        var lineString = (LineString)geometry.GetGeometryN(i);
                        foreach (uint encoded in EncodeWithWatermarkMtLtLt(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, WatermarkInt,
                            options, KeySequence))
                            yield return encoded;
                    }
                    break;
                case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtMt:
                    //Console.WriteLine("MtLtMt"); // отладка
                    for (int i = 0; i < geometry.NumGeometries; i++)
                    {
                        var lineString = (LineString)geometry.GetGeometryN(i);
                        foreach (uint encoded in EncodeWithWatermarkMtLtMt(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, WatermarkInt,
                            options, KeySequence))
                            yield return encoded;
                    }
                    break;
                case NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands:
                    //Console.WriteLine("MtLtMt"); // отладка
                    for (int i = 0; i < geometry.NumGeometries; i++)
                    {
                        var lineString = (LineString)geometry.GetGeometryN(i);
                        foreach (uint encoded in EncodeWithWatermarkNLt(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, WatermarkInt,
                            options, KeySequence))
                            yield return encoded;
                    }
                    break;
                    //throw new NotImplementedException();
            }
        }

        private static IEnumerable<uint> Encode(IPolygonal polygonal, TileGeometryTransform tgt, int zoom)
        {
            var geometry = (Geometry)polygonal;

            //Test the whole polygon geometry is larger than a single pixel.
            if (IsGreaterThanOnePixelOfTile(geometry, zoom))
            {
                int currentX = 0, currentY = 0;
                for (int i = 0; i < geometry.NumGeometries; i++)
                {
                    var polygon = (Polygon)geometry.GetGeometryN(i);

                    //Test that individual polygons are larger than a single pixel.
                    if (!IsGreaterThanOnePixelOfTile(polygon, zoom))
                        continue;

                    foreach (uint encoded in Encode(polygon.Shell.CoordinateSequence, tgt, ref currentX, ref currentY, true, false))
                        yield return encoded;
                    foreach (var hole in polygon.InteriorRings)
                    {
                        foreach (uint encoded in Encode(hole.CoordinateSequence, tgt, ref currentX, ref currentY, true, true))
                            yield return encoded;
                    }
                }
            }
        }

        /// <summary>
        /// Encodes geometry with watermark (Стойкий ЦВЗ, алгоритм встраивания в половину )
        /// </summary>
        private static IEnumerable<uint> EncodeWithWatermarkMtLtLt(CoordinateSequence sequence, TileGeometryTransform tgt,
            ref int currentX, ref int currentY, int WatermarkInt, NoDistortionWatermarkOptions options, int[] keySequence)
        {
            // how many parameters for LineTo command
            int count = sequence.Count;
            var encoded = new List<uint>();

            //Console.WriteLine($"currentX = {currentX}, currentY = {currentY}"); // отладка
            //Console.WriteLine($"sequence count = {count}"); // отладка
            //Console.WriteLine($"sequence = {sequence}"); // отладка

            var X = currentX; var Y = currentY;

            // весь этот кусок кода нужен для того, чтобы посчитать количество реальных сегментов
            var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
            int realSegments = 0;
            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                if (position.x != 0 || position.y != 0)
                {
                    realSegments++;
                }
            }

            currentX = X; currentY = Y;

            if (realSegments < options.D)
            {
                s_hasSuccessfullyEmbeded = false; // для реализации параметра Lf

                return Encode(sequence, tgt, ref currentX, ref currentY);
            }

            //Console.WriteLine($"Элементарных сегментов: {options.D}"); // отладка
            //Console.WriteLine($"Реальных сегментов: {realSegments}"); // отладка


            if (options.SecondHalfOfLineStringIsUsed)
            {
                sequence = sequence.Reversed();
            }


            int realSegmentsInOneElemSegment = realSegments / options.D;

            //Console.WriteLine($"Реальных сегментов в одном элементарном: {realSegmentsInOneElemSegment}"); // отладка

            var LsArray = GenerateSequenceLs(realSegmentsInOneElemSegment, options.Ls);

            int lastLineToCount = 0;
            int currentRealSegment = 0;
            int lastLineToCommand; // индекс последнего LineTo CommandInteger

            // Стартовая команда: первый MoveTo
            encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
            position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
            encoded.Add(GenerateParameterInteger(position.x));
            encoded.Add(GenerateParameterInteger(position.y));

            int encodedIndex = 2; // под индексами 0 - 2 добавили MoveTo и параметры, остановка на втором параметре MoveTo

            encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount));
            encodedIndex++; // encodedIndex = 3
            lastLineToCommand = encodedIndex;

            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                if (position.x != 0 || position.y != 0) 
                {
                    var currentElementarySegment = currentRealSegment / realSegmentsInOneElemSegment;
                    var realSegmentIndexInElementary = currentRealSegment - realSegmentsInOneElemSegment * currentElementarySegment;

                    if (currentElementarySegment < keySequence.Length
                        // currentRealSegment на первом шаге = 0, currentElementarySegment тоже = 0
                        && keySequence[currentElementarySegment] == WatermarkInt 
                        && LsArray[realSegmentIndexInElementary] == 1) 
                    {
                        lastLineToCount = 1;
                        encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
                        encoded.Add(GenerateParameterInteger(position.x));
                        encoded.Add(GenerateParameterInteger(position.y));
                        encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount));
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
                    encoded[lastLineToCommand] = GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount);

                    currentRealSegment++;
                }
            }

            // A line has 1 MoveTo and 1 LineTo command.
            // A line is valid if it has at least 2 points
            if (encoded.Count - 2 < 4)
            {
                encoded.Clear();
            }

            s_hasSuccessfullyEmbeded = true; // для реализации параметра Lf

            return encoded;
        }

        // Не работает нормально, пока не используем
        private static IEnumerable<uint> EncodeWithWatermarkMtLtMt(CoordinateSequence sequence, TileGeometryTransform tgt,
            ref int currentX, ref int currentY, int WatermarkInt, NoDistortionWatermarkOptions options, int[] keySequence)
        {
            // how many parameters for LineTo command
            int count = sequence.Count;
            var encoded = new List<uint>();

            Console.WriteLine($"currentX = {currentX}, currentY = {currentY}"); // отладка
            Console.WriteLine($"sequence count = {count}"); // отладка
            Console.WriteLine($"sequence = {sequence}"); // отладка

            var X = currentX; var Y = currentY;

            // весь этот кусок кода нужен для того, чтобы посчитать количество реальных сегментов
            var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
            int realSegments = 0;
            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                if (position.x != 0 || position.y != 0)
                {
                    realSegments++;
                }
            }

            currentX = X; currentY = Y;

            if (realSegments < options.D)
            {
                return Encode(sequence, tgt, ref currentX, ref currentY);
                //throw new Exception("Элементарных сегментов больше, чем реальных. Встраивание невозможно.");
            }

            Console.WriteLine($"Элементарных сегментов: {options.D}"); // отладка
            Console.WriteLine($"Реальных сегментов: {realSegments}"); // отладка

            int realSegmentsInOneElemSegment = realSegments / options.D;

            Console.WriteLine($"Реальных сегментов в одном элементарном: {realSegmentsInOneElemSegment}"); // отладка

            var LsArray = GenerateSequenceLs(realSegmentsInOneElemSegment, options.Ls);

            int encodedIndex = 0; // под индексами 0 - 2 добавили MoveTo и параметры, остановка на втором параметре MoveTo

            int lastLineToCount = 0;
            int currentRealSegment = 0;
            int currentElementarySegment = 0;

            LastCommandInfo lastCommand; // = new LastCommandInfo { Index = 0, Type = MapboxCommandType.MoveTo }; // индекс последней команды

            encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
            position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
            encoded.Add(GenerateParameterInteger(position.x));
            encoded.Add(GenerateParameterInteger(position.y));

            if (!(keySequence[currentElementarySegment] == WatermarkInt && LsArray[0] == 1))
            {
                encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, 1));
                encodedIndex = 3;
                lastCommand = new LastCommandInfo { Index = encodedIndex, Type = MapboxCommandType.LineTo };
            }
            else
            {
                encodedIndex = 2;
                lastCommand = new LastCommandInfo { Index = encodedIndex, Type = MapboxCommandType.MoveTo };
            }

            // 0-й отсчёт - это первый MoveTo
            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                if (position.x != 0 || position.y != 0)
                {
                    currentElementarySegment = currentRealSegment / realSegmentsInOneElemSegment;

                    var realSegmentIndexInElementary = currentRealSegment - realSegmentsInOneElemSegment * currentElementarySegment;

                    if (currentElementarySegment < keySequence.Length
                        // currentRealSegment на первом шаге = 0, currentElementarySegment тоже = 0
                        && keySequence[currentElementarySegment] == WatermarkInt // тут проблема с индексами (уже нет)
                        && LsArray[realSegmentIndexInElementary] == 1)
                    {
                        if (lastCommand.Index == encodedIndex)
                        {
                            if (lastCommand.Type == MapboxCommandType.LineTo) {
                                encoded[lastCommand.Index] = GenerateCommandInteger(MapboxCommandType.MoveTo, 1);
                            }
                            else if (lastCommand.Type == MapboxCommandType.MoveTo)
                            {
                                encoded[lastCommand.Index] = GenerateCommandInteger(MapboxCommandType.MoveTo, 2);
                            }
                            encodedIndex += 9;
                        }
                        else
                        {
                            encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
                            encodedIndex += 10;
                        }

                        encoded.Add(GenerateParameterInteger(position.x));
                        encoded.Add(GenerateParameterInteger(position.y));

                        encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, 1));
                        position = tgt.Transform(sequence, i - 1, ref currentX, ref currentY);
                        encoded.Add(GenerateParameterInteger(position.x));
                        encoded.Add(GenerateParameterInteger(position.y));

                        position = tgt.Transform(sequence, i, ref currentX, ref currentY);
                        encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
                        encoded.Add(GenerateParameterInteger(position.x));
                        encoded.Add(GenerateParameterInteger(position.y));

                        encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, 1));

                        lastLineToCount = 0; // Сколько параметров для последнего LineTo

                        
                        lastCommand.Index = encodedIndex;
                        lastCommand.Type = MapboxCommandType.LineTo;
                    }
                    else
                    {
                        encoded.Add(GenerateParameterInteger(position.x));
                        encoded.Add(GenerateParameterInteger(position.y));
                        encodedIndex += 2;

                        lastLineToCount++;
                        encoded[lastCommand.Index] = GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount); // это нужно делать 1 раз
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
        /// <param name="WatermarkInt"></param>
        /// <param name="options"></param>
        /// <param name="keySequence"></param>
        /// <returns></returns>
        private static IEnumerable<uint> EncodeWithWatermarkNLt(CoordinateSequence sequence, TileGeometryTransform tgt,
            ref int currentX, ref int currentY, int WatermarkInt, NoDistortionWatermarkOptions options, int[] keySequence)
        {
            // how many parameters for LineTo command
            int count = sequence.Count;
            var encoded = new List<uint>();

            //Console.WriteLine($"currentX = {currentX}, currentY = {currentY}"); // отладка
            //Console.WriteLine($"sequence count = {count}"); // отладка
            //Console.WriteLine($"sequence = {sequence}"); // отладка

            var X = currentX; var Y = currentY;

            var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
            int realSegments = 0;
            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                if (position.x != 0 || position.y != 0)
                {
                    realSegments++;
                }
            }

            currentX = X; currentY = Y;

            if (realSegments < options.D)
            {
                s_hasSuccessfullyEmbeded = false; // для реализации параметра Lf

                return Encode(sequence, tgt, ref currentX, ref currentY);
            }

            //Console.WriteLine($"Элементарных сегментов: {options.D}"); // отладка
            //Console.WriteLine($"Реальных сегментов: {realSegments}"); // отладка


            if (options.SecondHalfOfLineStringIsUsed)
            {
                sequence = sequence.Reversed();
            }


            int realSegmentsInOneElemSegment = realSegments / options.D;

            //Console.WriteLine($"Реальных сегментов в одном элементарном: {realSegmentsInOneElemSegment}"); // отладка

            var LsArray = GenerateSequenceLs(realSegmentsInOneElemSegment, options.Ls);

            int encodedIndex = 0; // под индексами 0 - 2 добавили MoveTo и параметры, остановка на втором параметре MoveTo

            int lastLineToCount = 0;
            int currentRealSegment = 0;
            int currentElementarySegment = 0;

            LastCommandInfo lastCommand; // = new LastCommandInfo { Index = 0, Type = MapboxCommandType.MoveTo }; // индекс последней команды

            encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
            position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
            encoded.Add(GenerateParameterInteger(position.x));
            encoded.Add(GenerateParameterInteger(position.y));

            encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, 1));
            encodedIndex = 3;
            lastCommand = new LastCommandInfo { Index = encodedIndex, Type = MapboxCommandType.LineTo };

            // 0-й отсчёт - это первый MoveTo
            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                if (position.x != 0 || position.y != 0)
                {
                    currentElementarySegment = currentRealSegment / realSegmentsInOneElemSegment;

                    var realSegmentIndexInElementary = currentRealSegment - realSegmentsInOneElemSegment * currentElementarySegment;

                    if (currentElementarySegment < keySequence.Length
                        // currentRealSegment на первом шаге = 0, currentElementarySegment тоже = 0
                        && keySequence[currentElementarySegment] == WatermarkInt // тут проблема с индексами (уже нет)
                        && LsArray[realSegmentIndexInElementary] == 1)
                    {
                        if (lastCommand.Index == encodedIndex)
                        {
                            encoded[lastCommand.Index] = GenerateCommandInteger(MapboxCommandType.MoveTo, 1);
                            encodedIndex += 3;
                        }
                        else
                        {
                            encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
                            encodedIndex += 4;
                        }

                        encoded.Add(GenerateParameterInteger(0));
                        encoded.Add(GenerateParameterInteger(0));

                        encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, 1));

                        lastLineToCount = 0; // Сколько команд для последнего LineTo

                        lastCommand.Index = encodedIndex;
                        lastCommand.Type = MapboxCommandType.LineTo;
                    }
                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));
                    encodedIndex += 2;

                    lastLineToCount++;
                    encoded[lastCommand.Index] = GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount); // это нужно делать 1 раз

                    currentRealSegment++;
                }
            }

            // A line has 1 MoveTo and 1 LineTo command.
            // A line is valid if it has at least 2 points
            if (encoded.Count - 2 < 4)
            {
                encoded.Clear();
            }

            s_hasSuccessfullyEmbeded = true; // для реализации параметра Lf

            return encoded;
        }


        private static IEnumerable<uint> Encode(CoordinateSequence sequence, TileGeometryTransform tgt,
            ref int currentX, ref int currentY,
            bool ring = false, bool ccw = false)
        {
            // how many parameters for LineTo command
            int count = sequence.Count;

            // if we have a ring we need to check orientation
            if (ring)
            {
                if (ccw != Algorithm.Orientation.IsCCW(sequence))
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
            int lineToCount = 0;
            encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, lineToCount));
            for (int i = 1; i < count; i++)
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

        /// <summary>
        /// Генерирует количество Ls (реальных сегментов в одном элементарном, в которые втраивается НТГ) на основе ключа LsKey;
        /// Генерирует массив интов с указанием, куда именно вставлять НТГ
        /// </summary>
        /// <param name="realSegmentsInOneElemSegment"></param>
        /// <param name="LsKey"></param>
        /// <returns></returns>
        private static List<int> OLDGenerateSequenceLs(int realSegmentsInOneElemSegment, int LsKey)
        {
            var random = new Random(LsKey);

            var Ls = random.Next(1, realSegmentsInOneElemSegment);

            var resultArr = new List<int>(realSegmentsInOneElemSegment);
            int randomIndex;

            for (var i = 0; i < realSegmentsInOneElemSegment; i++)
            {
                resultArr.Add(-1);
            }

            for (var i = 0; i < Ls; i++)
            {
                do
                {
                    randomIndex = random.Next(0, realSegmentsInOneElemSegment - 1);
                } while (resultArr[randomIndex] != -1);
                resultArr[randomIndex] = 1;
            }

            //Console.WriteLine($"Отладка ||| Массив Ls: {ConsoleWriter.GetIEnumerableStr(resultArr)}"); // отладка

            return resultArr;
        }

        private static List<int> GenerateSequenceLs(int realSegmentsInOneElemSegment, int Ls)
        {
            //var random = new Random(key);
            var random = new Random(); // лучше передавать ключ

            //var Ls = random.Next(1, realSegmentsInOneElemSegment);

            var resultArr = new List<int>(realSegmentsInOneElemSegment);
            int randomIndex;

            for (var i = 0; i < realSegmentsInOneElemSegment; i++)
            {
                resultArr.Add(0);
            }

            if (Ls < 1)
                Ls = 1;
            else if (Ls > realSegmentsInOneElemSegment)
                Ls = realSegmentsInOneElemSegment;

            for (var i = 0; i < Ls; i++)
            {
                do
                {
                    randomIndex = random.Next(0, realSegmentsInOneElemSegment);
                } while (resultArr[randomIndex] != 0);
                resultArr[randomIndex] = 1;
            }

            //Console.WriteLine($"Отладка ||| Массив Ls: {ConsoleWriter.GetIEnumerableStr(resultArr)}"); // отладка

            return resultArr;
        }
    }
}