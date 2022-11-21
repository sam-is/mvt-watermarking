using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles.Tiles.WebMercator;

namespace NetTopologySuite.IO.VectorTiles.Mapbox.NoNeed
{
    // see: https://github.com/mapbox/vector-tile-spec/tree/master/2.1
    public static class MapboxTileWriterWM
    {
        /// <summary>
        /// Writes the tiles in a /z/x/y.mvt folder structure.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="path">The path.</param>
        /// <param name="extent">The extent.</param>
        /// <remarks>Replaces the files if they are already present.</remarks>
        public static Dictionary<ulong, Tile> WriteWM(this VectorTileTree tree, BitArray WatermarkString, BitArray Key, uint extent = 4096)
        {
            var result = new Dictionary<ulong, Tile>();

            foreach (var tileIndex in tree)
            {
                result.Add(tileIndex, tree[tileIndex].WriteWM(WatermarkString, Key, extent));
            }

            return result;
        }

        /// <summary>
        /// Writes the tile to the given stream.
        /// </summary>
        /// <param name="vectorTile">The vector tile.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="extent">The extent.</param>
        /// <param name="idAttributeName">The name of an attribute property to use as the ID for the Feature. Vector tile feature ID's should be integer or ulong numbers.</param>
        public static Tile WriteWM(this VectorTile vectorTile, BitArray WatermarkString, BitArray Key,
            uint extent = 4096, string idAttributeName = "id")
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
                            feature.Geometry.AddRange(Encode(lineal, tgt, WatermarkString, Key)); // ЦВЗ только в лайнстринги запихивается
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

        // Кодирует LineString в MVT
        private static IEnumerable<uint> Encode(ILineal lineal, TileGeometryTransform tgt, BitArray WatermarkString, BitArray Key)
            // фрагмент ЦВЗ для каждого тайла надо определять как-то
        {
            var geometry = (Geometry)lineal;
            int currentX = 0, currentY = 0;
            if (WatermarkString.Count != 0 && Key.Count >= WatermarkString.Count) // ЦВЗ не пустой, и ключ равной или большей длины, чем ЦВЗ
            {
                //int D = 2 * WatermarkString.Length;
                for (int i = 0; i < geometry.NumGeometries; i++)
                {
                    var lineString = (LineString)geometry.GetGeometryN(i);
                    foreach (uint encoded in EncodeWithWatermark(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, WatermarkString, Key))
                        yield return encoded;
                }
            }
            else // Иначе кодируем без ЦВЗ
            {
                for (int i = 0; i < geometry.NumGeometries; i++)
                {
                    var lineString = (LineString)geometry.GetGeometryN(i);
                    foreach (uint encoded in Encode(lineString.CoordinateSequence, tgt, ref currentX, ref currentY, false))
                        yield return encoded;
                }
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
        private static IEnumerable<uint> EncodeWithWatermark(CoordinateSequence sequence, TileGeometryTransform tgt,
            ref int currentX, ref int currentY, BitArray WatermarkString, BitArray Key)
        {
            int DElementarySegmentsCount = WatermarkString.Count * 2;
            // !!!элементарных сегментов пока что будет ровно в два раза больше, чем битов в ЦВЗ!!!

            /////////// (DElementarySegmentsCount % WatermarkString.Length) != 0
            int WatermarkInt = WatermarkTransform.getIntFromBitArray(WatermarkString);
            // главное, чтобы после xor было не 0
            if (WatermarkInt == 0 || WatermarkString.Count == 0) // лучше ошибку выбрасывать
                // Если ЦВЗ пустой или D не делится на длину ЦВЗ без остатка, вызывается простой Encode. Но пока точно делится ^^^^^
            {
                return Encode(sequence, tgt, ref currentX, ref currentY);
            }
            ///////////
            

            // how many parameters for LineTo command
            int count = sequence.Count;
            var encoded = new List<uint>();

            // Start point
            encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
            var position = tgt.Transform(sequence, 0, ref currentX, ref currentY);
            encoded.Add(GenerateParameterInteger(position.x));
            encoded.Add(GenerateParameterInteger(position.y));

            int realSegments = 0; // число реальных сегментов (Pj-1)
            // по первому кругу считаем количество реальных сегментов
            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                if (position.x != 0 || position.y != 0)
                {
                    realSegments++;
                }
            }
            if (realSegments < DElementarySegmentsCount)
            {
                throw new Exception("Элементарных сегментов больше, чем реальных. Встраивание невозможно.");
            }
            int realSegmentsInONEElemSegment = DElementarySegmentsCount / realSegments; // может, сделать это по-другому,
                                                                                        // чтобы рациональнее использовать реальные сегменты?
            //int realSegmentsInLASTElemSegment = DElementarySegmentsCount % realSegments; // а это не нужно вообще по идее

            // По второму кругу уже кодируем.
            // Уже ДОЛЖНО БЫТЬ известно, что количество бит во фрагменте ЦВЗ
            // как минимум в два раза меньше числа элементарных сегментов

            // Изменяем встраиваемый ЦВЗ с помощью ключа
            var XorWatermarkAndKey = new BitArray(WatermarkString); 
            XorWatermarkAndKey.Xor(Key);
            var readyArrayToImplement = new BitArray(WatermarkString); // Проверить, происходит ли здесь копирование.
            // !ключ должен быть длиннее или равен вотермарке, наверное. Надо проверку сделать на это!
            for (int i = XorWatermarkAndKey.Length - 1; i >= (XorWatermarkAndKey.Length - WatermarkString.Length); --i)
            {
                readyArrayToImplement[i] = XorWatermarkAndKey[i];
            }

            // Add LineTo command (stub)
            int lastLineToCount = 0;
            encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount));

            int X = currentX; // текущее положение курсора для встраивания нетипичной геометрии
            int Y = currentY;
            int currentRealSegment = 0; 
            int encodedIndex = 3; // пока остановка на самом первом LineTo, до этого под индексами 0 - 2 добавили MoveTo и параметры

            int lastLineToCommand = encodedIndex; // индекс последнего LineTo CommandInteger
            for (int i = 1; i < count; i++)
            {
                position = tgt.Transform(sequence, i, ref currentX, ref currentY);

                //!!!!!!
                if (position.x != 0 || position.y != 0) // переделать для элементарных сегментов!
                {
                    currentRealSegment++;
                    if (i < readyArrayToImplement.Count && readyArrayToImplement[i] == true
                        && currentRealSegment % realSegmentsInONEElemSegment == 1)
                    {
                        // перед одним LineTo - MoveTo в следующую точку, потому LineTo назад. А после if уже сам LineTo
                        lastLineToCount = 1;
                        encoded.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
                        encoded.Add(GenerateParameterInteger(position.x));
                        encoded.Add(GenerateParameterInteger(position.y));
                        encoded.Add(GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount)); // надо запоминать индекс последней LineTo и обновлять
                        encoded.Add(GenerateParameterInteger(X));
                        encoded.Add(GenerateParameterInteger(Y));
                        encodedIndex += 6; // 6 команд/параметров в сумме добавили в список encoded при внесении нетипичной конструкции
                        lastLineToCommand = encodedIndex - 2; // отнимаем два параметра
                    }
                    encoded.Add(GenerateParameterInteger(position.x));
                    encoded.Add(GenerateParameterInteger(position.y));
                    encodedIndex += 2;

                    X = position.x;
                    Y = position.y;
                    lastLineToCount++;
                    encoded[lastLineToCommand] = GenerateCommandInteger(MapboxCommandType.LineTo, lastLineToCount); 
                }
            }

            // Validate encoded data

            // A line has 1 MoveTo and 1 LineTo command.
            // A line is valid if it has at least 2 points
            if (encoded.Count - 2 < 4)
            {
                encoded.Clear();
            }

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

        /*
        /// <summary>
        /// Generates a move command. 
        /// </summary>
        private static void GenerateMoveTo(List<uint> geometry, int dx, int dy)
        {
            geometry.Add(GenerateCommandInteger(MapboxCommandType.MoveTo, 1));
            geometry.Add(GenerateParameterInteger(dx));
            geometry.Add(GenerateParameterInteger(dy));
        }
         
        /// <summary>
        /// Generates a close path command.
        /// </summary>
        private static void GenerateClosePath(List<uint> geometry)
        {
            geometry.Add(GenerateCommandInteger(MapboxCommandType.ClosePath, 1));
        }
         */

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
}
