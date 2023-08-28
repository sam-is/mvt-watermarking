using Distortion;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark;
using System.Collections;
using Distortion.NdwmDistorsions;

namespace DistortionTry;
public class DistortionTester
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

    // Тут надо сразу среднеарифметическое считать по всем типам геометрии и использовании второй половины
    public async void DiffWatermarkParametersTest_Ls_Lf(List<CoordinateSet> parameterSetsStp, List<CoordinateSet> parameterSetsTegola, 
        OptionsParamRanges optionsParamRanges, BitArray message, string cataloguePath = "")
    {
        VectorTileTree vectorTileTree = TileSetCreator.GetVectorTileTree(parameterSetsStp, parameterSetsTegola);
        //VectorTileTree vectorTileTree = TileSetCreator.CreateRandomVectorTileTree(parameterSets);

        //var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var key = 123;

        //"average_Ls_Lf"
        await TestParametersInCycle_LS_LF(vectorTileTree, key, message, cataloguePath, "", optionsParamRanges);

        /*
        //var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        //var secondHalfOfLineStringIsUsed = false;
        var taskMtLtLtFalse = new Task(async () => 
        { 
            await TestParametersInCycle_LS_LF(vectorTileTree, key, message, cataloguePath, "MtLtLt_false", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed); 
        });
        taskMtLtLtFalse.Start();

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        secondHalfOfLineStringIsUsed = true;
        var taskMtLtLtTrue = new Task(async () =>
        {
            await TestParametersInCycle_LS_LF(vectorTileTree, key, message, cataloguePath, "MtLtLt_true", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
        });
        taskMtLtLtTrue.Start();

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        secondHalfOfLineStringIsUsed = false;
        var taskNLtFalse = new Task(async () =>
        {
            await TestParametersInCycle_LS_LF(vectorTileTree, key, message, cataloguePath, "NLt_false", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
        });
        taskNLtFalse.Start();

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        secondHalfOfLineStringIsUsed = true;
        var taskNLtTrue = new Task(async () =>
        {
            await TestParametersInCycle_LS_LF(vectorTileTree, key, message, cataloguePath, "NLt_true", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
        });
        taskNLtTrue.Start();

        taskMtLtLtFalse.Wait();
        taskMtLtLtTrue.Wait();
        taskNLtFalse.Wait();
        taskNLtTrue.Wait();
        */
    }

    public async void DiffWatermarkParametersTest_M_Nb(List<CoordinateSet> parameterSetsStp, List<CoordinateSet> parameterSetsTegola,
        OptionsParamRanges optionsParamRanges, BitArray message, string cataloguePath = "")
    {
        VectorTileTree vectorTileTree = TileSetCreator.GetVectorTileTree(parameterSetsStp, parameterSetsTegola);
        //VectorTileTree vectorTileTree = TileSetCreator.CreateRandomVectorTileTree(parameterSets);

        //var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var key = 123;

        //"average_M_Nb"
        await TestParametersInCycle_M_Nb(vectorTileTree, key, message, cataloguePath, "", optionsParamRanges);
        
        /*
        var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var secondHalfOfLineStringIsUsed = false;
        var taskMtLtLtFalse = new Task(async () =>
        {
            await TestParametersInCycle_M_Nb(vectorTileTree, key, message, cataloguePath, "MtLtLt_false", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
        });
        taskMtLtLtFalse.Start();

        
        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        secondHalfOfLineStringIsUsed = true;
        var taskMtLtLtTrue = new Task(async () =>
        {
            await TestParametersInCycle_M_Nb(vectorTileTree, key, message, cataloguePath, "MtLtLt_true", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
        });
        taskMtLtLtTrue.Start();

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        secondHalfOfLineStringIsUsed = false;
        var taskNLtFalse = new Task(async () =>
        {
            await TestParametersInCycle_M_Nb(vectorTileTree, key, message, cataloguePath, "NLt_false", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
        });
        taskNLtFalse.Start();

        aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        secondHalfOfLineStringIsUsed = true;
        var taskNLtTrue = new Task(async () =>
        {
            await TestParametersInCycle_M_Nb(vectorTileTree, key, message, cataloguePath, "NLt_true", optionsParamRanges, aEtype, secondHalfOfLineStringIsUsed);
        });
        taskNLtTrue.Start();

        taskMtLtLtFalse.Wait();
        taskMtLtLtTrue.Wait();
        taskNLtFalse.Wait();
        taskNLtTrue.Wait();
        */
    }

    public async Task TestParametersInCycle_LS_LF(VectorTileTree vectorTileTree, int key, BitArray message, 
        string cataloguePath, string subPath, OptionsParamRanges optionsParamRanges)
    {
        //, NoDistortionWatermarkOptions.AtypicalEncodingTypes aEtype, bool secondHalfOfLineStringIsUsed
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
                        /*
                        var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, aEtype, secondHalfOfLineStringIsUsed);
                        await WriteMetricsMatrix(vectorTileTree, options, key, message, fileName);
                        */

                        using (var streamWriter = new StreamWriter(fileName, true))
                        {
                            /*var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, aEtype, secondHalfOfLineStringIsUsed);
                            await WriteMetricsMatrix(vectorTileTree, options, key, message, fileName);*/

                            var taskAuxiliary = new Task<(List<List<double>>, List<List<double>>)>((List<List<double>>, List<List<double>>) () =>
                            {
                                var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt, false);
                                var resultMatrix1 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);
                                options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt, true);
                                var resultMatrix2 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);
                                return (resultMatrix1, resultMatrix2);
                            });
                            taskAuxiliary.Start();

                            
                            var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands, false);
                            var resultMatrix3 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);
                            options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands, true);
                            var resultMatrix4 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);

                            taskAuxiliary.Wait();
                            (List<List<double>> resultMatrix1, List<List<double>> resultMatrix2) = taskAuxiliary.Result;

                            var resultMatrix = new List<List<double>>();
                            foreach (var resultVector in resultMatrix1)
                            {
                                resultMatrix.Add(new List<double>(resultVector));
                            }
                            for (var i = 0; i < resultMatrix.Count; i++)
                            {
                                for (var j = 0; j < resultMatrix[0].Count; j++)
                                {
                                    resultMatrix[i][j] += resultMatrix2[i][j];
                                    resultMatrix[i][j] += resultMatrix3[i][j];
                                    resultMatrix[i][j] += resultMatrix4[i][j];
                                    resultMatrix[i][j] /= 4;
                                }
                            }

                            double metric = 0;
                            foreach (var vector in resultMatrix1)
                            {
                                foreach (var value in vector)
                                    metric += value;
                            }

                            var resultsCount = resultMatrix1.Count * resultMatrix1[0].Count;
                            var relativeMetric = Math.Round(metric / resultsCount, 4);

                            var optionsString = $"\nAVERAGE | D: {options.D} | M: {options.M} " +
                $"| Nb: {options.Nb} | Ls: {options.Ls} | Lf: {options.Lf}\n" +
                $"Metric: {Math.Round(metric, 3)}/{resultsCount} | Relative Metric: {relativeMetric}\n\n";
                            var resultMatrixString = "";

                            foreach (var vector in resultMatrix)
                            {
                                foreach (var value in vector)
                                {
                                    resultMatrixString += $"{Math.Round(value, 2)}\t";
                                }
                                resultMatrixString += "\n";
                            }


                            await streamWriter.WriteAsync(optionsString);
                            await streamWriter.WriteAsync(resultMatrixString);
                        }
                    }
                }
            }
        }
    }

    public async Task TestParametersInCycle_M_Nb(VectorTileTree vectorTileTree, int key, BitArray message,
        string cataloguePath, string subPath, OptionsParamRanges optionsParamRanges)
    {
        for (var lf = optionsParamRanges.Lfmin; lf <= optionsParamRanges.Lfmax; lf++)
        {
            for (var ls = optionsParamRanges.Lsmin; ls <= optionsParamRanges.Lsmax; ls++)
            {
                var logDirectory = $"..\\..\\..\\MatricesTests\\{cataloguePath}\\{subPath}";
                var dirInfo = new DirectoryInfo(logDirectory);
                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                }
                var fileName = logDirectory + $"\\Ls-{ls}_Lf-{lf}.txt";
                using (var fileStream = new FileStream(fileName, FileMode.Create)) { }

                for (var m = optionsParamRanges.Mmin; m <= optionsParamRanges.Mmax; m++)
                {
                    for (var nb = optionsParamRanges.Nbmin; nb <= optionsParamRanges.Nbmax; nb++)
                    {
                        using (var streamWriter = new StreamWriter(fileName, true))
                        {
                            /*var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, aEtype, secondHalfOfLineStringIsUsed);
                            await WriteMetricsMatrix(vectorTileTree, options, key, message, fileName);*/

                            var taskAuxiliary = new Task<(List<List<double>>, List<List<double>>)>((List<List<double>>, List<List<double>>) () =>
                            {
                                var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt, false);
                                var resultMatrix1 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);
                                options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt, true);
                                var resultMatrix2 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);
                                return (resultMatrix1, resultMatrix2);
                            });
                            taskAuxiliary.Start();

                            
                            var options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands, false);
                            var resultMatrix3 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);
                            options = new NoDistortionWatermarkOptions(m, nb, ls, lf, NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands, true);
                            var resultMatrix4 = GetMetricsMatrix(vectorTileTree, options, key, message, fileName);

                            taskAuxiliary.Wait();
                            (List<List<double>> resultMatrix1, List<List<double>> resultMatrix2) = taskAuxiliary.Result;

                            var resultMatrix = new List<List<double>>();
                            foreach (var resultVector in resultMatrix1)
                            {
                                resultMatrix.Add(new List<double>(resultVector));
                            }
                            for (var i = 0; i < resultMatrix.Count; i++)
                            {
                                for (var j = 0; j < resultMatrix[0].Count; j++)
                                {
                                    resultMatrix[i][j] += resultMatrix2[i][j];
                                    resultMatrix[i][j] += resultMatrix3[i][j];
                                    resultMatrix[i][j] += resultMatrix4[i][j];
                                    resultMatrix[i][j] /= 4;
                                }
                            }

                            double metric = 0;
                            foreach (var vector in resultMatrix1)
                            {
                                foreach (var value in vector)
                                    metric += value;
                            }

                            var resultsCount = resultMatrix1.Count * resultMatrix1[0].Count;
                            var relativeMetric = Math.Round(metric / resultsCount, 4);

                            var optionsString = $"\nAVERAGE | D: {options.D} | M: {options.M} " +
                $"| Nb: {options.Nb} | Ls: {options.Ls} | Lf: {options.Lf}\n" +
                $"Metric: {Math.Round(metric, 3)}/{resultsCount} | Relative Metric: {relativeMetric}\n\n";
                            var resultMatrixString = "";

                            foreach (var vector in resultMatrix)
                            {
                                foreach (var value in vector)
                                {
                                    resultMatrixString += $"{Math.Round(value, 2)}\t";
                                }
                                resultMatrixString += "\n";
                            }


                            await streamWriter.WriteAsync(optionsString);
                            await streamWriter.WriteAsync(resultMatrixString);
                        }
                    }
                }
            }
        }
    }

    public List<List<double>> GetMetricsMatrix(VectorTileTree vectorTileTree, NoDistortionWatermarkOptions options,
        int key, BitArray message, string fileName)
    {
        var resultMatrix = new List<List<double>>();

        //var step = 0.05;
        //var upperBorder = 1;
        //double param = 0;

        var step = 1;
        var upperBorder = 3;
        var param = 1;

        //var step = 0.01;
        //var upperBorder = 0.5;
        //var param = 0.01;

        while (param <= upperBorder)
        {
            var oneParamResultVector = new List<double>();

            var distortionWithParameterList = new List<IDistortion>() {
                    //new DeletingLayersDistortion(param),
                    /*
                    new DeletingByAreaDistortion(param),
                    new RemoverByPerimeter(param),
                    new ObjectsAdder(param),
                    new ObjectsRemover(param),
                    new ObjectsMagnifier(param),
                    new CoordinateOrderReverser(param),
                    */

                    //new FixedObjectsMagnifier(Convert.ToInt32(Math.Ceiling(param * 10)))
                    new FewPointsDeleter(param),

                    //new ShiftingPointsDistortion(param),

                    /*new ReducingNumberOfPointsDistortion(param, false),
                    new ReducingNumberOfPointsDistortion(param, true),*/
                };

            foreach (var distortion in distortionWithParameterList)
            {
                var comparisonResult = TestSingleDistortion(vectorTileTree, distortion, options, key, message, param);
                //comparisonResult = comparisonResult < 1 ? 0 : 1; //!!! строка для проверки извлекаемости вида 1/0
                /*if (comparisonResult < 1)
                    comparisonResult = 0;*/
                oneParamResultVector.Add(comparisonResult);
            }

            resultMatrix.Add(oneParamResultVector);

            param += step;
        }

        

        return resultMatrix;
    }

    public async Task WriteMetricsMatrix(VectorTileTree vectorTileTree, NoDistortionWatermarkOptions options,
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
                    //new DeletingLayersDistortion(param),
                    new DeletingByAreaDistortion(param),
                    new RemoverByPerimeter(param),
                    new ObjectsAdder(param),
                    new ObjectsRemover(param),
                    new ObjectsMagnifier(param),
                    new CoordinateOrderReverser(param),

                    //new ShiftingPointsDistortion(param),

                    //new ReducingNumberOfPointsDistortion(param, false),
                    //new ReducingNumberOfPointsDistortion(param, true),
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

    private double TestSingleDistortion(VectorTileTree vectorTileTree, IDistortion distortion, NoDistortionWatermarkOptions options,
        int key, BitArray message, double? distortParam)
    {
        var ndwm = new NoDistortionWatermark(options);
        VectorTileTree treeWithWatermark = ndwm.Embed(vectorTileTree, key, message);
        BitArray extractedMessageNoDistortion = ndwm.Extract(treeWithWatermark, key);

        VectorTileTree distortedTreeWithWatermark = distortion.Distort(treeWithWatermark);
        BitArray extractedMessageWithDistortion = ndwm.Extract(distortedTreeWithWatermark, key);

        /*Console.WriteLine($"\n\n\toptions: Nb-{options.Nb}|M-{options.M}|D-{options.D}|Ls-{options.Ls}|Lf-{options.Lf}|" +
            $"encType-{options.AtypicalEncodingType}|sh-{options.SecondHalfOfLineStringIsUsed}|||param-{distortParam}\n");*/
        //ResultPrinter.PrintDistortion(distortion, message, extractedMessageNoDistortion, extractedMessageWithDistortion);

        return CompareMessages(extractedMessageNoDistortion, extractedMessageWithDistortion);
    }

    private double CompareMessages(BitArray extractedMessageNoDistortion, BitArray extractedMessageWithDistortion)
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

    private double CompareMessagesByFragments(BitArray extractedMessageNoDistortion, BitArray extractedMessageWithDistortion, int nb)
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
