using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Algorithm.Match;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace ParameterValues;

public class CheckParameters
{
    public QimMvtWatermarkOptions? Options { get; set; }

    public enum ParamName
    {
        T2,
        K,
        Distance,
        T1,
        R,
        Extent
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

            var bits = new bool[20];
            for (var i = 0; i < bits.Length; i++)
                bits[i] = true;
            var message = new BitArray(bits);

            VectorTileTree tileTreeWatermarked;
            try
            {
                tileTreeWatermarked = watermark.Embed(tileTree, 0, message);
            }
            catch (Exception)
            {
                accuracy.Add(0.0);
                avgF.Add(0.0);
                avgH.Add(0.0);
                continue;
            }

            var m = watermark.Extract(tileTreeWatermarked, 0);

            var countEqual = 0;
            for (var i = 0; i < message.Count; i++)
                if (m[i] == message[i])
                    countEqual++;

            accuracy.Add((double)countEqual / message.Count);

            var listH = new List<double>();
            var listF = new List<double>();
            var hausdorffSimilarityMeasure = new HausdorffSimilarityMeasure();
            var frechetSimilarityMeasure = new FrechetSimilarityMeasure();

            foreach (var id in tileTree)
            {
                for (var i = 0; i < tileTree[id].Layers.Count; i++)
                    for (var j = 0; j < tileTree[id].Layers[i].Features.Count; j++)
                    {
                        var h = hausdorffSimilarityMeasure.Measure(tileTreeWatermarked[id].Layers[i].Features[j].Geometry, tileTree[id].Layers[i].Features[j].Geometry);
                        var f = frechetSimilarityMeasure.Measure(tileTreeWatermarked[id].Layers[i].Features[j].Geometry, tileTree[id].Layers[i].Features[j].Geometry);
                        listH.Add(double.IsNegativeInfinity(h) ? 0 : h);
                        listF.Add(double.IsNegativeInfinity(f) ? 0 : f);
                    }
            }

            avgH.Add(listH.Average());
            avgF.Add(listF.Average());
        }
        return new Measures { Accuracy = accuracy, AvgHausdorff = avgH, AvgFrechet = avgF };
    }
}