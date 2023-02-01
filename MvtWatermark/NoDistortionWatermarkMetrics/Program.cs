namespace NoDistortionWatermarkMetrics;

internal class Program
{
    public static void Main(string[] args)
    {
        var parameterSets = new List<MetricAnalyzer.ZxySet>
        {
            new MetricAnalyzer.ZxySet(10, 658, 334),
            //new MetricAnalyzer.ZxySet(10, 658, 335)
            new MetricAnalyzer.ZxySet(10, 658, 337),
            new MetricAnalyzer.ZxySet(10, 658, 338)
        };

        var parameterRangeSet = new MetricAnalyzer.ParameterRangeSet(1, 7, 2, 4, 1, 16);

        var singleParameterSet = new MetricAnalyzer.ZxySet(0, 0, 0);

        //MetricAnalyzer.DisplayUsersTileMetric(parameterRangeSet, singleParameterSet);

        MetricAnalyzer.DisplayMetricForDBTileSet(parameterRangeSet, parameterSets);

        //Console.WriteLine(MetricAnalyzer.TestVectorTileIsCorrect(new MetricAnalyzer.ZxySet(10, 658, 338)));
    }
}