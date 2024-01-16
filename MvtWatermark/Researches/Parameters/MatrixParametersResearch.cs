using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Algorithm.Match;
using NetTopologySuite.IO.VectorTiles;
using Researches.Distortion;
using System.Collections;
using System.Collections.Concurrent;

namespace Researches.Parameters;
public class MatrixParametersResearch(QimMvtWatermarkOptions options)
{
    public QimMvtWatermarkOptions Options { get; set; } = options;

    public void Run(VectorTileTree tileTree, string path)
    {
        var valuesR = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15, 20, 25, 30, 40, 50 };
        var valuesNb = new int[] { 1, 2, 3, 4, 5, 8, 10, 15, 20, 30, 50, 100 };
        var valuesT2 = new double[] { 0.01, 0.025, 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 0.99 };
        var valuesK = new double[] { /*0.01, 0.025, 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8,*/ 0.9, 0.95, 0.99 };

        var valuesExtent = new int[] { 256, 512, 1024, 2048, 4096, 8192, 16384 };


        //Compute(tileTree, valuesK, valuesT2, Path.Combine(path, "k+t2 4096"), TestType.KAndT2);
        //Compute(tileTree, valuesNb, valuesR, Path.Combine(path, "nb+r 4096.txt"), TestType.NbAndR);
        Compute(tileTree, valuesK, valuesExtent, Path.Combine(path, "k+extent"), TestType.KAndExtent);
    }

    public void Compute<T, U>(VectorTileTree tileTree, IEnumerable<T> values1, IEnumerable<U> values2, string path, TestType testType)
    {
        var matrixAccuracy = new ConcurrentDictionary<(T, U), double>();
        var matrixF = new ConcurrentDictionary<(T, U), double>();
        var matrixH = new ConcurrentDictionary<(T, U), double>();
        foreach (var value1 in values1)
        {
            Console.WriteLine($"{value1}");
            foreach (var value2 in values2)
            {
                Console.WriteLine($"\t{value2}");
                //var accuracy = ComputeAccuracy(value1, value2, tileTree, testType);
                (var accuracy, var h, var f) = ComputeAccuracy(value1, value2, tileTree, testType);
                matrixAccuracy[(value1, value2)] = accuracy;
                matrixF[(value1, value2)] = f;
                matrixH[(value1, value2)] = h;
            }
        }

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        WriteMatrixToFile(matrixAccuracy, Path.Combine(path, "accuracy.txt"), values1, values2, testType);
        WriteMatrixToFile(matrixF, Path.Combine(path, "f.txt"), values1, values2, testType);
        WriteMatrixToFile(matrixH, Path.Combine(path, "h.txt"), values1, values2, testType);
    }

    public static void WriteMatrixToFile<T, U>(IDictionary<(T, U), double> matrix, string path, IEnumerable<T> values1, IEnumerable<U> values2, TestType testType)
    {
        using var writer = new StreamWriter(path);

        switch (testType)
        {
            case TestType.NbAndR:
                writer.Write(" nb/r   ");
                break;
            case TestType.KAndT2:
                writer.Write(" k/t2   ");
                break;
            case TestType.KAndExtent:
                writer.Write(" k/extent");
                break;
        };


        foreach (var value in values2)
        {
            switch (testType)
            {
                case TestType.NbAndR:
                    writer.Write($"{value,-8: ###}");
                    break;
                case TestType.KAndT2:
                    writer.Write($"{value,-8: 0.###}");
                    break;
                case TestType.KAndExtent:
                    writer.Write($"{value,-8: #####}");
                    break;
            };
        }

        foreach (var value1 in values1)
        {
            switch (testType)
            {
                case TestType.NbAndR:
                    writer.Write($"\n{value1,-8: ###}");
                    break;
                case TestType.KAndT2:
                    writer.Write($"\n{value1,-8: 0.###}");
                    break;
                case TestType.KAndExtent:
                    writer.Write($"\n {value1,-8: 0.###}");
                    break;
            };

            foreach (var value2 in values2)
            {
                writer.Write($"{matrix[(value1, value2)],-8: 0.####}");
            }
        }
    }

    public (double, double, double) ComputeAccuracy<T, U>(T value1, U value2, VectorTileTree tileTree, TestType testType)
    {

        var options = new QimMvtWatermarkOptions(Options);
        switch (testType)
        {
            case TestType.NbAndR:
                Options.R = Convert.ToInt32(value2);
                Options.Nb = Convert.ToInt32(value1);
                Options.M = (int)Math.Ceiling(Math.Sqrt(Options.R * Options.Nb));
                break;
            case TestType.KAndT2:
                options.T2 = Convert.ToDouble(value2);
                options.Delta2 = Convert.ToDouble(value1) * Options.T2;
                break;
            case TestType.KAndExtent:
                options.Delta2 = Convert.ToDouble(value1) * Options.T2;
                options.Extent = Convert.ToInt32(value2);
                break;
        };


        var watermark = new QimMvtWatermark(options);

        var bits = new bool[tileTree.Count() * options.Nb];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        VectorTileTree tileTreeWatermarked;
        try
        {
            tileTreeWatermarked = watermark.Embed(tileTree, 0, message);
            Data.WriteToFile(tileTreeWatermarked, Path.Combine("TmpTiles", "TmpFolder", $"{value1}, {value2}"));
        }
        catch (Exception)
        {
            return (0, 0, 0);
        }

        var tileTreeFromFiles = Data.ReadFromFiles(Path.Combine("TmpTiles", "TmpFolder", $"{value1}, {value2}"));

        var m = watermark.Extract(tileTreeFromFiles, 0);

        var countEqual = 0;

        for (var i = 0; i < m.Count; i++)
            if (m[i] == message[i % message.Count])
            {
                countEqual++;
            }

        var hausdorffSimilarityMeasure = new HausdorffSimilarityMeasure();
        var frechetSimilarityMeasure = new FrechetSimilarityMeasure();
        var listH = new List<double>();
        var listF = new List<double>();
        foreach (var id in tileTree)
        {
            for (var i = 0; i < tileTree[id].Layers.Count; i++)
                for (var j = 0; j < tileTree[id].Layers[i].Features.Count; j++)
                {
                    var layer = tileTreeFromFiles[id].Layers[i];

                    foreach (var feature in layer.Features)
                    {
                        if (feature.Attributes["GLOBALID"].ToString() == tileTree[id].Layers[i].Features[j].Attributes["GLOBALID"].ToString() && feature.Geometry.OgcGeometryType == tileTree[id].Layers[i].Features[j].Geometry.OgcGeometryType)
                        {
                            var h = hausdorffSimilarityMeasure.Measure(feature.Geometry, tileTree[id].Layers[i].Features[j].Geometry);
                            var f = frechetSimilarityMeasure.Measure(feature.Geometry, tileTree[id].Layers[i].Features[j].Geometry);
                            listH.Add(double.IsNegativeInfinity(h) ? 0 : h);
                            listF.Add(double.IsNegativeInfinity(f) ? 0 : f);
                            continue;
                        }
                    }
                }
        }

        return ((double)countEqual / m.Count, listH.Average(), listF.Average());
    }
}
