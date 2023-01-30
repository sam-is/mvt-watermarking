using MvtWatermark.NoDistortionWatermark;
using MvtWatermark.NoDistortionWatermark.Auxiliary;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using NoDistortionWatermarkMetrics.DebugClasses;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using System.Collections;
using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace NoDistortionWatermarkMetrics;

public static class MetricAnalyzer
{
    /// <summary>
    /// Набор характеристик тайла: zoom (приближение), x (абсцисса), y (ордината)
    /// </summary>
    public struct ZxySet
    {
        public int Zoom { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public ZxySet(int zoom, int x, int y)
        {
            this.Zoom = zoom;
            this.X = x;
            this.Y = y;
        }
    }

    /// <summary>
    /// Набор диапазона параметров NoDistortionWatermarkOptions для проверки валидности встроенных/извлеченных ЦВЗ
    /// </summary>
    public struct ParameterRangeSet
    {
        public int Mmax { get; set; }
        public int Nbmax { get; set; }
        public int Lfmax { get; set; }
        public int Lsmax { get; set; }
        private int _wmMin;
        private int _wmMax;
        public int WmMin { 
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
        public int WmMax
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

        public ParameterRangeSet(int mMax, int nbMax, int lfMax, int lsMax, int wmMin, int wmMax)
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

    /// <summary>
    /// Проверка валидности извлеченных ЦВЗ для дерева тайлов из Базы данных
    /// </summary>
    /// <param name="parameterRangeSet">Диапазон параметров NoDistortionWatermarkOptions</param>
    /// <param name="parameterSets">Коллекция наборов параметров для тайлов, согласно которым тайлы будут браться из базы данных</param>
    /// <returns></returns>
    public static bool DisplayMetricForDBTileSet(ParameterRangeSet parameterRangeSet, IEnumerable<ZxySet> parameterSets)
    {
        var mainErrorsResultList = new List<int>();
        var resultExtractedIntsList = new List<int>();

        var singleOptionsSetIntsList = new List<int>();

        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var connectionString = $"Data Source = {path}";
        using var sqliteConnection = new SqliteConnection(connectionString);
        sqliteConnection.Open();

        Console.WriteLine($"Connection string = {connectionString}");

        var vtTree = new VectorTileTree();
        var areAnyCorrectTilesHere = false;

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

        NoDistortionWatermarkOptions.AtypicalEncodingTypes aEtype;

        for (var m = 1; m <= parameterRangeSet.Mmax; m++)
        {
            for (var nb = 1; nb <= parameterRangeSet.Nbmax; nb++)
            {
                for (var lf = 1; lf <= parameterRangeSet.Lfmax; lf++)
                {
                    for (var ls = 1; ls <= parameterRangeSet.Lsmax; ls++)
                    {
                        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
                        var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, aEtype, false);

                        var singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                            vtTree, options, false, out singleOptionsSetIntsList);
                        mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                        resultExtractedIntsList.AddRange(singleOptionsSetIntsList);

                        options.SecondHalfOfLineStringIsUsed = true;

                        singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax,
                            vtTree, options, false, out singleOptionsSetIntsList);
                        mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                        resultExtractedIntsList.AddRange(singleOptionsSetIntsList);


                        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
                        options.AtypicalEncodingType = aEtype;
                        options.SecondHalfOfLineStringIsUsed = false;

                        singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                            vtTree, options, false, out singleOptionsSetIntsList);
                        mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                        resultExtractedIntsList.AddRange(singleOptionsSetIntsList);

                        options.SecondHalfOfLineStringIsUsed = true;

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

    /// <summary>
    /// Проверка тайла на валидность геометрии (для библиотеки NetTopologySuite)
    /// 
    /// Ремарка: некоторые MVT-тайлы являются валидными для QGis, но невалидными для NetTopologySuite
    /// </summary>
    /// <param name="parameterSet"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static bool TestVectorTileIsCorrect(ZxySet parameterSet)
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var connectionString = $"Data Source = {dbPath}";
        using var sqliteConnection = new SqliteConnection(connectionString);
        sqliteConnection.Open();

        var vt = GetSingleVectorTileFromDB(sqliteConnection, parameterSet.Zoom, parameterSet.X, parameterSet.Y);
        if (vt == null)
            return false;

        var filePath = $"C:\\SerializedTiles\\SerializedWM_Metric\\{parameterSet.Zoom}\\{parameterSet.X}\\{parameterSet.Y}.mvt";

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

    /// <summary>
    /// Возвращает MVT-тайл из Sqlite-базы данных
    /// </summary>
    /// <param name="sqliteConnection"></param>
    /// <param name="zoom"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static VectorTile? GetSingleVectorTileFromDB(SqliteConnection? sqliteConnection, int zoom, int x, int y)
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

    /// <summary>
    /// Проверка валидности извлеченных ЦВЗ для тайла, который будет создан в этой функции по переданным параемтрам
    /// </summary>
    /// <param name="parameterRangeSet"></param>
    /// <param name="parameterSet"></param>
    /// <returns></returns>
    public static bool DisplayUsersTileMetric(ParameterRangeSet parameterRangeSet, ZxySet parameterSet)
    {
        var mainErrorsResultList = new List<int>();
        var resultExtractedIntsList = new List<int>();

        var singleOptionsSetIntsList = new List<int>();

        var vtTree = new VectorTileTree();
        ulong tile_id;
        VectorTile vt = CreateVectorTile(parameterSet.X, parameterSet.Y, parameterSet.Zoom, out tile_id);
        vtTree[tile_id] = vt;

        NoDistortionWatermarkOptions.AtypicalEncodingTypes aEtype;

        for (var m = 1; m <= parameterRangeSet.Mmax; m++)
        {
            for (var nb = 1; nb <= parameterRangeSet.Nbmax; nb++)
            {
                for (var lf = 1; lf <= parameterRangeSet.Lfmax; lf++)
                {
                    for (var ls = 1; ls <= parameterRangeSet.Lsmax; ls++)
                    {
                        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
                        var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, aEtype, false);

                        var singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                            vtTree, options, false, out singleOptionsSetIntsList);
                        mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                        resultExtractedIntsList.AddRange(singleOptionsSetIntsList);

                        options.SecondHalfOfLineStringIsUsed = true;

                        singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax,
                            vtTree, options, false, out singleOptionsSetIntsList);
                        mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                        resultExtractedIntsList.AddRange(singleOptionsSetIntsList);


                        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
                        //options = new NoDistortionWatermarkOptions(m, nb, Ls, Lf, aEtype, true);
                        options.AtypicalEncodingType = aEtype;
                        options.SecondHalfOfLineStringIsUsed = false;

                        singleOptionsSetErrorsList = GetDifferentMessagesSingleParameterSetMetric(parameterRangeSet.WmMin, parameterRangeSet.WmMax, 
                            vtTree, options, false, out singleOptionsSetIntsList);
                        mainErrorsResultList.AddRange(singleOptionsSetErrorsList);
                        resultExtractedIntsList.AddRange(singleOptionsSetIntsList);

                        options.SecondHalfOfLineStringIsUsed = true;

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
        var key = 123;

        var resultedList = new List<int>();
        extractedIntsList = new List<int>();

        for (var i = begin; i < end; i++)
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
        var ndWm = new NoDistortionWatermark(options);

        string path;
        if (!isParallel)
        {
            path = "C:\\SerializedTiles\\SerializedWM_Metric";
        }
        else
        {
            path = $"C:\\SerializedTiles\\SerializedWM_Metric_parallel\\{options.M}_{options.Nb}_{options.Lf}";
        }

        var resulttree = ndWm.Embed(vtTree, key, message);
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
        var ndWm = new NoDistortionWatermark(options);
        var message = ndWm.Extract(tiles, key);
        WatermarkInt = WatermarkTransform.GetIntFromBitArrayNullable(message);
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

        for (var i = 1; i < 20; i++)
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
        double xCoord, yCoord;

        if (numOfdots == 1)
        {
            xCoord = rand.Next(-179, 178) + 0.5;
            yCoord = rand.Next(-89, 88) + 0.5;
            var point = new Point(new Coordinate(xCoord, yCoord));
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
            xCoord = rand.Next(-179, 179);
            yCoord = rand.Next(-89, 89);

            coordinateCollection.Add(new Coordinate(xCoord, yCoord));
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

/*
public static bool GetUsersTileMetricParallel(int begin, int end, int zoom, int x, int y)
    {
        if (begin < 1 || begin >= end)
            return false;

        var mainResultList = new List<int>();
        var resultExtractedIntsList = new List<int>();

        int LsKey = 54321;
        NoDistortionWatermarkOptions.AtypicalEncodingTypes aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        bool secondHalfIsUsed = false;

        var m_Max = 5;
        var tasks = new Task<List<int>>[m_Max];
        for (int m = 1; m <= m_Max; m++)
        {
            tasks[m - 1] = new Task<List<int>>(() => TaskAction(m, LsKey, aEtype, secondHalfIsUsed, begin, end, zoom, x, y, out resultExtractedIntsList));
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
        NoDistortionWatermarkOptions.AtypicalEncodingTypes aEtype, bool secondHalfIsUsed, int begin, int end, int zoom, int x, int y,
        out List<int> resultExtractedIntsList)
    {
        var resultList = new List<int>();
        resultExtractedIntsList = new List<int>();

        for (int Nb = 1; Nb <= 8; Nb++)
        {
            for (int Lf = 1; Lf <= 5; Lf++)
            {
                var options = new NoDistortionWatermarkOptions(m, Nb, LsKey, Lf, aEtype, secondHalfIsUsed);
                var extractedIntsList = new List<int>();

                resultList.AddRange(GetDifferentMessagesMetric(begin, end, zoom, x, y, options, true, out extractedIntsList));
                resultExtractedIntsList.AddRange(extractedIntsList);
            }
        }

        return resultList;
    }
*/