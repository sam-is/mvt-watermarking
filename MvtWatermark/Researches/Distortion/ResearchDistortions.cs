using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Researches.Distortion;
public class ResearchDistortions
{
    static public void Start(VectorTileTree tileTree, string path)
    {
        var valuesExtent = new int[] { 256, 512, 1024, 2048, 4096, 8192, 16384 };
        var valuesR = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15, 20, 25, 30, 40, 50 };
        var valuesT2 = new double[] { 0.01, 0.025, 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 0.99 };
        var valuesK = new double[] { 0.01, 0.025, 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 0.99 };
        var valuesNb = new int[] { 1, 2, 3, 4, 5, 8, 10, 15, 20, 30, 50, 100 };

        Compute(tileTree, valuesNb, valuesR, Path.Combine(path, "nb+r"), TestType.NbAndR);
        Compute(tileTree, valuesK, valuesT2, Path.Combine(path, "k+t2"), TestType.KAndT2);
        Compute(tileTree, valuesK, valuesExtent, Path.Combine(path, "k+extent"), TestType.KAndExtent);
    }

    static public void StartWithKey(VectorTileTree tileTree, string path)
    {
        var valuesR = new int[] { 1, 2, 3, 4, 5, 6 };
        var valuesT2 = new double[] { 0.2, 0.3, 0.4, 0.5 };
        var valuesK = new double[] { 0.2, 0.3, 0.4, 0.5 };
        var valuesNb = new int[] { 1, 2, 3, 4, 5, 8 };

        var countKeys = 20;
        var nbr = new List<double>[,,]?[countKeys];
        var kt2 = new List<double>[,,]?[countKeys];

        for (var i = 0; i < countKeys; i++)
        {
            var key = i * 237 + 28;
            Console.WriteLine(key);
            var accuracynbr = ComputeWithKey(tileTree, valuesNb, valuesR, Path.Combine(path, $"key = {key}", "nb, r"), TestType.NbAndR, key);
            if (accuracynbr == null)
                return;
            nbr[i] = accuracynbr;

            var accuracykt2 = ComputeWithKey(tileTree, valuesK, valuesT2, Path.Combine(path, $"key = {key}", "k, t2"), TestType.KAndT2, key);
            if (accuracykt2 == null)
                return;
            kt2[i] = accuracykt2;
        }

        var pathForDistortion = Path.Combine(path, "nb, r");
        if (!Directory.Exists(pathForDistortion))
            Directory.CreateDirectory(pathForDistortion);

        WriteDispersion(nbr, valuesNb, valuesR, countKeys, pathForDistortion);

        pathForDistortion = Path.Combine(path, "k, t2");
        if (!Directory.Exists(pathForDistortion))
            Directory.CreateDirectory(pathForDistortion);

        WriteDispersion(kt2, valuesK, valuesT2, countKeys, pathForDistortion);
    }

    static public void WriteDispersion(List<double>[,,]?[] accuracy, IList values1, IList values2, int countKeys, string path)
    {
        var mean = new List<double>[values1.Count, values2.Count, 5];
        for (var key = 0; key < countKeys; key++)
        {
            for (var i = 0; i < values1.Count; i++)
            {
                for (var j = 0; j < values2.Count; j++)
                {
                    for (var dist = 0; dist < 5; dist++)
                        for (var s = 0; s < 10; s++)
                        {
                            mean[i, j, dist] = new List<double> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                            mean[i, j, dist][s] += accuracy[key]![i, j, dist][s];
                        }
                }
            }
        }

        for (var i = 0; i < values1.Count; i++)
        {
            for (var j = 0; j < values2.Count; j++)
            {
                for (var dist = 0; dist < 5; dist++)
                    for (var s = 0; s < 10; s++)
                        mean[i, j, dist][s] /= countKeys;
            }
        }

        var dispersion = new List<double>[values1.Count, values2.Count, 5];
        for (var key = 0; key < countKeys; key++)
        {
            for (var i = 0; i < values1.Count; i++)
            {
                for (var j = 0; j < values2.Count; j++)
                {
                    for (var dist = 0; dist < 5; dist++)
                        for (var s = 0; s < 10; s++)
                        {
                            dispersion[i, j, dist] = new List<double> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                            dispersion[i, j, dist][s] += Math.Pow(accuracy[key]![i, j, dist][s] - mean[i, j, dist][s], 2);
                        }
                }
            }
        }

        for (var i = 0; i < values1.Count; i++)
        {
            for (var j = 0; j < values2.Count; j++)
            {
                for (var dist = 0; dist < 5; dist++)
                    for (var s = 0; s < 10; s++)
                        dispersion[i, j, dist][s] /= countKeys;
            }
        }


        for (var i = 0; i < values1.Count; i++)
        {
            for (var j = 0; j < values2.Count; j++)
            {
                using var writer = new StreamWriter(Path.Combine(path, $"{values1[i]}, {values2[j]}.txt"));
                writer.Write("ShiftingPoints  ReducingNumberOfPoints  DeletingLayers  DeletingByArea  AddingNewGeometries\n");
                for (var s = 0; s < 10; s++)
                {
                    for (var dist = 0; dist < 5; dist++)
                        writer.Write($"{dispersion[i, j, dist][s],-8: ##0.###}");
                    writer.WriteLine();
                }
            }
        }

    }

    static public void Compute(VectorTileTree tileTree, IEnumerable firstValues, IEnumerable secondValues, string filePath, TestType testType)
    {
        string metricFilename;
        string startString;
        string filename;
        switch (testType)
        {
            case TestType.NbAndR:
                metricFilename = "metric nb, r.txt";
                startString = " nb/r  ";
                filename = "r nb={0}.txt";
                break;
            case TestType.KAndT2:
                metricFilename = "metric k, t2.txt";
                startString = " k/t2 ";
                filename = "t2 k={0}.txt";
                break;
            case TestType.KAndExtent:
                metricFilename = "metric k, extent.txt";
                startString = " k/ext";
                filename = "extent k={0}.txt";
                break;
            default: return;
        }

        if (!Directory.Exists(filePath))
            Directory.CreateDirectory(filePath);

        using (var metricsWriter = new StreamWriter(Path.Combine(filePath, metricFilename)))
        {
            metricsWriter.Write(startString);
            foreach (var value in secondValues)
            {
                switch (testType)
                {
                    case TestType.NbAndR:
                        metricsWriter.Write($"{value,-6: ###}");
                        break;
                    case TestType.KAndT2:
                        metricsWriter.Write($"{value,-6: 0.###}");
                        break;
                    case TestType.KAndExtent:
                        metricsWriter.Write($"{value,-6: #####}");
                        break;
                }
            }
        }

        switch (testType)
        {
            case TestType.NbAndR:
                Console.WriteLine("nb and r");
                break;
            case TestType.KAndT2:
                Console.WriteLine("k and t2");
                break;
            case TestType.KAndExtent:
                Console.WriteLine("k and extent");
                break;
        }

        foreach (var firstValue in firstValues)
        {
            Console.WriteLine($"first value = {firstValue}");
            using var writer = new StreamWriter(Path.Combine(filePath, string.Format(filename, firstValue)));
            writer.Write($"Count tiles: {tileTree.Count()}\n");
            writer.Write("ShiftingPoints  ReducingNumberOfPoints  DeletingLayers  DeletingByArea  AddingNewGeometries\n\n");

            var metrics = new List<double>();
            foreach (var secondValue in secondValues)
            {
                Console.WriteLine($"    second value = {secondValue}");
                switch (testType)
                {
                    case TestType.NbAndR:
                        {
                            var options = new QimMvtWatermarkOptions(0.95, 0.2, 5, 4096, 2, (int)firstValue, (int)secondValue, null, false);
                            var (metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding) = TestWithMetric.Run(tileTree, options, 0);
                            metrics.Add(metric);
                            WriteToFile(writer, options, tileTree.Count(), metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding);
                            break;
                        }
                    case TestType.KAndT2:
                        {
                            var options = new QimMvtWatermarkOptions((double)firstValue, (double)secondValue, 5, 4096, 2, 5, 20, null, false);
                            var (metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding) = TestWithMetric.Run(tileTree, options, 0);
                            metrics.Add(metric);
                            WriteToFile(writer, options, tileTree.Count(), metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding);
                            break;
                        }
                    case TestType.KAndExtent:
                        {
                            var options = new QimMvtWatermarkOptions((double)firstValue, 0.2, 5, (int)secondValue, 2, 5, 20, null, false);
                            var (metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding) = TestWithMetric.Run(tileTree, options, 0);
                            metrics.Add(metric);
                            WriteToFile(writer, options, tileTree.Count(), metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding);
                            break;
                        }
                }
            }

            using var metricsWriter = new StreamWriter(Path.Combine(filePath, metricFilename), true);
            switch (testType)
            {
                case TestType.NbAndR:
                    metricsWriter.Write($"\n{(int)firstValue,-6: ###}");
                    break;
                case TestType.KAndT2:
                    metricsWriter.Write($"\n{(double)firstValue,-6: 0.###}");
                    break;
                case TestType.KAndExtent:
                    metricsWriter.Write($"\n{(double)firstValue,-6: 0.###}");
                    break;
            }

            foreach (var metric in metrics)
                metricsWriter.Write($"{metric,-6: 0.##}");
        }
    }

    static public void WriteToFile(TextWriter writer, QimMvtWatermarkOptions options, int countTiles, double metric, List<double> accuracyShifting, List<double> accuracyReducing, List<double> accuracyDeletingLayers, List<double> accuracyDeletingByArea, List<double> accuracyAdding)
    {
        writer.Write($"\n\nk: {options.Delta2 / options.T2: 0.#####}, " +
                            $"T2: {options.T2}, T1: {options.T1}, " +
                            $"Extent: {options.Extent}, Distance: {options.Distance}, " +
                            $"Nb: {options.Nb}, R: {options.R}, M: {options.M}, Length message: {options.Nb * countTiles}, Metric: {metric: 0.###}\n");

        for (var i = 0; i < 10; i++)
        {
            writer.Write($"{accuracyShifting[i],-6: 0.####} {accuracyReducing[i],-6: 0.####} {accuracyDeletingLayers[i],-6: 0.####} {accuracyDeletingByArea[i],-6: 0.####} {accuracyAdding[i],-6: 0.####}\n");
        }
    }

    static public List<double>[,,]? ComputeWithKey(VectorTileTree tileTree, ICollection firstValues, ICollection secondValues, string filePath, TestType testType, int key)
    {
        string metricFilename;
        string startString;
        string filename;

        var accuracyForAll = new List<double>[firstValues.Count, secondValues.Count, 5];
        switch (testType)
        {
            case TestType.NbAndR:
                metricFilename = "metric nb, r.txt";
                startString = " nb/r  ";
                filename = "r nb={0}.txt";
                break;
            case TestType.KAndT2:
                metricFilename = "metric k, t2.txt";
                startString = " k/t2 ";
                filename = "t2 k={0}.txt";
                break;
            case TestType.KAndExtent:
                metricFilename = "metric k, extent.txt";
                startString = " k/ext";
                filename = "extent k={0}.txt";
                break;
            default: return null;
        }

        if (!Directory.Exists(filePath))
            Directory.CreateDirectory(filePath);

        using (var metricsWriter = new StreamWriter(Path.Combine(filePath, metricFilename)))
        {
            metricsWriter.Write(startString);
            foreach (var value in secondValues)
            {
                switch (testType)
                {
                    case TestType.NbAndR:
                        metricsWriter.Write($"{value,-6: ###}");
                        break;
                    case TestType.KAndT2:
                        metricsWriter.Write($"{value,-6: 0.###}");
                        break;
                    case TestType.KAndExtent:
                        metricsWriter.Write($"{value,-6: #####}");
                        break;
                }
            }
        }

        switch (testType)
        {
            case TestType.NbAndR:
                Console.WriteLine("nb and r");
                break;
            case TestType.KAndT2:
                Console.WriteLine("k and t2");
                break;
            case TestType.KAndExtent:
                Console.WriteLine("k and extent");
                break;
        }

        var extrenalIteration = 0;
        foreach (var firstValue in firstValues)
        {
            Console.WriteLine($"first value = {firstValue}");
            using var writer = new StreamWriter(Path.Combine(filePath, string.Format(filename, firstValue)));
            writer.Write($"Count tiles: {tileTree.Count()}\n");
            writer.Write("ShiftingPoints  ReducingNumberOfPoints  DeletingLayers  DeletingByArea  AddingNewGeometries\n\n");

            var metrics = new List<double>();
            var iteration = 0;
            foreach (var secondValue in secondValues)
            {
                Console.WriteLine($"    second value = {secondValue}");
                switch (testType)
                {
                    case TestType.NbAndR:
                        {
                            var options = new QimMvtWatermarkOptions(0.95, 0.2, 5, 4096, 2, (int)firstValue, (int)secondValue, null, false);
                            var (metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding) = TestWithMetric.Run(tileTree, options, key);
                            metrics.Add(metric);
                            accuracyForAll[extrenalIteration, iteration, 0] = accuracyShifting;
                            accuracyForAll[extrenalIteration, iteration, 1] = accuracyReducing;
                            accuracyForAll[extrenalIteration, iteration, 2] = accuracyDeletingLayers;
                            accuracyForAll[extrenalIteration, iteration, 3] = accuracyDeletingByArea;
                            accuracyForAll[extrenalIteration, iteration, 4] = accuracyAdding;
                            WriteToFile(writer, options, tileTree.Count(), metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding);
                            break;
                        }
                    case TestType.KAndT2:
                        {
                            var options = new QimMvtWatermarkOptions((double)firstValue, (double)secondValue, 5, 4096, 2, 5, 20, null, false);
                            var (metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding) = TestWithMetric.Run(tileTree, options, key);
                            metrics.Add(metric);
                            accuracyForAll[extrenalIteration, iteration, 0] = accuracyShifting;
                            accuracyForAll[extrenalIteration, iteration, 1] = accuracyReducing;
                            accuracyForAll[extrenalIteration, iteration, 2] = accuracyDeletingLayers;
                            accuracyForAll[extrenalIteration, iteration, 3] = accuracyDeletingByArea;
                            accuracyForAll[extrenalIteration, iteration, 4] = accuracyAdding;
                            WriteToFile(writer, options, tileTree.Count(), metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding);
                            break;
                        }
                    case TestType.KAndExtent:
                        {
                            var options = new QimMvtWatermarkOptions((double)firstValue, 0.2, 5, (int)secondValue, 2, 5, 20, null, false);
                            var (metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding) = TestWithMetric.Run(tileTree, options, key);
                            metrics.Add(metric);
                            accuracyForAll[extrenalIteration, iteration, 0] = accuracyShifting;
                            accuracyForAll[extrenalIteration, iteration, 1] = accuracyReducing;
                            accuracyForAll[extrenalIteration, iteration, 2] = accuracyDeletingLayers;
                            accuracyForAll[extrenalIteration, iteration, 3] = accuracyDeletingByArea;
                            accuracyForAll[extrenalIteration, iteration, 4] = accuracyAdding;
                            WriteToFile(writer, options, tileTree.Count(), metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding);
                            break;
                        }
                }
                iteration++;
            }

            using var metricsWriter = new StreamWriter(Path.Combine(filePath, metricFilename), true);
            switch (testType)
            {
                case TestType.NbAndR:
                    metricsWriter.Write($"\n{(int)firstValue,-6: ###}");
                    break;
                case TestType.KAndT2:
                    metricsWriter.Write($"\n{(double)firstValue,-6: 0.###}");
                    break;
                case TestType.KAndExtent:
                    metricsWriter.Write($"\n{(double)firstValue,-6: 0.###}");
                    break;
            }

            foreach (var metric in metrics)
                metricsWriter.Write($"{metric,-6: 0.##}");

            extrenalIteration++;
        }

        return accuracyForAll;
    }
}
