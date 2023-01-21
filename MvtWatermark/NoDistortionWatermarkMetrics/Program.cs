namespace NoDistortionWatermarkMetrics
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var parameterSet = new List<MetricAnalyzer.ZXYset>
            {
                new MetricAnalyzer.ZXYset(10, 658, 334),
                //new MetricAnalyzer.ZXYset(10, 658, 335)
                new MetricAnalyzer.ZXYset(10, 658, 337),
                new MetricAnalyzer.ZXYset(10, 658, 338)
            };

            MetricAnalyzer.GetUsersTileMetric(1, 16, 0, 0, 0);

            //MetricAnalyzer.GetDBTileMetric(1, 16, parameterSet);

            //Console.WriteLine(MetricAnalyzer.TestVectorTileIsCorrect(new MetricAnalyzer.ZXYset(10, 658, 338)));
        }
    }
}