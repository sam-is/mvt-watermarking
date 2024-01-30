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
    public void MetersToDegrees(double x, double y, double expectedX, double expectedY)
    {
        var coordinate = CoordinateConverter.MetersToDegrees(new Coordinate(x, y));
        Assert.Equal(expectedX, coordinate.X, 1E-4);
        Assert.Equal(expectedY, coordinate.Y, 1E-4);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0.089834, 0.0898, 10000.275, 9996.494)]
    [InlineData(13.47473, -17.6789, 1500000.0819, -1999998.33)]
    [InlineData(180, -85, 20037508.342789244, -19971868.88040856)]
    public void DegreeToMeters(double x, double y, double expectedX, double expectedY)
    {
        var coordinate = CoordinateConverter.DegreesToMeters(new Coordinate(x, y));
        Assert.Equal(expectedX, coordinate.X, 1E-2);
        Assert.Equal(expectedY, coordinate.Y, 1E-2);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, -20037508.34, -20037508.34)]
    [InlineData(0, 0, 0, 2048, 2048, 0, 0)]
    [InlineData(0, 0, 0, 4095, 4095, 20027724.4, 20027724.4)]
    [InlineData(651, 338, 10, 2048, 2048, 5459438.307, 6790054.095)]
    public void IntegerToMeters(int tileX, int tileY, int tileZ, int x, int y, double expectedX, double expectedY)
    {
        var envelopeTile = CoordinateConverter.DegreesToMeters(CoordinateConverter.TileBounds(tileX, tileY, tileZ));
        var extentDistance = envelopeTile.Height / 4096;
        var coordinate = CoordinateConverter.IntegerToMeters(new CoordinateConverter.IntPoint(x, y), envelopeTile, extentDistance);
        Assert.Equal(expectedX, coordinate.X, 1E-2);
        Assert.Equal(expectedY, coordinate.Y, 1E-2);
    }

    [Theory]
    [InlineData(0, 0, 0, -20037508.34, -20037508.34, 0, 0)]
    [InlineData(0, 0, 0, 0, 0, 2048, 2048)]
    [InlineData(0, 0, 0, 20027724.4, 20027724.4, 4095, 4095)]
    [InlineData(651, 338, 10, 5459438.307, 6790054.095, 2048, 2048)]
    public void MetersToInteger(int tileX, int tileY, int tileZ, int x, int y, double expectedX, double expectedY)
    {
        var envelopeTile = CoordinateConverter.DegreesToMeters(CoordinateConverter.TileBounds(tileX, tileY, tileZ));
        var extentDistance = envelopeTile.Height / 4096;
        var coordinate = CoordinateConverter.MetersToInteger(new Coordinate(x, y), envelopeTile, extentDistance);
        Assert.Equal(expectedX, coordinate.X);
        Assert.Equal(expectedY, coordinate.Y);
    }
}
