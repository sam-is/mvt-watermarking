using Distortion;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Algorithm.Match;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Researches.Distortion;
public class Test
{
    public static void Run(VectorTileTree tileTree, QimMvtWatermarkOptions qimMvtWatermarkOptions, TextWriter writer)
    {
        var watermark = new QimMvtWatermark(qimMvtWatermarkOptions);

        var bits = new bool[qimMvtWatermarkOptions.Nb * tileTree.Count()];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);

        var valuesRelativeDouble = new double[] { 0.01, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1 };
        var valuesDeletingByArea = new double[] { 0.0001, 0.001, 0.005, 0.01, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1 };
        var valuesSeparation = new SeparationByGeometryTypeDistortion.Mode[] { SeparationByGeometryTypeDistortion.Mode.All,
                                                                      SeparationByGeometryTypeDistortion.Mode.Points,
                                                                      SeparationByGeometryTypeDistortion.Mode.Lines,
                                                                      SeparationByGeometryTypeDistortion.Mode.Polygons };

        var valuesAdding = new double[] { 1, 5, 10, 20, 30, 40, 50, 60, 70, 90, 100, 150, 200, 300, 400, 500 };

        writer.Write($"\n\nk: {qimMvtWatermarkOptions.Delta2 / qimMvtWatermarkOptions.T2}, " +
                     $"T2: {qimMvtWatermarkOptions.T2}, T1: {qimMvtWatermarkOptions.T1}, " +
                     $"Extent: {qimMvtWatermarkOptions.Extent}, Distance: {qimMvtWatermarkOptions.Distance}, " +
                     $"Nb: {qimMvtWatermarkOptions.Nb}, R: {qimMvtWatermarkOptions.R}, M: {qimMvtWatermarkOptions.M}, Length message: {message.Length}");

        CreateDistortionAndPrintresult(valuesRelativeDouble, DistortionType.ShiftingPoints, tileTreeWatermarked, message, watermark, writer);
        CreateDistortionAndPrintresult(valuesRelativeDouble, DistortionType.ReducingNumberOfPoints, tileTreeWatermarked, message, watermark, writer);
        CreateDistortionAndPrintresult(valuesRelativeDouble, DistortionType.DeletingLayers, tileTreeWatermarked, message, watermark, writer);
        CreateDistortionAndPrintresult(valuesDeletingByArea, DistortionType.DeletingByArea, tileTreeWatermarked, message, watermark, writer);
        CreateDistortionAndPrintresult(valuesSeparation, DistortionType.SeparationByGeometryType, tileTreeWatermarked, message, watermark, writer);
        CreateDistortionAndPrintresult(valuesAdding, DistortionType.AddingNewGeometries, tileTreeWatermarked, message, watermark, writer);

    }

    private static void CreateDistortionAndPrintresult(IEnumerable values, DistortionType distortionType, VectorTileTree tileTreeWatermarked, BitArray message, QimMvtWatermark watermark, TextWriter writer)
    {

        writer.Write($"\n\n{distortionType}\n\n");
        writer.Write($"{"value",-8}\t{"accuracy",-8}\t{"avg Hausdorff",-12}\t{"avg Frechet",-12}\n");
        foreach (var value in values)
        {
            IDistortion distortion;
            switch (distortionType)
            {
                case DistortionType.ShiftingPoints:
                    writer.Write($"\n{(double)value,-8}\t");
                    distortion = new ShiftingPointsDistortion((double)value);
                    PrintResult(tileTreeWatermarked, message, watermark, distortion, writer);
                    break;
                case DistortionType.DeletingLayers:
                    writer.Write($"\n{(double)value,-8}\t");
                    distortion = new DeletingLayersDistortion((double)value);
                    PrintResult(tileTreeWatermarked, message, watermark, distortion, writer);
                    break;
                case DistortionType.SeparationByGeometryType:
                    writer.Write($"\n{value,-8}\t");
                    distortion = new SeparationByGeometryTypeDistortion((SeparationByGeometryTypeDistortion.Mode)value);
                    PrintResult(tileTreeWatermarked, message, watermark, distortion, writer);
                    break;
                case DistortionType.DeletingByArea:
                    writer.Write($"\n {(double)value,-8}\t");
                    distortion = new DeletingByAreaDistortion((double)value);
                    PrintResult(tileTreeWatermarked, message, watermark, distortion, writer);
                    break;
                case DistortionType.AddingNewGeometries:
                    writer.Write($"\n {Convert.ToInt32(value),-8}\t");
                    distortion = new AddingNewGeometriesDistortion(Convert.ToInt32(value));
                    PrintResult(tileTreeWatermarked, message, watermark, distortion, writer);
                    break;
                case DistortionType.ReducingNumberOfPoints:
                    writer.Write($"\n {(double)value,-8}\t");
                    distortion = new ReducingNumberOfPointsDistortion((double)value, true);
                    PrintResult(tileTreeWatermarked, message, watermark, distortion, writer);
                    break;
            }
        }
    }

    private static void PrintResult(VectorTileTree tileTreeWatermarked, BitArray message, QimMvtWatermark watermark, IDistortion distortion, TextWriter writer)
    {
        var distortingTileTree = distortion.Distort(tileTreeWatermarked);

        var m = watermark.Extract(distortingTileTree, 0);

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

        writer.Write($"{accuracy,-8}\t");

        if (distortion.GetType() == typeof(ShiftingPointsDistortion) || distortion.GetType() == typeof(ReducingNumberOfPointsDistortion))
        {
            var listH = new List<double>();
            var listF = new List<double>();
            var hausdorffSimilarityMeasure = new HausdorffSimilarityMeasure();
            var frechetSimilarityMeasure = new FrechetSimilarityMeasure();

            foreach (var id in tileTreeWatermarked)
            {
                for (var i = 0; i < distortingTileTree[id].Layers.Count; i++)
                {
                    for (var k = 0; k < tileTreeWatermarked[id].Layers.Count; k++)
                        if (tileTreeWatermarked[id].Layers[k].Name == distortingTileTree[id].Layers[i].Name)
                            for (var j = 0; j < distortingTileTree[id].Layers[i].Features.Count; j++)
                            {
                                var h = hausdorffSimilarityMeasure.Measure(tileTreeWatermarked[id].Layers[k].Features[j].Geometry, distortingTileTree[id].Layers[i].Features[j].Geometry);
                                var f = frechetSimilarityMeasure.Measure(tileTreeWatermarked[id].Layers[k].Features[j].Geometry, distortingTileTree[id].Layers[i].Features[j].Geometry);
                                listH.Add(double.IsNegativeInfinity(h) || h < 0 ? 0 : h);
                                listF.Add(double.IsNegativeInfinity(f) || f < 0 ? 0 : f);
                            }
                }
            }

            writer.Write($"{(listH.Count == 0 ? 0 : listH.Average()),-12:f7}\t");
            writer.Write($"{(listF.Count == 0 ? 0 : listF.Average()),-12:f7}");
        }
    }
}
