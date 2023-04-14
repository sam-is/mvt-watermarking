using Distortion;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark;
using System.Collections;

namespace DistortionTry;
public static class DistortionTester
{
    public struct OptionsParamRanges
    {
        public int Mmin { get; set; }
        public int Mmax { get; set; }
        public int Nbmin { get; set; }
        public int Nbmax { get; set; }
        public int Lfmin { get; set; }
        public int Lfmax { get; set; }
        public int Lsmin { get; set; }
        public int Lsmax { get; set; }
    }
    public static async void DiffWatermarkParametersTest(List<CoordinateSet> parameterSetsStp, List<CoordinateSet> parameterSetsTegola, 
        OptionsParamRanges optionsParamRanges, BitArray message, string cataloguePath = "")
    {
        VectorTileTree vectorTileTree = TileSetCreator.GetVectorTileTree(parameterSetsStp, parameterSetsTegola);
        //VectorTileTree vectorTileTree = TileSetCreator.CreateRandomVectorTileTree(parameterSets);

        //var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var key = 123;

        var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var secondHalfOfLineStringIsUsed = false;
        await TestParametersInCycle(vectorTileTree, key, message, cataloguePath, "MtLtLt_false", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        secondHalfOfLineStringIsUsed = true;
        await TestParametersInCycle(vectorTileTree, key, message, cataloguePath, "MtLtLt_true", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        secondHalfOfLineStringIsUsed = false;
        await TestParametersInCycle(vectorTileTree, key, message, cataloguePath, "NLt_false", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        secondHalfOfLineStringIsUsed = true;    
        await TestParametersInCycle(vectorTileTree, key, message, cataloguePath, "NLt_true", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
    }

    public static async Task TestParametersInCycle(VectorTileTree vectorTileTree, int key, BitArray message, 
        string cataloguePath, string subPath, OptionsParamRanges optionsParamRanges, NoDistortionWatermarkOptions.AtypicalEncodingTypes aEtype, 
        bool secondHalfOfLineStringIsUsed)
    {
        for (var m = optionsParamRanges.Mmin; m <= optionsParamRanges.Mmax; m++)
        {
            for (var nb = optionsParamRanges.Nbmin; nb <= optionsParamRanges.Nbmax; nb++)
            {
                var logDirectory = $"..\\..\\..\\MatricesTests\\{cataloguePath}\\{subPath}";
                var dirInfo = new DirectoryInfo(logDirectory);
                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                }
                var fileName = logDirectory + $"\\Nb-{nb}_M-{m}_D-{Convert.ToInt32(2 * m * Math.Pow(2, nb))}.txt";
                using (var fileStream = new FileStream(fileName, FileMode.Create)) { }

                for (var lf = optionsParamRanges.Lfmin; lf <= optionsParamRanges.Lfmax; lf++)
                {
                    for (var ls = optionsParamRanges.Lsmin; ls <= optionsParamRanges.Lsmax; ls++)
                    {
                        var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, aEtype, secondHalfOfLineStringIsUsed);
                        await WriteMetricsMatrix(vectorTileTree, options, key, message, fileName);
                    }
                }
            }
        }
    }

    public static async Task WriteMetricsMatrix(VectorTileTree vectorTileTree, NoDistortionWatermarkOptions options,
        int key, BitArray message, string fileName)
    {
        using (var streamWriter = new StreamWriter(fileName, true))
        {
            var resultMatrix = new List<List<double>>();

            double param = 0;
            while (param <= 1)
            {
                var oneParamResultVector = new List<double>();

                var distortionWithParameterList = new List<IDistortion>() {
                    new DeletingLayersDistortion(param),
                    new DeletingByAreaDistortion(param),
                    new RemoverByPerimeter(param),
                    new ObjectsAdder(param),
                    new ObjectsRemover(param),
                    new ObjectsMagnifier(param),
                    new CoordinateOrderReverser(param),

                    new ShiftingPointsDistortion(param),

                    new ReducingNumberOfPointsDistortion(param, false),
                    new ReducingNumberOfPointsDistortion(param, true),
                };

                foreach (var distortion in distortionWithParameterList)
                {
                    var comparisonResult = TestSingleDistortion(vectorTileTree, distortion, options, key, message, param);
                    oneParamResultVector.Add(comparisonResult);
                }

                resultMatrix.Add(oneParamResultVector);

                param += 0.05;
            }

            double metric = 0;
            foreach (var vector in resultMatrix)
            {
                foreach (var value in vector)
                    metric += value;
            }

            var resultsCount = resultMatrix.Count * resultMatrix[0].Count;
            var relativeMetric = Math.Round(metric / resultsCount, 4);

            var optionsString = $"\nEncType: {options.AtypicalEncodingType} | D: {options.D} | M: {options.M} " +
                $"| Nb: {options.Nb} | Ls: {options.Ls} | Lf: {options.Lf} | SecondHalfUsed: {options.SecondHalfOfLineStringIsUsed}\n" +
                $"Metric: {Math.Round(metric, 3)}/{resultsCount} | Relative Metric: {relativeMetric}\n\n";
            var resultMatrixString = "";

            foreach (var vector in resultMatrix)
            {
                foreach(var value in vector)
                {
                    resultMatrixString += $"{Math.Round(value, 2)}\t";
                }
                resultMatrixString += "\n";
            }


            await streamWriter.WriteAsync(optionsString);
            await streamWriter.WriteAsync(resultMatrixString);
        }
    }

    private static double TestSingleDistortion(VectorTileTree vectorTileTree, IDistortion distortion, NoDistortionWatermarkOptions options,
        int key, BitArray message, double? distortParam)
    {
        var ndwm = new NoDistortionWatermark(options);
        VectorTileTree treeWithWatermark = ndwm.Embed(vectorTileTree, key, message);
        BitArray extractedMessageNoDistortion = ndwm.Extract(treeWithWatermark, key);

        VectorTileTree distortedTreeWithWatermark = distortion.Distort(treeWithWatermark);
        BitArray extractedMessageWithDistortion = ndwm.Extract(distortedTreeWithWatermark, key);

        Console.WriteLine($"\n\n\toptions: Nb-{options.Nb}|M-{options.M}|D-{options.D}|Ls-{options.Ls}|Lf-{options.Lf}|" +
            $"encType-{options.AtypicalEncodingType}|sh-{options.SecondHalfOfLineStringIsUsed}|||param-{distortParam}\n");
        ResultPrinter.PrintDistortion(distortion, message, extractedMessageNoDistortion, extractedMessageWithDistortion);

        return CompareMessages(extractedMessageNoDistortion, extractedMessageWithDistortion);
    }

    private static double CompareMessages(BitArray extractedMessageNoDistortion, BitArray extractedMessageWithDistortion)
    {
        var bitArrayBitsToCompareCount = extractedMessageWithDistortion.Count < extractedMessageNoDistortion.Count 
            ? extractedMessageWithDistortion.Count : extractedMessageNoDistortion.Count;

        var matchesCount = 0;
        var falseOnly = true;
        for (var i = 0; i < bitArrayBitsToCompareCount; i++)
        {
            if (extractedMessageNoDistortion[i] == extractedMessageWithDistortion[i])
            {
                matchesCount++;
                if (extractedMessageNoDistortion[i] == true)
                    falseOnly = false;
            }
        }

        if (falseOnly)
            return 0;

        return ((double)matchesCount)/extractedMessageNoDistortion.Count;
    }

    private static double CompareMessagesByFragments(BitArray extractedMessageNoDistortion, BitArray extractedMessageWithDistortion, int nb)
    {
        var bitArrayBitsToCompareCount = extractedMessageWithDistortion.Count < extractedMessageNoDistortion.Count
            ? extractedMessageWithDistortion.Count : extractedMessageNoDistortion.Count;

        var bitArrayFragmentsToCompareCount = bitArrayBitsToCompareCount / nb;

        var matchesCount = 0;
        for (var i = 0; i < bitArrayFragmentsToCompareCount; i++)
        {
            for (var j = 0; j < nb; j++)
            {
                if (extractedMessageNoDistortion[i * nb + j] != extractedMessageWithDistortion[i * nb + j])
                {
                    break;
                }
                matchesCount++;
            }
        }

        return ((double)matchesCount) / bitArrayFragmentsToCompareCount;
    }



    public static async void DiffDistortionParametersTest(VectorTileTree vectorTileTree, NoDistortionWatermarkOptions options,
        int key, BitArray message)
    {

        double param = 0;
        while (param <= 1)
        {
            var distortionWithParameterList = new List<IDistortion>() {
                new DeletingLayersDistortion(param),
                new DeletingByAreaDistortion(param),
                new RemoverByPerimeter(param),
                new ObjectsAdder(param),
                new ObjectsRemover(param),
                new ObjectsMagnifier(param),
                new CoordinateOrderReverser(param),

                new ShiftingPointsDistortion(param),

                new ReducingNumberOfPointsDistortion(param, false),
                new ReducingNumberOfPointsDistortion(param, true),
            };

            foreach (var distortion in distortionWithParameterList)
            {
                await TestDistortion(vectorTileTree, distortion, options, key, message, param);
            }

            param += 0.05;
        }

        var otherDistortionsList = new List<IDistortion>()
        {
            new SeparationByGeometryTypeDistortion(SeparationByGeometryTypeDistortion.Mode.Lines),
            new DeleterByBounds(60, 50, 50, 52),

            // new DeleterByBounds(53, 52, 51.4, 51.5),
            // new DeleterByBounds(53, 52, 50, 52),
        };

        foreach (var distortion in otherDistortionsList)
        {
            await TestDistortion(vectorTileTree, distortion, options, key, message, param);
        }
    }
    public static async Task TestDistortion(VectorTileTree vectorTileTree, IDistortion distortion, NoDistortionWatermarkOptions options, 
        int key, BitArray message, double? distortParam)
    {
        var ndwm = new NoDistortionWatermark(options);
        //VectorTileTree treeWithWatermark = GetTreeWithWatermark(ndwm, vectorTileTree, key, message);
        VectorTileTree treeWithWatermark = ndwm.Embed(vectorTileTree, key, message);
        BitArray extractedMessageNoDistortion = ndwm.Extract(treeWithWatermark, key);

        /*
        Console.BackgroundColor = ConsoleColor.Cyan; // ОТЛАДКА
        Console.ForegroundColor = ConsoleColor.Black; // ОТЛАДКА
        Console.WriteLine("\nПЕРЕД ИСКАЖЕНИЕМ\n"); // ОТЛАДКА
        Console.BackgroundColor = ConsoleColor.Black; // ОТЛАДКА
        Console.ForegroundColor = ConsoleColor.White; // ОТЛАДКА
        */

        //Console.WriteLine("\nПеред дистортом\n"); // ОТЛАДКА
        //Console.WriteLine($"\n\n[TestDistortion] Искажение: {distortion.GetType()}");
        VectorTileTree distortedTreeWithWatermark = distortion.Distort(treeWithWatermark);
        //Console.WriteLine("\nПосле дисторта\n"); // ОТЛАДКА
        BitArray extractedMessageWithDistortion = ndwm.Extract(distortedTreeWithWatermark, key);
        //Console.WriteLine("\nПосле извлечения\n"); // ОТЛАДКА

        //ResultPrinter.Print(distortion, message, extractedMessageNoDistortion, extractedMessageWithDistortion);
        //Console.WriteLine("\nПеред логом\n"); // ОТЛАДКА

        var logPath = $"..\\..\\..\\distortionTests\\Nb-{options.Nb}_M-{options.M}_D-{options.D}_Ls-{options.Ls}_Lf-{options.Lf}\\" +
            $"EncType-{options.AtypicalEncodingType}\\SecondLineStringHalfIsUsed-{options.SecondHalfOfLineStringIsUsed}".Replace('.', '_');
        var dirInfo = new DirectoryInfo(logPath);
        if (!dirInfo.Exists)
        {
            dirInfo.Create();
        }
        await ResultPrinter.Log(distortion, message, extractedMessageNoDistortion, extractedMessageWithDistortion, logPath, distortParam);
        //Console.WriteLine("\nПосле лога\n"); // ОТЛАДКА
    }
}
