using Distortion;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Researches.Distortion;
public class TestWithMetric
{
    public static (double, List<double>, List<double>, List<double>, List<double>, List<double>) Run(VectorTileTree tileTree, QimMvtWatermarkOptions qimMvtWatermarkOptions, int key)
    {
        var watermark = new QimMvtWatermark(qimMvtWatermarkOptions);

        var bits = new bool[qimMvtWatermarkOptions.Nb * tileTree.Count()];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var tileTreeWatermarked = watermark.Embed(tileTree, key, message);

        var valuesRelativeDouble = new double[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1 };
        var valuesDeletingByArea = new double[] { 0.00001, 0.0001, 0.001, 0.005, 0.01, 0.05, 0.1, 0.2, 0.3, 0.4 };
        var valuesAdding = new double[] { 50, 85, 120, 155, 190, 225, 260, 295, 330, 365 };

        var metric = 0.0;

        var accuracyShifting = ComputeAccuracyAfterDistortion(valuesRelativeDouble, DistortionType.ShiftingPoints, tileTreeWatermarked, message, watermark, key);
        metric += accuracyShifting.Sum();

        var accuracyReducing = ComputeAccuracyAfterDistortion(valuesRelativeDouble, DistortionType.ReducingNumberOfPoints, tileTreeWatermarked, message, watermark, key);
        metric += accuracyReducing.Sum();

        var accuracyDeletingLayers = ComputeAccuracyAfterDistortion(valuesRelativeDouble, DistortionType.DeletingLayers, tileTreeWatermarked, message, watermark, key);
        metric += accuracyDeletingLayers.Sum();

        var accuracyDeletingByArea = ComputeAccuracyAfterDistortion(valuesDeletingByArea, DistortionType.DeletingByArea, tileTreeWatermarked, message, watermark, key);
        metric += accuracyDeletingByArea.Sum();

        var accuracyAdding = ComputeAccuracyAfterDistortion(valuesAdding, DistortionType.AddingNewGeometries, tileTreeWatermarked, message, watermark, key);
        metric += accuracyAdding.Sum();

        return (metric, accuracyShifting, accuracyReducing, accuracyDeletingLayers, accuracyDeletingByArea, accuracyAdding);
    }

    private static List<double> ComputeAccuracyAfterDistortion(IEnumerable values, DistortionType distortionType, VectorTileTree tileTreeWatermarked, BitArray message, QimMvtWatermark watermark, int key)
    {
        var accuracy = new List<double>();
        foreach (var value in values)
        {
            IDistortion distortion;
            switch (distortionType)
            {
                case DistortionType.ShiftingPoints:
                    distortion = new ShiftingPointsDistortion((double)value);
                    accuracy.Add(ComputeAccuracy(tileTreeWatermarked, message, watermark, distortion, key));
                    break;
                case DistortionType.DeletingLayers:
                    distortion = new DeletingLayersDistortion((double)value);
                    accuracy.Add(ComputeAccuracy(tileTreeWatermarked, message, watermark, distortion, key));
                    break;
                case DistortionType.DeletingByArea:
                    distortion = new DeletingByAreaDistortion((double)value);
                    accuracy.Add(ComputeAccuracy(tileTreeWatermarked, message, watermark, distortion, key));
                    break;
                case DistortionType.AddingNewGeometries:
                    distortion = new AddingNewGeometriesDistortion(Convert.ToInt32(value));
                    accuracy.Add(ComputeAccuracy(tileTreeWatermarked, message, watermark, distortion, key));
                    break;
                case DistortionType.ReducingNumberOfPoints:
                    distortion = new ReducingNumberOfPointsDistortion((double)value, true);
                    accuracy.Add(ComputeAccuracy(tileTreeWatermarked, message, watermark, distortion, key));
                    break;
            }
        }
        return accuracy;
    }

    private static double ComputeAccuracy(VectorTileTree tileTreeWatermarked, BitArray message, QimMvtWatermark watermark, IDistortion distortion, int key)
    {
        var distortingTileTree = distortion.Distort(tileTreeWatermarked);

        var m = watermark.Extract(distortingTileTree, key);

        double accuracy;

        if (m.Count != 0)
        {
            var countEqual = 0;
            for (var i = 0; i < message.Count && i < m.Count; i++)
                if (m[i] == message[i])
                    countEqual++;
            accuracy = (double)countEqual / message.Count;
        }
        else
            accuracy = 0;

        return accuracy;
    }
}
