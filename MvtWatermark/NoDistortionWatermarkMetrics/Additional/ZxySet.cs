namespace NoDistortionWatermarkMetrics.Additional;
/// <summary>
/// Набор характеристик тайла: zoom (приближение), x (абсцисса), y (ордината)
/// </summary>
public class ZxySet
{
    public int Zoom { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public ZxySet(int zoom, int x, int y)
    {
        this.Zoom = zoom;
        this.X = x;
        this.Y = y;
    }
}
