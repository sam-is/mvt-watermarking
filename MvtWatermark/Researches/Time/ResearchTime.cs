using Microsoft.Data.Sqlite;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;
using System.Data;

namespace Researches.Time;
public class ResearchTime
{
    static public void StartStp(string pathDb, int minZ, int maxZ, int nb, int r, string path)
    {
        path = Path.Combine(path, $"nb = {nb}, r = {r}");

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var options = new QimMvtWatermarkOptions(0.8, 0.2, 10, 4096, 2, nb, r, null);
        for (var z = minZ; z <= maxZ; z++)
        {
            string pathDbZoom;
            if (z < 13)
                pathDbZoom = Path.Combine(pathDb, "stp0-12zoom.mbtiles");
            else
                pathDbZoom = Path.Combine(pathDb, $"stp{z}zoom.mbtiles");

            var (minX, maxX, minY, maxY) = Data.GetMinMax(pathDbZoom, z);
            var tileTree = Data.GetStpVectorTileTree(pathDbZoom, minX, maxX, minY, maxY, z);
            Compute(tileTree, z, options, path);
        }
    }

    static public void StartTegola(int minZ, int maxZ, int nb, int r, string path)
    {
        path = Path.Combine(path, $"nb = {nb}, r = {r}");

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var options = new QimMvtWatermarkOptions(0.8, 0.2, 5, 4096, 2, nb, r, null);
        for (var z = minZ; z <= maxZ; z++)
        {
            var minX = 0;
            var minY = 0;
            var maxX = 1 << z;
            var maxY = 1 << z;
            Console.WriteLine($"z = {z}     Get tiles: {DateTime.Now}");
            var tileTree = Data.GetTegolaVectorTileTree(minX, maxX, minY, maxY, z);
            Console.WriteLine($"z = {z}     Geted tiles: {DateTime.Now}");
            Compute(tileTree, z, options, path);
        }
    }
    static public void Compute(VectorTileTree tileTree, int z, QimMvtWatermarkOptions options, string path)
    {
        using var writer = new StreamWriter(Path.Combine(path, $"{z} zoom.txt"));

        writer.WriteLine($"Count tiles: {tileTree.Count()}");
        writer.WriteLine($"Count good tile: {GetCountGoodTile(tileTree)}");

        var watch = new System.Diagnostics.Stopwatch();

        var bits = new bool[1];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        options.Mode = Mode.Repeat;
        var watermark = new QimMvtWatermark(options);

        watch.Start();
        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);
        watch.Stop();

        Console.WriteLine("Embeded");
        writer.WriteLine($"Embeding: {watch.ElapsedMilliseconds} ms = {watch.Elapsed.TotalMinutes} min");

        watch.Restart();
        var m = watermark.Extract(tileTreeWatermarked, 0);
        watch.Stop();

        Console.WriteLine("Extracted");
        writer.WriteLine($"Extracting: {watch.ElapsedMilliseconds} ms = {watch.Elapsed.TotalMinutes} min");

        var count = 0;
        for (var i = 0; i < m.Count; i++)
            if (m[i] != message[i % message.Count])
                count++;

        writer.WriteLine($"Length all messages: {m.Count}\nLength message: {message.Count}\nCount not correct bit: {count}");
    }

    public static int GetCountGoodTile(VectorTileTree tileTree)
    {
        var listCount = new List<int>();

        foreach (var tileId in tileTree)
            Console.WriteLine(tileId);

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
