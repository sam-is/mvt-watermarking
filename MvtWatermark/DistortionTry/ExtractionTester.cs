using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace DistortionTry;
public class ExtractionTester
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
                var logDirectory = $"..\\..\\..\\ExtractionTests\\{cataloguePath}\\{subPath}";
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
                        await WriteExtractionMetrics(vectorTileTree, options, key, message, fileName);
                    }
                }
            }
        }
    }

    public static async Task WriteExtractionMetrics(VectorTileTree vectorTileTree, NoDistortionWatermarkOptions options,
        int key, BitArray message, string fileName)
    {
        using (var streamWriter = new StreamWriter(fileName, true))
        {
            var optionsString = $"\nEncType: {options.AtypicalEncodingType} | D: {options.D} | M: {options.M} " +
                $"| Nb: {options.Nb} | Ls: {options.Ls} | Lf: {options.Lf} | SecondHalfUsed: {options.SecondHalfOfLineStringIsUsed}\n";

            var ndwm = new NoDistortionWatermark(options);
            VectorTileTree treeWithWatermark = ndwm.Embed(vectorTileTree, key, message);
            BitArray extractedMessage = ndwm.Extract(treeWithWatermark, key);

            Console.WriteLine($"\n\n\toptions: Nb-{options.Nb}|M-{options.M}|D-{options.D}|Ls-{options.Ls}|Lf-{options.Lf}|" +
                $"encType-{options.AtypicalEncodingType}|sh-{options.SecondHalfOfLineStringIsUsed}\n");
            var resultString = ResultPrinter.GetExtractionString(message, ndwm.EmbededMessage, extractedMessage);

            resultString += "\n" + CompareMessages(ndwm.EmbededMessage, extractedMessage);

            await streamWriter.WriteAsync(optionsString);
            await streamWriter.WriteAsync(resultString);
        }
    }

    private static double CompareMessages(BitArray originalMessage, BitArray extractedMessage)
    {
        var bitArrayBitsToCompareCount = extractedMessage.Count < originalMessage.Count
            ? extractedMessage.Count : originalMessage.Count;

        var matchesCount = 0;
        var falseOnly = true;
        for (var i = 0; i < bitArrayBitsToCompareCount; i++)
        {
            if (originalMessage[i] == extractedMessage[i])
            {
                matchesCount++;
                if (extractedMessage[i] == true)
                    falseOnly = false;
            }
        }

        if (falseOnly)
            return 0;

        return ((double)matchesCount) / originalMessage.Count;
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
}
