﻿using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using NetTopologySuite.IO.VectorTiles.Mapbox.Watermarking;
using MvtWatermark.DebugClasses;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using System.Collections;
using NetTopologySuite.Utilities;
using System.IO.Compression;
using System.IO;

using Microsoft.Data.Sqlite;

namespace NoDistortionWatermarkMetrics
{
    internal static class MetricAnalyzer
    {
        internal struct ZxySet
        {
            internal int Zoom { get; set; }
            internal int X { get; set; }
            internal int Y { get; set; }

            internal ZxySet(int zoom, int x, int y)
            {
                this.Zoom = zoom;
                this.X = x;
                this.Y = y;
            }
        }

        internal struct ParameterRangeSet
        {
            internal int Mmax { get; set; }
            internal int Nbmax { get; set; }
            internal int Lfmax { get; set; }
            internal int Lsmax { get; set; }
            private int _wmMin;
            private int _wmMax;
            internal int WmMin { 
                get { return _wmMin; } 
                set
                {
                    if (value < 1)
                        throw new Exception("WmMin cannot be smaller then 1");
                    else if (value > _wmMax)
                        throw new Exception("WmMin cannot be bigger then WmMax");
                    _wmMin = value;
                } 
            }
            internal int WmMax
            {
                get { return _wmMax; }
                set
                {
                    if (value < 1)
                        throw new Exception("WmMax cannot be smaller then 1");
                    else if(value < _wmMin)
                        throw new Exception("WmMax cannot be smaller then WmMin");
                    _wmMax = value;
                }
            }

            internal ParameterRangeSet(int mMax, int nbMax, int lfMax, int lsMax, int wmMin, int wmMax)
            {
                if (wmMin > wmMax)
                    throw new Exception("WmMin cannot be bigger then WmMax");
                else if (wmMin < 1 || wmMax < 1)
                {
                    throw new Exception("WmMin and WmMax cannot be smaller then 1");
                }

                Mmax = mMax; 
                Nbmax = nbMax; 
                Lfmax = lfMax;
                Lsmax = lsMax;
                _wmMin = wmMin;
                _wmMax = wmMax;
            }
        }

        internal static bool DisplayMetricForDBTileSet(ParameterRangeSet parameterRangeSet, IEnumerable<ZxySet> parameterSets)
        {
            var mainErrorsResultList = new List<int>();
            var resultExtractedIntsList = new List<int>();

            var singleOptionsSetIntsList = new List<int>();

            var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
            string? connectionString = $"Data Source = {path}";
            using var sqliteConnection = new SqliteConnection(connectionString);
            sqliteConnection.Open();

            Console.WriteLine($"Connection string = {connectionString}");

            var vtTree = new VectorTileTree();
            bool areAnyCorrectTilesHere = false;

            foreach (var parameterSet in parameterSets)
            {
                var vt = GetSingleVectorTileFromDB(sqliteConnection, parameterSet.Zoom, parameterSet.X, parameterSet.Y);
                if (vt != null)
                {
                    areAnyCorrectTilesHere = true;
                    vtTree[vt.TileId] = vt;
                }
            }

            if (!areAnyCorrectTilesHere)
                return false;

            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype;

            for (int m = 1; m <= parameterRangeSet.Mmax; m++)
            {
                for (int Nb = 1; Nb <= parameterRangeSet.Nbmax; Nb++)
                {
                    for (int Lf = 1; Lf <= parameterRangeSet.Lfmax; Lf++)
                    {
                        for (int Ls = 1; Ls <= parameterRangeSet.Lsmax; Ls++)
                        {
                            AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
                            var options = new NoDistortionWatermarkOptions(m, Nb, Ls, Lf, AEtype, false);

                            var singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                                vtTree, options, false, out singleOptionsSetIntsList);

                            mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                            resultExtractedIntsList.AddRange(singleOptionsSetIntsList);

                            AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
                            options = new NoDistortionWatermarkOptions(m, Nb, Ls, Lf, AEtype, true);

                            singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                                vtTree, options, false, out singleOptionsSetIntsList);

                            mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                            resultExtractedIntsList.AddRange(singleOptionsSetIntsList);
                        }
                    }
                }
            }

            Console.WriteLine($"Извлечённые ЦВЗ: {ConsoleWriter.GetIEnumerableStr(resultExtractedIntsList)}");
            Console.WriteLine($"\nСписок соответствия: {ConsoleWriter.GetIEnumerableStr(mainErrorsResultList)}");
            //Console.WriteLine($"\nСреднее количество ошибок в тайле x={x}, y={y}, zoom={zoom}: {mainErrorsResultList.Average()}");
            Console.WriteLine($"\nСреднее количество ошибок в дереве из бд: {mainErrorsResultList.Average()}");

            return true;
        }

        internal static bool TestVectorTileIsCorrect(ZxySet parameterSet)
        {
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
            string? connectionString = $"Data Source = {dbPath}";
            using var sqliteConnection = new SqliteConnection(connectionString);
            sqliteConnection.Open();

            var vt = GetSingleVectorTileFromDB(sqliteConnection, parameterSet.Zoom, parameterSet.X, parameterSet.Y);
            if (vt == null)
                return false;

            string filePath = $"C:\\SerializedTiles\\SerializedWM_Metric\\{parameterSet.Zoom}\\{parameterSet.X}\\{parameterSet.Y}.mvt";

            using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                vt.Write(fileStream);
            }

            var reader = new MapboxTileReader();
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(vt.TileId);

            using (var fs = new FileStream(filePath, FileMode.Open))
            {
                vt = reader.Read(fs, tileDefinition);
                foreach (var l in vt.Layers)
                {
                    var features = l.Features;
                    Console.WriteLine("\n");

                    foreach (var f in features)
                    {
                        if (!f.Geometry.IsValid)
                            throw new Exception("Невалидная геометрия!");
                    }
                }
            }

            return true;
        }

        internal static VectorTile? GetSingleVectorTileFromDB(SqliteConnection? sqliteConnection, int zoom, int x, int y)
        {
            using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
            command.Parameters.AddWithValue("$z", zoom);
            command.Parameters.AddWithValue("$x", x);
            command.Parameters.AddWithValue("$y", (1 << zoom) - y - 1);
            var obj = command.ExecuteScalar();

            if (obj == null)
            {
                Console.WriteLine("obj = null");
                return null;
            }
            else
            {
                Console.WriteLine("Successfully got the tile");
            }

            var bytes = (byte[])obj!;

            using var memoryStream = new MemoryStream(bytes);
            var reader = new MapboxTileReader();

            memoryStream.Seek(0, SeekOrigin.Begin);
            using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
            var vt = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom));

            return vt;
        }

        internal static bool DisplayUsersTileMetric(ParameterRangeSet parameterRangeSet, int zoom, int x, int y)
        {
            var mainErrorsResultList = new List<int>();
            var resultExtractedIntsList = new List<int>();

            var singleOptionsSetIntsList = new List<int>();

            var vtTree = new VectorTileTree();
            ulong tile_id;
            VectorTile vt = CreateVectorTile(x, y, zoom, out tile_id);
            vtTree[tile_id] = vt;

            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype;

            for (int m = 1; m <= parameterRangeSet.Mmax; m++)
            {
                for (int Nb = 1; Nb <= parameterRangeSet.Nbmax; Nb++)
                {
                    for (int Lf = 1; Lf <= parameterRangeSet.Lfmax; Lf++)
                    {
                        for (int Ls = 1; Ls <= parameterRangeSet.Lsmax; Ls++)
                        {
                            AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
                            var options = new NoDistortionWatermarkOptions(m, Nb, Ls, Lf, AEtype, false);

                            var singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                                vtTree, options, false, out singleOptionsSetIntsList);

                            mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                            resultExtractedIntsList.AddRange(singleOptionsSetIntsList);

                            AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
                            options = new NoDistortionWatermarkOptions(m, Nb, Ls, Lf, AEtype, true);

                            singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                                vtTree, options, false, out singleOptionsSetIntsList);

                            mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                            resultExtractedIntsList.AddRange(singleOptionsSetIntsList);
                        }
                    }
                }
            }

            Console.WriteLine($"Извлечённые ЦВЗ: {ConsoleWriter.GetIEnumerableStr(resultExtractedIntsList)}");
            Console.WriteLine($"Список соответствия: {ConsoleWriter.GetIEnumerableStr(mainErrorsResultList)}");
            Console.WriteLine($"\nСреднее количество ошибок: {mainErrorsResultList.Average()}");

            return true;
        }

        

        private static List<int> GetDifferentMessagesSingleParameterSetMetric(int begin, int end, VectorTileTree vtTree, NoDistortionWatermarkOptions options, bool isParallel,
            out List<int> extractedIntsList)
        {
            if (begin < 1 || begin >= end) 
                throw new Exception("Некорректные значения begin и end");
            //var options = new NoDistortionWatermarkOptions(2, 3, 54321, 3, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt, true);
            int key = 123;

            var resultedList = new List<int>();
            extractedIntsList = new List<int>();

            for (int i = begin; i < end; i++)
            {
                int? extractedMessageInt;
                var comparsionResult = IsExtractedWatermarkCorrect(options, key, i, vtTree, isParallel, out extractedMessageInt);
                if (comparsionResult != null)
                {
                    resultedList.Add(Convert.ToInt32(comparsionResult)); // может, среднее арифметическое можно и с null-элементами вычислять?
                    extractedIntsList.Add(Convert.ToInt32(extractedMessageInt));
                }
            }

            //ConsoleWriter.WriteIEnumerable(resultedList);
            //Console.WriteLine($"\nСреднее количество ошибок: {resultedList.Average()}");

            return resultedList;
        }

        /// <summary>
        /// Побитовое сравнение результата извлечения и изначального ЦВЗ
        /// </summary>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <param name="messageInt"></param>
        /// <param name="tileZoom"></param>
        /// <param name="tileX"></param>
        /// <param name="tileY"></param>
        /// <param name="isParallel"></param>
        /// <param name="extractedMessageInt"></param>
        /// <returns></returns>
        private static int? IsExtractedWatermarkCorrect(NoDistortionWatermarkOptions options, int key, int messageInt, VectorTileTree vtTree, bool isParallel,
            out int? extractedMessageInt)
        {
            var message = new BitArray(new int[] { messageInt });

            //var resultTree = EmbedingWatermark(tileZoom, tileX, tileY, options, message, key, isParallel);
            var resultTree = EmbeddingWatermark(vtTree, options, message, key, isParallel);
            var extractedMessage = ExtractFromVectorTileTree(resultTree, options, key, out extractedMessageInt);

            Console.WriteLine($"extractedMessageInt = {extractedMessageInt}\n");

            if (extractedMessageInt == null)
                return null; // проверить, может ли быть возвращён null и зачем он нужен

            if (extractedMessage.Count == 0 || (extractedMessage.Count == 1 && extractedMessage[0] == false))
                return null; // проверить, может ли быть возвращён null и зачем он нужен

            if (extractedMessage.AreEqual(message))
                return 0;
            else return 1;
        }

        private static VectorTileTree EmbedingWatermark(int zoom, int x, int y, NoDistortionWatermarkOptions options, BitArray message, int key = 123, bool isParallel = false)
        {
            var NdWm = new NoDistortionWatermark(options);

            var vtTree = new VectorTileTree();
            ulong tile_id;
            VectorTile vt = CreateVectorTile(x, y, zoom, out tile_id);
            vtTree[tile_id] = vt;

            string path;
            if (!isParallel)
            {
                path = "C:\\SerializedTiles\\SerializedWM_Metric";
            }
            else
            {
                path = $"C:\\SerializedTiles\\SerializedWM_Metric_parallel\\{options.M}_{options.Nb}_{options.Lf}";
            }

            var resulttree = NdWm.Embed(vtTree, key, message);
            resulttree.Write(path);

            //Console.WriteLine($"messageInt = {WatermarkTransform.getIntFromBitArray(message)}");
            //Console.WriteLine("Встраивание завершено"); // отладка

            ReadSomething($"{path}\\{zoom}\\{x}\\{y}.mvt", tile_id);

            return resulttree;
        }

        /// <summary>
        /// Встраивание ЦВЗ в переданое дерево векторных тайлов
        /// </summary>
        /// <param name="vtTree"></param>
        /// <param name="options"></param>
        /// <param name="message"></param>
        /// <param name="key"></param>
        /// <param name="isParallel"></param>
        /// <returns></returns>
        private static VectorTileTree EmbeddingWatermark(VectorTileTree vtTree, NoDistortionWatermarkOptions options, BitArray message, int key = 123, bool isParallel = false)
        {
            var NdWm = new NoDistortionWatermark(options);

            string path;
            if (!isParallel)
            {
                path = "C:\\SerializedTiles\\SerializedWM_Metric";
            }
            else
            {
                path = $"C:\\SerializedTiles\\SerializedWM_Metric_parallel\\{options.M}_{options.Nb}_{options.Lf}";
            }

            var resulttree = NdWm.Embed(vtTree, key, message);
            resulttree.Write(path);

            //ReadSomething($"{path}\\{zoom}\\{x}\\{y}.mvt", tile_id);

            return resulttree;
        }

        /// <summary>
        /// Извлечение ЦВЗ в формате Integer? из VectorTileTree.
        /// </summary>
        /// <param name="tiles"></param>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private static BitArray ExtractFromVectorTileTree(VectorTileTree tiles, NoDistortionWatermarkOptions options, int key, out int? WatermarkInt)
        {
            var NdWm = new NoDistortionWatermark(options);
            var message = NdWm.Extract(tiles, key);
            WatermarkInt = WatermarkTransform.getIntFromBitArrayNullable(message);
            return message;
        }

        /// <summary>
        /// Чтение геометрии из файла (одного тайла) и проверка геометрии в нём на валидность.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="tile_id"></param>
        private static void ReadSomething(string filePath, ulong tile_id)
        {
            //Create a MapboxTileReader.
            var reader = new MapboxTileReader();

            //Define which tile you want to read. You may be able to extract the x/y/zoom info from the file path of the tile. 
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile_id);

            //Open a vector tile file as a stream.
            using (var fs = new FileStream(filePath, FileMode.Open))
            {
                //Read the vector tile.
                var vt = reader.Read(fs, tileDefinition);

                //Loop through each layer.
                foreach (var l in vt.Layers)
                {
                    //Access the features of the layer and do something with them. 
                    var features = l.Features;
                    //Console.WriteLine("\n");

                    foreach (var f in features)
                    {
                        if (!f.Geometry.IsValid)
                            throw new Exception("Невалидная геометрия!");
                    }
                }
            }
        }

        /// <summary>
        /// Создание пользовательского VectorTile с одним слоем
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="zoom"></param>
        /// <param name="tile_id"></param>
        /// <returns></returns>
        private static VectorTile CreateVectorTile(int x, int y, int zoom, out ulong tile_id)
        {
            tile_id = NetTopologySuite.IO.VectorTiles.Tiles.Changed.Tile.CalculateTileId(zoom, x, y);
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom);
            var vt = new VectorTile { TileId = tileDefinition.Id };
            var lyr = new Layer { Name = "layer1" };

            for (int i = 1; i < 20; i++)
            {
                var feature = CreateFeature(i * i, i);
                lyr.Features.Add(feature);
            }

            vt.Layers.Add(lyr);

            Console.WriteLine("Возвращаем векторный тайл..."); // отладка
            return vt;
        }

        /// <summary>
        /// Создание Feature с рандомными точками (seed = numOfDots), тип геометрии - точка либо лайнстринг, полигонов не создаётся.
        /// </summary>
        /// <param name="numOfdots"></param>
        /// <param name="id"></param>
        /// <param name="isPolygon"></param>
        /// <returns></returns>
        private static Feature CreateFeature(int numOfdots, int id, bool isPolygon = false)
        {
            var rand = new Random(numOfdots);
            int X, Y;

            if (numOfdots == 1)
            {
                X = rand.Next(-179, 179);
                Y = rand.Next(-89, 89);
                var point = new Point(new Coordinate(X, Y));
                //Console.WriteLine("\nПоинт: "); // отладка
                //Console.WriteLine(point); // отладка
                //Console.WriteLine("\n"); // отладка
                return new Feature
                {
                    Geometry = point,
                    Attributes = new AttributesTable(new Dictionary<string, object>()
                    {
                        ["LN_ID"] = id,
                        ["type"] = "Point",
                    })
                };
            }

            var coordinateCollection = new List<Coordinate>();
            for (var i = 0; i < numOfdots; i++)
            {
                X = rand.Next(-179, 179);
                Y = rand.Next(-89, 89);

                coordinateCollection.Add(new Coordinate(X, Y));
            }
            var coordinateArray = coordinateCollection.ToArray();

            var geom = new LineString(coordinateArray);

            //Console.WriteLine("\nЛайнстринг: "); // отладка
            //Console.WriteLine(geom.ToString()); // отладка
            //Console.WriteLine("\n"); // отладка

            return new Feature
            {
                Geometry = geom,
                Attributes = new AttributesTable(new Dictionary<string, object>()
                {
                    ["LN_ID"] = id,
                    ["type"] = "Linestring",
                })
            };
        }
    }
}

/*
 internal static bool GetUsersTileMetricParallel(int begin, int end, int zoom, int x, int y)
        {
            if (begin < 1 || begin >= end)
                return false;

            var mainResultList = new List<int>();
            var resultExtractedIntsList = new List<int>();

            int LsKey = 54321;
            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
            bool secondHalfIsUsed = false;

            var m_Max = 5;
            var tasks = new Task<List<int>>[m_Max];
            for (int m = 1; m <= m_Max; m++)
            {
                tasks[m - 1] = new Task<List<int>>(() => TaskAction(m, LsKey, AEtype, secondHalfIsUsed, begin, end, zoom, x, y, out resultExtractedIntsList));
                tasks[m - 1].Start();
            }

            Task.WaitAll(tasks);
            foreach(var singleTask in tasks)
            {
                mainResultList.AddRange(singleTask.Result);
            }

            Console.WriteLine($"Извлечённые ЦВЗ: {ConsoleWriter.GetIEnumerableStr(resultExtractedIntsList)}");
            Console.WriteLine($"Список соответствия: {ConsoleWriter.GetIEnumerableStr(mainResultList)}");
            Console.WriteLine($"\nСреднее количество ошибок: {mainResultList.Average()}");

            return true;
        }
 */

/*
 private static List<int> TaskAction(int m, int LsKey, 
            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype, bool secondHalfIsUsed, int begin, int end, int zoom, int x, int y,
            out List<int> resultExtractedIntsList)
        {
            var resultList = new List<int>();
            resultExtractedIntsList = new List<int>();

            for (int Nb = 1; Nb <= 8; Nb++)
            {
                for (int Lf = 1; Lf <= 5; Lf++)
                {
                    var options = new NoDistortionWatermarkOptions(m, Nb, LsKey, Lf, AEtype, secondHalfIsUsed);
                    var extractedIntsList = new List<int>();

                    resultList.AddRange(GetDifferentMessagesMetric(begin, end, zoom, x, y, options, true, out extractedIntsList));
                    resultExtractedIntsList.AddRange(extractedIntsList);
                }
            }

            return resultList;
        }
 */