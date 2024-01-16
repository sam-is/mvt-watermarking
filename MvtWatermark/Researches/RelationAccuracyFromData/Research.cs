using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Researches.RelationAccuracyFromData;
public class Reaserch
{
    public static void Start(VectorTileTree tileTree, string path)
    {
        var options = new QimMvtWatermarkOptions(0.9, 0.2, 5, 2048, 2, 1, 1, null, false);
        var values = new Tuple<int, int>[] { new(1, 1), new(4, 4), new(8, 8), new(16, 16), new(25,25), new(1, 25) /*new(1, 25), new(16, 16), new(20, 20), new (2, 8)*/ }.OrderBy(item => item.Item1).ThenBy(item => item.Item2).ToArray();

        Run(tileTree, options, path, values);
    }

    public static void Run(VectorTileTree tileTree, QimMvtWatermarkOptions options, string path, Tuple<int, int>[] values)
    {
        var matrix = new Dictionary<Tuple<Tuple<int, int>, Tuple<int, int>>, double>();
        foreach (var tileId in tileTree)
        {
            foreach (var (valueNb, valueR) in values)
            {
                var currentOptions = new QimMvtWatermarkOptions(options)
                {
                    Nb = valueNb,
                    R = valueR,
                    M = (int)Math.Ceiling(Math.Sqrt(valueR * valueNb))
                };
                var (countFeature, countPoints) = ComputeFeaturesAndPoints(tileTree[tileId]);
                var tmpVectorTileTree = new VectorTileTree
                {
                    [tileId] = tileTree[tileId]
                };
                var accuracy = ComputeAccuracy(tmpVectorTileTree, currentOptions);
                matrix[new(new(valueNb, valueR), new(countFeature, countPoints))] = accuracy;
            }
        }

        WriteResult(path, matrix, values);
    }

    public static void WriteResult(string path, Dictionary<Tuple<Tuple<int, int>, Tuple<int, int>>, double> matrix, Tuple<int, int>[] values)
    {
        using var writer = new StreamWriter(Path.Combine(path, "landplot 2048 z=12.txt"));
        writer.Write(" Features   Points    ");

        foreach (var (valueNb, valueR) in values)
        {
            writer.Write($"({valueNb}, {valueR})\t");
        }
        writer.WriteLine();

        var orderedMatrix = matrix.OrderBy(item => item.Key.Item2.Item1).ThenBy(item => item.Key.Item2.Item2).ThenBy(item => item.Key.Item1.Item1).ThenBy(item => item.Key.Item1.Item2);

        Tuple<int, int>? tmp = null;
        foreach (var item in orderedMatrix)
        {
            if (tmp == null)
            {
                tmp = item.Key.Item2;
                writer.Write($"{item.Key.Item2.Item1,-10: ########} {item.Key.Item2.Item2,-10: ########}");
            }
            else if (tmp.Item1 != item.Key.Item2.Item1 || tmp.Item2 != item.Key.Item2.Item2)
            {
                writer.WriteLine();
                tmp = item.Key.Item2;
                writer.Write($"{item.Key.Item2.Item1,-10: ########} {item.Key.Item2.Item2,-10: ########}");
            }

            writer.Write($"{item.Value,-10: 0.#####}");
        }
        var allCounts = matrix.GroupBy(item => (item.Key.Item1.Item1, item.Key.Item1.Item2)).Select(param => param.Count()).ToList();
        var counts = matrix.GroupBy(item => (item.Key.Item1.Item1, item.Key.Item1.Item2)).Select(param => param.Sum(p => p.Value == -1 ? 0 : p.Value)).ToList();

        writer.WriteLine();
        writer.Write("                     ");

        for (var i = 0; i < counts.Count; i++)
            writer.Write($"{(double)counts[i] / allCounts[i],-10: 0.#####}");
    }

    public static (int, int) ComputeFeaturesAndPoints(VectorTile tile)
    {
        var countFeature = 0;
        var countPoints = 0;
        foreach (var layer in tile.Layers)
        {
            foreach (var feature in layer.Features)
            {
                countFeature++;
                countPoints += feature.Geometry.NumPoints;
            }
        }
        return (countFeature, countPoints);
    }

    public static double ComputeAccuracy(VectorTileTree tileTree, QimMvtWatermarkOptions options)
    {
        var watermark = new QimMvtWatermark(options);

        var bits = new bool[1];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var path = Path.Combine("TmpTiles", "ForAccuracyResearch");

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        VectorTileTree tileTreeWatermarked;
        var directory = new DirectoryInfo(path);

        foreach (FileInfo file in directory.GetFiles())
            file.Delete();

        foreach (DirectoryInfo dir in directory.GetDirectories())
            dir.Delete(true);

        try
        {
            tileTreeWatermarked = watermark.Embed(tileTree, 0, message);
            Data.WriteToFile(tileTreeWatermarked, path);
        }
        catch (Exception)
        {
            return -1;
        }


        var tileTreeFromFiles = Data.ReadFromFiles(path);

        var m = watermark.Extract(tileTreeFromFiles, 0);
        if (m.Count == 0)
            return -1;

        var countEqual = 0;

        for (var i = 0; i < m.Count; i++)
            if (m[i] == message[i % message.Count])
            {
                countEqual++;
            }

        return (double)countEqual / m.Count;
    }
}
