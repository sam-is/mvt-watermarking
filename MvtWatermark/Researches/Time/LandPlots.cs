using Microsoft.Data.Sqlite;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.Collections;
using System.IO.Compression;
using System.Text;

namespace Researches.Time;
public class LandPlots
{
    static public void Start(string pathDb, int minZ, int maxZ, int nb, int r, string path)
    {
        path = Path.Combine(path, $"nb = {nb}, r = {r}");

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var options = new QimMvtWatermarkOptions(0.9, 0.2, 1, 2048, 2, nb, r, null);
        for (var z = minZ; z <= maxZ; z++)
        {
            var watch = new System.Diagnostics.Stopwatch();

            using (var writer = new StreamWriter(Path.Combine(path, $"{z} zoom.txt")))
            {
                writer.WriteLine("Start");
            }

            watch.Start();
            var (minX, maxX, minY, maxY) = Data.GetMinMax(pathDb, z);
            Console.WriteLine($"minX = {minX}, maxX = {maxX}, minY = {minY}, maxY = {maxY}");
            //for (var i = 0; i < 5; i++)
            //{
                //var tileTree = new VectorTileTree();
                //if (i == 0)
                //    tileTree = Data.GetStpVectorTileTree(pathDb, minX, (int)Math.Floor((double)(minX + (maxX - minX) / 4)), minY, maxY, z);
                //if (i == 1)
                //    tileTree = Data.GetStpVectorTileTree(pathDb, (int)Math.Floor((double)(minX + (maxX - minX) / 4)) + 1, (int)Math.Floor((double)(minX + 385 * ((maxX - minX) / 1024))), minY, maxY, z);
                //if (i == 2)
                //    tileTree = Data.GetStpVectorTileTree(pathDb, (int)Math.Floor((double)(minX + 385 * ((maxX - minX) / 1024))) + 1, (int)Math.Floor((double)(minX + 420 * ((maxX - minX) / 1024))), minY, maxY, z);
                //if (i == 3)
                //    tileTree = Data.GetStpVectorTileTree(pathDb, (int)Math.Floor((double)(minX + 420 * ((maxX - minX) / 1024))) + 1, (int)Math.Floor((double)(minX + 3 * ((maxX - minX) / 4))), minY, maxY, z);
                //if (i == 4)
                //    tileTree = Data.GetStpVectorTileTree(pathDb, (int)Math.Floor((double)(minX + 3 * ((maxX - minX) / 4))) + 1, maxX, minY, maxY, z);
                //watch.Stop();

                //var tileTree = Data.GetStpVectorTileTree(pathDb, 20900, 20910, (1 << z) - 22195 - 1, (1 << z) - 22185 - 1, z);

                var tileTree = Data.GetStpVectorTileTree(pathDb, z);

                using (var writer = new StreamWriter(Path.Combine(path, $"{z} zoom.txt"), true))
                {
                    writer.WriteLine($"\nGetting tiles: {watch.ElapsedMilliseconds} ms = {watch.Elapsed.TotalMinutes} min");
                }
                Compute(tileTree, z, options, path, pathDb);
            //}
        }
    }

    static public void WriteToDb(VectorTileTree tileTree, string path)
    {
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        using var transaction = sqliteConnection.BeginTransaction();

        using var command = new SqliteCommand(@"UPDATE [tiles] SET [tile_data] = $data WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y;", sqliteConnection, transaction);
        foreach (var tileId in tileTree)
        {
            var tileInfo = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);

            using var compressedStream = new MemoryStream();
            using var compressor = new GZipStream(compressedStream, CompressionMode.Compress, true);

            tileTree[tileId].Write(compressor);
            compressor.Flush();
            compressedStream.Seek(0, SeekOrigin.Begin);

            command.Parameters.AddWithValue("$z", tileInfo.Zoom);
            command.Parameters.AddWithValue("$x", tileInfo.X);
            command.Parameters.AddWithValue("$y", (1 << tileInfo.Zoom) - tileInfo.Y - 1);
            command.Parameters.AddWithValue("$data", compressedStream.ToArray());

            command.ExecuteNonQuery();
            command.Parameters.Clear();
        }

        transaction.Commit();
    }
    static public void Compute(VectorTileTree tileTree, int z, QimMvtWatermarkOptions options, string path, string pathDb)
    {
        using var writer = new StreamWriter(Path.Combine(path, $"{z} zoom.txt"), true);

        writer.WriteLine($"Count tiles: {tileTree.Count()}");
        writer.WriteLine($"Count good tile: {GetCountGoodTile(tileTree)}");

        var watch = new System.Diagnostics.Stopwatch();

        var bytes = Encoding.UTF8.GetBytes("Samarskaya oblast'. Informsputnik");//. This geodata belongs to Informsputnik. If you're reading this, it means there was a digital watermark embedded in the data.");
        //var bytes = Encoding.Unicode.GetBytes("Самарская область. Информспутник. Данные геоданные принадлежат Информспутнику. Если вы это читаете, значит в данные был встроен цифровой водяной знак");
        var message = new BitArray(bytes);
        options.MessageLength = message.Count;

        var watermark = new QimMvtWatermark(options);

        Console.WriteLine("Embeding start");
        watch.Start();
        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);
        watch.Stop();

        Console.WriteLine("Embeded");
        writer.WriteLine($"Embeding: {watch.ElapsedMilliseconds} ms = {watch.Elapsed.TotalMinutes} min");

        var pathToSave = Path.Combine("TmpTiles", "LandPlotTest");
        //Data.WriteToFile(tileTreeWatermarked, pathToSave);

        var reader = new MapboxTileReader();
        var countBrokenTile = 0;
        watch.Restart();
        Parallel.ForEach(tileTreeWatermarked, tileId =>
        {
            using var mem = new MemoryStream();
            tileTreeWatermarked[tileId].Write(mem);
            mem.Seek(0, SeekOrigin.Begin);
            try
            {
                var t = reader.Read(mem, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId));
            }
            catch(InvalidOperationException) 
            {
                tileTreeWatermarked[tileId] = tileTree[tileId];
                countBrokenTile++;
            }
        });
        watch.Stop();

        writer.WriteLine($"Count broken tile that replaced: {countBrokenTile}, {watch.ElapsedMilliseconds} ms = {watch.Elapsed.TotalMinutes} min");

        watch.Restart();
        WriteToDb(tileTreeWatermarked, pathDb);
        watch.Stop();

        writer.WriteLine($"Writing data: {watch.ElapsedMilliseconds} ms = {watch.Elapsed.TotalMinutes} min");

        //var tileTreeFromFile = Data.ReadFromFiles(pathToSave);
        var tileTreeFromFile = Data.GetStpVectorTileTree(pathDb, z);

        for (var j = 5; j <= 30; j+=5)
        {
            var countTiles = (int)Math.Floor(tileTreeFromFile.Count() * ((double)j/100));
            var tiles = tileTreeFromFile.Take(countTiles);

            var vectorTileTree = new VectorTileTree();

            foreach (var tile in tiles)
            {
                vectorTileTree[tile] = tileTreeFromFile[tile];
            }

            watch.Restart();
            var m = watermark.Extract(vectorTileTree, 0);
            watch.Stop();

            Console.WriteLine("Extracted");
            writer.WriteLine($"Extracting: {watch.ElapsedMilliseconds} ms = {watch.Elapsed.TotalMinutes} min");

            Console.WriteLine($"Count extracted bit: {m.Count}");

            using var writerMessage = new StreamWriter(Path.Combine(path, $"embeded message {z}.txt"), true);

            var b = new byte[(m.Length - 1) / 8 + 1];
            m.CopyTo(b, 0);
            var text = Encoding.UTF8.GetString(b);

            writerMessage.WriteLine($"\nText {j}: \n");
            writerMessage.Write(text);
            Console.WriteLine(text);

            var count = 0;
            for (var i = 0; i < m.Count; i++)
                if (m[i] != message[i % message.Count])
                {
                    count++;
                }

            writer.WriteLine($"{j} Length all messages: {m.Count}\nLength message: {message.Count}\nCount not correct bit: {count}\n");
        }
    }

    public static int GetCountGoodTile(VectorTileTree tileTree)
    {
        var listCount = new List<int>();

        //foreach (var tileId in tileTree)
        //    Console.WriteLine(tileId);

        foreach (var tileId in tileTree)
        {
            var count = 0;
            foreach (var layer in tileTree[tileId].Layers)
                foreach (var feature in layer.Features)
                    count += feature.Geometry.NumPoints;
            listCount.Add(count);
        }
        listCount = [.. listCount.OrderBy(count => count)];

        var countGoodTile = tileTree.Count() - listCount.Where(count => count < 100).Count();

        return countGoodTile;
    }
}
