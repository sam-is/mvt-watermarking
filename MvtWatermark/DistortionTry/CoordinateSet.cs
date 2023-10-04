namespace DistortionTry;
/// <summary>
/// Набор характеристик тайла: zoom (приближение), x (абсцисса), y (ордината)
/// </summary>
public class CoordinateSet
{
    public int Zoom { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public CoordinateSet(int zoom, int x, int y)
    {
        this.Zoom = zoom;
        this.X = x;
        this.Y = y;
    }
}