using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.IO.VectorTiles;
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
    internal static class MetricsAnalyzer
    {
        internal static bool GetDBTileMetrics(int begin, int end, int zoom, int x, int y)
        {
            if (begin < 1 || begin >= end)
                return false;

            var mainResultList = new List<int>();

            var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");

            using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
            sqliteConnection.Open();

            using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
            command.Parameters.AddWithValue("$z", zoom);
            command.Parameters.AddWithValue("$x", x);
            command.Parameters.AddWithValue("$y", (1 << zoom) - y - 1);
            var obj = command.ExecuteScalar();

            if (obj == null)
            {
                Console.WriteLine("obj = null");
                return false;
            }

            var bytes = (byte[])obj!;

            using var memoryStream = new MemoryStream(bytes);
            var reader = new MapboxTileReader();

            memoryStream.Seek(0, SeekOrigin.Begin);
            using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
            var vt = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom));

            var vtTree = new VectorTileTree();
            vtTree[vt.TileId] = vt;

            int Ls_key = 54321;
            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
            bool secondHalfIsUsed = false;

            var m_Max = 5;
            for (int m = 1; m <= m_Max; m++)
            {
                for (int Nb = 1; Nb <= 8; Nb++)
                {
                    for (int Lf = 1; Lf <= 5; Lf++)
                    {
                        var options = new NoDistortionWatermarkOptions(m, Nb, Ls_key, Lf, AEtype, secondHalfIsUsed);

                        var singleOptionsResultList = GetDifferentMessagesMetrics(begin, end, zoom, x, y, options, false);
                        mainResultList.AddRange(singleOptionsResultList);
                    }
                }
            }

            ConsoleWriter.WriteIEnumerable(mainResultList);
            Console.WriteLine($"\nСреднее количество ошибок в тайле x={x}, y={y}, zoom={zoom}: {mainResultList.Average()}");

            return true;
        }

        internal static bool GetUsersTileMetricsParallel(int begin, int end, int zoom, int x, int y)
        {
            if (begin < 1 || begin >= end)
                return false;

            var mainResultList = new List<int>();

            int Ls_key = 54321;
            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
            bool secondHalfIsUsed = false;

            var m_Max = 5;
            var tasks = new Task<List<int>>[m_Max];
            for (int m = 1; m <= m_Max; m++)
            {
                tasks[m - 1] = new Task<List<int>>(() => TaskAction(m, Ls_key, AEtype, secondHalfIsUsed, begin, end, zoom, x, y));
                tasks[m - 1].Start();
            }

            Task.WaitAll(tasks);
            foreach(var singleTask in tasks)
            {
                mainResultList.AddRange(singleTask.Result);
            }

            ConsoleWriter.WriteIEnumerable(mainResultList);
            Console.WriteLine($"\nСреднее количество ошибок: {mainResultList.Average()}");

            return true;
        }

        internal static bool GetUsersTileMetrics(int begin, int end, int zoom, int x, int y)
        {
            if (begin < 1 || begin >= end)
                return false;

            var mainResultList = new List<int>();
            /*
            int m = 2;
            int Nb = 5;
            
            int Lf = 3;
            */

            int Ls_key = 54321;
            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
            bool secondHalfIsUsed = false;

            var m_Max = 5;
            for (int m = 1; m <= m_Max; m++)
            {
                for (int Nb = 1; Nb <= 8; Nb++)
                {
                    for (int Lf = 1; Lf <= 5; Lf++)
                    {
                        var options = new NoDistortionWatermarkOptions(m, Nb, Ls_key, Lf, AEtype, secondHalfIsUsed);

                        var singleOptionsResultList = GetDifferentMessagesMetrics(begin, end, zoom, x, y, options, false);
                        mainResultList.AddRange(singleOptionsResultList);
                    }
                }
            }

            ConsoleWriter.WriteIEnumerable(mainResultList);
            Console.WriteLine($"\nСреднее количество ошибок: {mainResultList.Average()}");

            return true;
        }

        private static List<int> TaskAction(int m, int Ls_key, 
            NoDistortionWatermarkOptions.AtypicalEncodingTypes AEtype, bool secondHalfIsUsed, int begin, int end, int zoom, int x, int y)
        {
            var resultList = new List<int>();

            for (int Nb = 1; Nb <= 8; Nb++)
            {
                for (int Lf = 1; Lf <= 5; Lf++)
                {
                    var options = new NoDistortionWatermarkOptions(m, Nb, Ls_key, Lf, AEtype, secondHalfIsUsed);

                    resultList.AddRange(GetDifferentMessagesMetrics(begin, end, zoom, x, y, options, true));
                }
            }

            return resultList;
        }

        internal static List<int> GetDifferentMessagesMetrics(int begin, int end, int zoom, int x, int y, NoDistortionWatermarkOptions options, bool isParallel)
        {
            if (begin < 1 || begin >= end) 
                throw new Exception("Некорректные значения begin и end");
            //var options = new NoDistortionWatermarkOptions(2, 3, 54321, 3, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt, true);
            int key = 123;

            var resultedList = new List<int>();
            for (int i = begin; i < end; i++)
            {
                var comparsionResult = IsExtractedWatermarkCorrect(options, key, i, zoom, x, y, isParallel);
                if (comparsionResult != null)
                    resultedList.Add(Convert.ToInt32(comparsionResult)); // может, среднее арифметическое можно и с null-элементами вычислять?
            }

            //ConsoleWriter.WriteIEnumerable(resultedList);
            //Console.WriteLine($"\nСреднее количество ошибок: {resultedList.Average()}");

            return resultedList;
        }

        private static int? IsExtractedWatermarkCorrect(NoDistortionWatermarkOptions options, int key, int messageInt, int tileZoom, int tileX, int tileY, bool isParallel)
        {
            Console.WriteLine("Работает GetUsersTileMetrics");
            var message = new BitArray(new int[] { messageInt });

            var resulttree = EmbedingWatermark(tileZoom, tileX, tileY, options, message, key, isParallel);
            var extractedMessageInt = ExtractFromVTtree(resulttree, options, key);

            Console.WriteLine($"extractedMessageInt = {extractedMessageInt}");
            Console.WriteLine("GetUsersTileMetrics завершил работу");

            if (extractedMessageInt == null)
                return null;

            if (extractedMessageInt == messageInt)
                return 0;
            else return 1;
        }

        private static VectorTileTree EmbedingWatermark(int zoom, int x, int y, NoDistortionWatermarkOptions options, BitArray message, int key = 123, bool isParallel = false)
        {
            var NdWm = new NoDistortionWatermark(options);

            var vtTree = new VectorTileTree();
            ulong tile_id;
            VectorTile vt = createVectorTile(x, y, zoom, out tile_id);
            vtTree[tile_id] = vt;

            string path;
            if (!isParallel)
            {
                path = "C:\\SerializedTiles\\SerializedWM_metrics";
            }
            else
            {
                path = $"C:\\SerializedTiles\\SerializedWM_metrics_parallel\\{options.M}_{options.Nb}_{options.Lf}";
            }

            var resulttree = NdWm.Embed(vtTree, key, message);
            resulttree.Write(path);

            Console.WriteLine($"messageInt = {WatermarkTransform.getIntFromBitArray(message)}");
            Console.WriteLine("Встраивание завершено"); // отладка

            ReadSomething($"{path}\\{zoom}\\{x}\\{y}.mvt", tile_id);

            return resulttree;
        }

        /// <summary>
        /// Извлечение ЦВЗ в формате Integer? из VectorTileTree.
        /// </summary>
        /// <param name="tiles"></param>
        /// <param name="options"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private static int? ExtractFromVTtree(VectorTileTree tiles, NoDistortionWatermarkOptions options, int key = 123)
        {
            var NdWm = new NoDistortionWatermark(options);
            var WatermarkInt = WatermarkTransform.getIntFromBitArrayNullable(NdWm.Extract(tiles, key));
            if (WatermarkInt == 0)
                return null;
            return WatermarkInt;
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
                        //Console.WriteLine("Фича: ");
                        //Console.WriteLine(f.Geometry);
                        //Console.WriteLine($"Валидна ли геометрия: {f.Geometry.IsValid}");
                        if (!f.Geometry.IsValid)
                            throw new Exception("Невалидная геометрия!");
                        //Console.WriteLine(f.Attributes.Count);
                        //Console.WriteLine("\n");
                    }
                }
            }
        }

        private static VectorTile createVectorTile(int x, int y, int zoom, out ulong tile_id)
        {
            tile_id = NetTopologySuite.IO.VectorTiles.Tiles.Changed.Tile.CalculateTileId(zoom, x, y);
            var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom);
            var vt = new VectorTile { TileId = tileDefinition.Id };
            var lyr = new Layer { Name = "layer1" };

            for (int i = 1; i < 20; i++)
            {
                var feature = createFeature(i * i, i);
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
        private static Feature createFeature(int numOfdots, int id, bool isPolygon = false)
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
