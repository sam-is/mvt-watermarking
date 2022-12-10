using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using MvtWatermark.DebugClasses;
using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO.VectorTiles.Mapbox.Watermarking
{
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
            var tileDefinition = new Tiles.Tile(tileId); // tileId хранит в себе всю нужную информацию о тайле
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

        // это не нужно!
        public int ExtractWM(string path, ulong tileId, NoDistortionWatermarkOptions options, int key)
        {
            var (zoom, x, y) = Tiles.Tile.CalculateTile(tileId);
            var filePath = $"{path}/{zoom}/{x}/{y}.mvt";
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    var tile = ProtoBuf.Serializer.Deserialize<Mapbox.Tile>(fs);
                    return ExtractWM(tile, tileId, options, key);
                }
            }
            catch(Exception ex)
            {
                return -1; // тут надо исключение выбрасывать
            }
        }

        public int ExtractWM(Tile tile, ulong tileId, NoDistortionWatermarkOptions options, int key)
        {
            var rand = new Random(key + Convert.ToInt32(tileId));

            var maxBitArray = new BitArray(options.Nb, true);
            var MaxInt = WatermarkTransform.getIntFromBitArray(maxBitArray);
            var HowMuchEachValue = new int[MaxInt + 1];

            var keySequence = new int[options.D / 2];

            keySequence[0] = 0;
            HowMuchEachValue[0] = 1;
            for (int i = 1; i < options.D / 2; i++)
            {
                int value;
                do
                {
                    value = rand.Next(0, MaxInt + 1);
                } while (HowMuchEachValue[value] >= options.M);
                keySequence[i] = value;
                HowMuchEachValue[value]++;
            } // нагенерили {Sk}

            var ExtractedWatermarkIntegers = new List<int>(); // в скобочках будет Lf видимо
            var tileDefinition = new Tiles.Tile(tileId); 
            foreach (var mbTileLayer in tile.Layers)
            {
                Debug.Assert(mbTileLayer.Version == 2U);

                var tgs = new TileGeometryTransform(tileDefinition, mbTileLayer.Extent);

                int featureIndex = 0;

                foreach (var mbTileFeature in mbTileLayer.Features)
                {
                    Console.WriteLine($"текущая Фича: {featureIndex++}"); // отладка
                    int? WatermarkIntFromFeature = ExtractFromFeature(tgs, mbTileFeature, options, keySequence);
                    if (WatermarkIntFromFeature != null)
                    {
                        ExtractedWatermarkIntegers.Add(Convert.ToInt32(WatermarkIntFromFeature));
                    }
                }
            }

            // Работа с полученным списком
            var groupsWithCounts = from s in ExtractedWatermarkIntegers
                                   group s by s into g
                                   select new
                                   {
                                       Item = g.Key,
                                       Count = g.Count()
                                   };
            var groupsSorted = groupsWithCounts.OrderByDescending(g => g.Count);
            int mostFrequestWatermarkInt = groupsSorted.First().Item;

            return mostFrequestWatermarkInt;
        }

        private IFeature ReadFeature(TileGeometryTransform tgs, Tile.Layer mbTileLayer, Tile.Feature mbTileFeature, string idAttributeName)
        {
            var geometry = ReadGeometry(tgs, mbTileFeature.Type, mbTileFeature.Geometry);
            var attributes = ReadAttributeTable(mbTileFeature, mbTileLayer.Keys, mbTileLayer.Values);

            //Check to see if an id value is already captured in the attributes, if not, add it.
            if (!string.IsNullOrEmpty(idAttributeName) && !mbTileLayer.Keys.Contains(idAttributeName))
            {
                ulong id = mbTileFeature.Id;
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
        /// Извлечение из фичи, если в ней лежит лайнстринг
        /// </summary>
        /// <param name="tgs"></param>
        /// <param name="mbTileFeature"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private int? ExtractFromFeature(TileGeometryTransform tgs, Tile.Feature mbTileFeature, 
            NoDistortionWatermarkOptions options, int[] keySequence)
        {
            if (mbTileFeature.Type == Tile.GeomType.LineString)
            {
                var WatermarkInt = ReadLineStringWM(tgs, mbTileFeature.Geometry, options, keySequence);
                return WatermarkInt;
            }
            return null;
        }

        /// <summary>
        /// Извлекает ЦВЗ из одной фичи с лайнстрингом
        /// </summary>
        /// <param name="tgs"></param>
        /// <param name="geometry"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private int? ReadLineStringWM(TileGeometryTransform tgs, IList<uint> geometry, NoDistortionWatermarkOptions options, int[] keySequence)
        {
            int currentIndex = 0; int currentX = 0; int currentY = 0;
            //int WatermarkInt = -1; 
            int? WatermarkInt = null;
            switch (options.AtypicalEncodingType){
                case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt:
                    WatermarkInt = ExtractWMFromSingleLinestringMtLtLt(tgs, geometry, ref currentIndex, ref currentX, ref currentY, options, keySequence);
                    break;
                case NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtMt:
                    throw new NotImplementedException();
                    break;
                case NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands:
                    WatermarkInt = ExtractWMFromSingleLinestringNLt(tgs, geometry, ref currentIndex, ref currentX, ref currentY, options, keySequence);
                    break;
            }
             
            return WatermarkInt;
        }

        private Geometry ReadPoint(TileGeometryTransform tgs, IList<uint> geometry)
        {
            int currentIndex = 0; int currentX = 0; int currentY = 0;
            var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, forPoint:true);
            return CreatePuntal(sequences);
        }

        private Geometry ReadLineString(TileGeometryTransform tgs, IList<uint> geometry)
        {
            int currentIndex = 0; int currentX = 0; int currentY = 0;
            var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY);
            return CreateLineal(sequences);
        }

        private Geometry ReadPolygon(TileGeometryTransform tgs, IList<uint> geometry)
        {
            int currentIndex = 0; int currentX = 0; int currentY = 0;
            var sequences = ReadCoordinateSequences(tgs, geometry, ref currentIndex, ref currentX, ref currentY, 1);
            return CreatePolygonal(sequences);
        }

        private Geometry CreatePuntal(CoordinateSequence[] sequences)
        {
            if (sequences == null || sequences.Length == 0)
                return null;

            var points = new Point[sequences.Length];
            for (int i = 0; i < sequences.Length; i++)
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
            for (int i = 0; i < sequences.Length; i++)
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

            for (int i = 0; i < sequences.Length; i++)
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
        /// Извлечение ЦВЗ из лайнстринга, закодированного, как MoveTo-LineTo-LineTo
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
            var RealSegments = new List<bool>(); 

            // (currentX, currentY) = (0, 0), currentIndex = 0
            var currentPosition = (currentX, currentY);

            var (command, count) = ParseCommandInteger(geometry[currentIndex++]); // после команды currentIndex = 1
            Debug.Assert(command == MapboxCommandType.MoveTo);
            //Debug.Assert(count >= 1 && count <= 2, "Первая команда MoveTo имеет счётчик команд не 1 и не 2");
            Debug.Assert(count == 1, "Первая команда MoveTo имеет счётчик команд не 1");

            // Read the current position
            currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex); // после команды currentIndex = 3

            /*
            bool firstSegmentEncoded = false;

            if (count == 2)
            {
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex); // после команды currentIndex = 5
                firstSegmentEncoded = true;
            }
            */

            while (currentIndex < geometry.Count)
            {
                //Console.WriteLine("Парсим MoveTo-LineTo"); // отладка

                // Если в первый сегмент встроено, то команда == LineTo
                (command, count) = ParseCommandInteger(geometry[currentIndex++]);
                /*
                if (firstSegmentEncoded)
                {
                    Console.WriteLine("LineTo, первый сегмент закодирован"); // отладка

                    Debug.Assert(command == MapboxCommandType.LineTo);
                    Debug.Assert(count >= 2);
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    RealSegments.Add(true);
                    count -= 2;

                    firstSegmentEncoded = false;

                    Console.WriteLine("Первый закодированный сегмент рассмотрен"); // отладка

                }
                */
                // иначе - команда == MoveTo
                if (command == MapboxCommandType.MoveTo)
                {
                    //Console.WriteLine("MoveTo, после закодированного сегмента"); // отладка

                    Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    (command, count) = ParseCommandInteger(geometry[currentIndex++]);
                    Debug.Assert(command == MapboxCommandType.LineTo, $"Assertion: MapboxCommandType = {command}");
                    Debug.Assert(count >= 2, $"Assertion: LineTo count = {count}");
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    RealSegments.Add(true);
                    count -= 2;

                    Console.WriteLine($"currentPosition: {currentPosition}"); // отладка
                    //Console.WriteLine("Очередной закодированный сегмент рассмотрен"); // отладка
                }
                else
                {
                    Debug.Assert(command == MapboxCommandType.LineTo, "ну и че");
                    //Console.WriteLine($"Первый сегмент не закодирован, вторая команда - LineTo, count = {count}"); // отладка
                }
                //else throw new Exception("Cannot decode the sequence");
                

                // Read and add offsets
                for (int i = 0; i < count; i++)
                {
                    //Console.WriteLine($"currentPositionX: {currentPosition.currentX}, currentPositionY: {currentPosition.currentY}," + // отладка
                        //$"currentIndex: {currentIndex}"); // отладка
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    RealSegments.Add(false);

                    Console.WriteLine($"currentPosition: {currentPosition}"); // отладка
                    //Console.WriteLine("параметры LineTo"); // отладка
                }
            }

            var realSegmentsNum = RealSegments.Count;
            //var ElementarySegmentsNum = keySequence.Length * 2;
            var realSegmentsInONEElemSegment = realSegmentsNum / options.D;

            // предусмотреть возможность отражения лайнстринга
            var ExtractedWatermarkInts = new List<int>();
            //int WatermarkInt;
            /*for (int i = 0; i < RealSegments.Count; i++) // условие изменить, так как ток половину лайнстринга рассматриваем
            {
                var currentElementarySegment = i/realSegmentsInONEElemSegment;
                if (RealSegments[i] == true)
                {
                    ExtractedWatermarkInts.Add(keySequence[currentElementarySegment]);
                }
            }*/

            if (RealSegments.Count < options.D) // если вотермарки не обнаружено (количество реальных сегментов меньше количества элементарных)
                return null;

            Console.WriteLine($"realSegmentsInONEElemSegment: {realSegmentsInONEElemSegment}"); // отладка
            Console.WriteLine($"keySequence: {ConsoleWriter.GetArrayStr<int>(keySequence)}"); // отладка

            for (int i = 0; i < options.D/2; i++)
            {
                for (int j = 0; j < realSegmentsInONEElemSegment; j++)
                {
                    if (RealSegments[realSegmentsInONEElemSegment * i + j])
                    {
                        Console.WriteLine($"current keySequence[{i}]: {keySequence[i]}"); // отладка

                        ExtractedWatermarkInts.Add(keySequence[i]);
                        break;
                    }
                }
            }

            Console.WriteLine($"ExtractedWatermarkInts: {ConsoleWriter.GetIEnumerableStr<int>(ExtractedWatermarkInts)}"); // отладка
            // if (ExtractedWatermarkInts.Count == 0) return null;

            var groupsWithCounts = from s in ExtractedWatermarkInts
                                   group s by s into g
                                   select new
                                   {
                                       Item = g.Key,
                                       Count = g.Count()
                                   };
            var groupsSorted = groupsWithCounts.OrderByDescending(g => g.Count);
            int mostFrequestWatermarkInt = groupsSorted.First().Item;

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
            var RealSegments = new List<bool>();

            // (currentX, currentY) = (0, 0), currentIndex = 0
            var currentPosition = (currentX, currentY);

            // для проверки, что новый MoveTo не смещает курсор (почти) относительно предыдущего положения
            var lastPosition = currentPosition;

            bool isFirstSegment = true;

            while (currentIndex < geometry.Count)
            {
                //Console.WriteLine("Парсим MoveTo-LineTo"); // отладка

                var (command, count) = ParseCommandInteger(geometry[currentIndex++]);
                Debug.Assert(command == MapboxCommandType.MoveTo);
                Debug.Assert(count == 1, $"Assertion: MoveTo count = {count}");
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

                Console.WriteLine($"lastPosition = {lastPosition}, currentPosition = {currentPosition}"); // отладка
                if (!isFirstSegment && lastPosition != currentPosition)
                {
                    return null;
                }

                (command, count) = ParseCommandInteger(geometry[currentIndex++]);
                Debug.Assert(command == MapboxCommandType.LineTo, $"Assertion: MapboxCommandType = {command}");
                Debug.Assert(count >= 1, $"Assertion: LineTo count = {count}");
                currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);

                if (!isFirstSegment) {
                    RealSegments.Add(true);
                }
                else
                {
                    RealSegments.Add(false);
                }
                isFirstSegment = false;


                for (int i = 1; i < count; i++)
                {
                    currentPosition = ParseOffset(currentPosition, geometry, ref currentIndex);
                    RealSegments.Add(false);

                    lastPosition = currentPosition;
                }
            }

            var realSegmentsNum = RealSegments.Count;
            var realSegmentsInONEElemSegment = realSegmentsNum / options.D;

            // предусмотреть возможность отражения лайнстринга
            var ExtractedWatermarkInts = new List<int>();

            if (RealSegments.Count < options.D) // если вотермарки не обнаружено (количество реальных сегментов меньше количества элементарных)
                return null;

            Console.WriteLine($"realSegmentsInONEElemSegment: {realSegmentsInONEElemSegment}"); // отладка
            Console.WriteLine($"keySequence: {ConsoleWriter.GetArrayStr<int>(keySequence)}"); // отладка

            for (int i = 0; i < options.D / 2; i++)
            {
                for (int j = 0; j < realSegmentsInONEElemSegment; j++)
                {
                    if (RealSegments[realSegmentsInONEElemSegment * i + j]) 
                    {
                        Console.WriteLine($"current keySequence[{i}]: {keySequence[i]}"); // отладка

                        ExtractedWatermarkInts.Add(keySequence[i]);
                        break;
                    }
                }
            }

            Console.WriteLine($"ExtractedWatermarkInts: {ConsoleWriter.GetIEnumerableStr<int>(ExtractedWatermarkInts)}"); // отладка

            // if (ExtractedWatermarkInts.Count == 0) return null;

            var groupsWithCounts = from s in ExtractedWatermarkInts
                                   group s by s into g
                                   select new
                                   {
                                       Item = g.Key,
                                       Count = g.Count()
                                   };
            var groupsSorted = groupsWithCounts.OrderByDescending(g => g.Count);
            int mostFrequestWatermarkInt = groupsSorted.First().Item;

            // update current position values
            currentX = currentPosition.currentX;
            currentY = currentPosition.currentY;

            return mostFrequestWatermarkInt;
        }

        private CoordinateSequence[] ReadCoordinateSequences(
            TileGeometryTransform tgs, IList<uint> geometry,
            ref int currentIndex, ref int currentX, ref int currentY, int buffer = 0, bool forPoint = false)
        {

            (var command, int count) = ParseCommandInteger(geometry[currentIndex]);
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
                int sequenceIndex = 0;
                TransformOffsetAndAddToSequence(tgs, currentPosition, sequence, sequenceIndex++);

                // Read and add offsets
                for (int i = 1; i <= count; i++)
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
            for (int i = 0; i < numSequences; i++)
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

            for (int i = 0; i < mbTileFeature.Tags.Count; i += 2)
            {
                string key = keys[(int)mbTileFeature.Tags[i]];
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
}
