namespace NoDistortionWatermarkMetrics
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //MetricsAnalyzer.GetUsersTileMetrics(1, 16, 0, 0, 0);
            //MetricsAnalyzer.GetUsersTileMetricsParallel(1, 16);
            MetricsAnalyzer.GetDBTileMetrics(1, 16, 10, 658, 334);
        }
    }
}