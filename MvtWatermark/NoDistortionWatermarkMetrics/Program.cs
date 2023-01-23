namespace NoDistortionWatermarkMetrics
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var parameterSet = new List<MetricAnalyzer.ZxySet>
            {
                new MetricAnalyzer.ZxySet(10, 658, 334),
                //new MetricAnalyzer.ZxySet(10, 658, 335)
                new MetricAnalyzer.ZxySet(10, 658, 337),
                new MetricAnalyzer.ZxySet(10, 658, 338)
            };

            var parameterRangeSet = new MetricAnalyzer.ParameterRangeSet(1, 7, 2, 4, 1, 16);

            //MetricAnalyzer.DisplayUsersTileMetric(parameterRangeSet, 0, 0, 0);

            MetricAnalyzer.DisplayMetricForDBTileSet(parameterRangeSet, parameterSet);

            //Console.WriteLine(MetricAnalyzer.TestVectorTileIsCorrect(new MetricAnalyzer.ZxySet(10, 658, 338)));
        }
    }
}