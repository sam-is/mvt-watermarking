using NoDistortionWatermarkMetrics.Additional;
namespace NoDistortionWatermarkMetrics;

internal class Program
{
    public static void Main(string[] args)
    {
        var parameterSets = new List<ZxySet>
        {
            //new ZxySet(10, 658, 331),
            new ZxySet(10, 658, 332),
            new ZxySet(10, 658, 333),
            new ZxySet(10, 658, 334),
            new ZxySet(10, 658, 338),
            //new ZxySet(10, 658, 335), // кривой тайл, не считывается
            new ZxySet(10, 658, 337),
        };
        // если не в порядке возрастания Y, то результаты странные, надо проверить

        var parameterRangeSet = new ParameterRangeSet(1, 7, 2, 4, 1, 16);

        var singleParameterSet = new ZxySet(0, 0, 0);

        //MetricAnalyzer.DisplayUsersTileMetric(parameterRangeSet, singleParameterSet);

        //MetricAnalyzer.DisplayMetricForDBTileSet(parameterRangeSet, parameterSets);

        //Console.WriteLine(MetricAnalyzer.TestVectorTileIsCorrect(new MetricAnalyzer.ZxySet(10, 658, 338)));

        NewMetricAnalyzer.TestAlgorithm(parameterSets);
    }
}