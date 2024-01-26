using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Geometries;
using Xunit;

namespace MvtWatermarkTests;
public class CoordinateConverterTests
{
    [Theory]
    [InlineData(0, 0, 0, -180, 180, -85.0511, 85.0511)]
    [InlineData(0, 0, 1, -180, 0, 0, 85.0511)]
    [InlineData(0, 0, 2, -180, -90, 66.5132, 85.0511)]
    [InlineData(0, 0, 3, -180, -135, 79.1713, 85.0511)]
    public void TileBounds(int x, int y, int z, double expectedMinX, double expectedMaxX, double expectedMinY, double expectedMaxY)
    {
        var envelope = CoordinateConverter.TileBounds(x, y, z);
        Assert.Equal(expectedMinX, envelope.MinX, 1E-4);
        Assert.Equal(expectedMaxX, envelope.MaxX, 1E-4);
        Assert.Equal(expectedMinY, envelope.MinY, 1E-4);
        Assert.Equal(expectedMaxY, envelope.MaxY, 1E-4);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10000, 10000, 0.0898, 0.0898)]
    [InlineData(1500000, -2000000, 13.4747, -17.6789)]
    [InlineData(20037508.342789244, -19971868.880408566, 180, -85)]
    public void MetersToDegree(double x, double y, double expectedX, double expectedY )
    {
        var coordinate = CoordinateConverter.MetersToDegrees(new Coordinate(x, y));
        Assert.Equal(expectedX, coordinate.X, 1E-4);
        Assert.Equal(expectedY, coordinate.Y, 1E-4);
    }
}
