using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Algorithm.Match;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Researches.Parameters;

public class CheckParameters(QimMvtWatermarkOptions options)
{
    public QimMvtWatermarkOptions Options { get; set; } = options;

    public enum ParamName
    {
        T2,
        K,
        Distance,
        T1,
        R,
        Extent
    }

    public void Run(VectorTileTree tileTree, string path)
    {

        var valuesT2AndK = new double[] { 0.01, 0.05, 0.08, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 0.98, 1 };
        var valuesT1 = new double[] { 1, 2, 3, 5, 8, 10, 15, 20, 30, 40, 50, 75, 100, 200, 300, 500, 1000 };
        var valuesDistance = new double[] { 1, 2, 3 };
        var valuesR = new double[] { 1, 2, 3, 5, 8, 10, 15, 20, 40, 60, 80, 100, 150, 200 };
        var valuesExtent = new double[] { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };

        using var streamWriter = new StreamWriter(path);

        streamWriter.Write($"Default options:\nk: {Options.Delta2 / Options.T2}, t2: {Options.T2}, t1: {Options.T1}, extent: {Options.Extent}, distance: {Options.Distance}, nb: {Options.Nb}, r: {Options.R}, m: {Options.M}\n\n\n");

        var t2 = Compute(tileTree, ParamName.T2, valuesT2AndK);
        WriteToFile(streamWriter, valuesT2AndK, t2, nameof(ParamName.T2));

        var k = Compute(tileTree, ParamName.K, valuesT2AndK);
        WriteToFile(streamWriter, valuesT2AndK, k, nameof(ParamName.K));

        var t1 = Compute(tileTree, ParamName.T1, valuesT1);
        WriteToFile(streamWriter, valuesT1, t1, nameof(ParamName.T1));

        var distance = Compute(tileTree, ParamName.Distance, valuesDistance);
        WriteToFile(streamWriter, valuesDistance, distance, nameof(ParamName.Distance));

        var r = Compute(tileTree, ParamName.R, valuesR);
        WriteToFile(streamWriter, valuesR, r, nameof(ParamName.R));

        var extent = Compute(tileTree, ParamName.Extent, valuesExtent);
        WriteToFile(streamWriter, valuesExtent, extent, nameof(ParamName.Extent));
    }

    public static void WriteToFile(TextWriter textWriter, IReadOnlyList<double> values, Measures measure, string name)
    {
        textWriter.Write($"{name}\n");
        textWriter.Write($"{"value",-4}\t{"accuracy",-8}\t{"avg Hausdorff",-12}\t{"avg Frechet",-12}\n");

        for (var i = 0; i < values.Count; i++)
            textWriter.Write($"{values[i],-4}\t{measure.Accuracy![i],-8}\t{measure.AvgHausdorff![i],-12:f7}\t{measure.AvgFrechet![i],-12:f7}\n");

        textWriter.Write("\n\n\n");
    }

    public Measures Compute(VectorTileTree tileTree, ParamName paramName, IEnumerable values)
    {
        var avgH = new List<double>();
        var avgF = new List<double>();
        var accuracy = new List<double>();

        foreach (double value in values)
        {
            var options = new QimMvtWatermarkOptions(
                Options!.Delta2 / Options.T2,
                Options.T2,
                Options.T1,
                Options.Extent,
                Options.Distance,
                Options.Nb,
                Options.R,
                Options.M,
                Options.IsGeneralExtractionMethod
                );

            switch (paramName)
            {
                case ParamName.T1:
                    options.T1 = Convert.ToInt32(value);
                    break;
                case ParamName.T2:
                    var k = options.Delta2 / options.T2;
                    options.T2 = value;
                    if (value * k + value > 1)
                    {
                        k = (1 - value) / value;
                        options.Delta2 = k * value;
                    }
                    break;
                case ParamName.K:
                    options.Delta2 = value * options.T2;
                    break;
                case ParamName.Distance:
                    options.Distance = Convert.ToInt32(value);
                    break;
                case ParamName.Extent:
                    options.Extent = Convert.ToInt32(value);
                    break;
                case ParamName.R:
                    options.R = Convert.ToInt32(value);
                    options.M = (int)Math.Ceiling(Math.Sqrt(Options.Nb * options.R));
                    break;
            }

            var watermark = new QimMvtWatermark(options);

            var bits = new bool[1];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = true;
            var message = new BitArray(bits);

            VectorTileTree tileTreeWatermarked;
            try
            {
                tileTreeWatermarked = watermark.Embed(tileTree, 0, message);
                Data.WriteToFile(tileTreeWatermarked, "TmpTiles");
            }
            catch (Exception)
            {
                accuracy.Add(0.0);
                avgF.Add(0.0);
                avgH.Add(0.0);
                continue;
            }

            var tileTreeFromFiles = Data.ReadFromFiles("TmpTiles");

            var m = watermark.Extract(tileTreeFromFiles, 0);

            var countEqual = 0;
            //for (var i = 0; i < message.Count; i++)
            //    if (m[i] == message[i])
            //        countEqual++;

            for (var i = 0; i < m.Count; i++)
                if (m[i] == message[i % message.Count])
                {
                    countEqual++;
                }

            accuracy.Add((double)countEqual / m.Count);

            var listH = new List<double>();
            var listF = new List<double>();
            var hausdorffSimilarityMeasure = new HausdorffSimilarityMeasure();
            var frechetSimilarityMeasure = new FrechetSimilarityMeasure();

            foreach (var id in tileTree)
            {
                for (var i = 0; i < tileTree[id].Layers.Count; i++)
                    for (var j = 0; j < tileTree[id].Layers[i].Features.Count; j++)
                    {
                        var layer = tileTreeFromFiles[id].Layers[i];

                        foreach(var feature in layer.Features)
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
                        //var h = hausdorffSimilarityMeasure.Measure(tileTreeFromFiles[id].Layers[i].Features[j].Geometry, tileTree[id].Layers[i].Features[j].Geometry);
                        //var f = frechetSimilarityMeasure.Measure(tileTreeFromFiles[id].Layers[i].Features[j].Geometry, tileTree[id].Layers[i].Features[j].Geometry);
                       
                        //listH.Add(double.IsNegativeInfinity(h) ? 0 : h);
                        //listF.Add(double.IsNegativeInfinity(f) ? 0 : f);
                    }
            }

            avgH.Add(listH.Average());
            avgF.Add(listF.Average());
        }
        return new Measures { Accuracy = accuracy, AvgHausdorff = avgH, AvgFrechet = avgF };
    }
}