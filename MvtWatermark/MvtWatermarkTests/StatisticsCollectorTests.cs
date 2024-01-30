using MvtWatermark.QimMvtWatermark;
using MvtWatermark.QimMvtWatermark.Requantization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using Xunit;

namespace MvtWatermarkTests;
public class StatisticsCollectorTests
{
    [Fact]
    public void CollectTest()
    {
        var tile = new VectorTile
        {
            TileId = 0
        };

        var feature = new Feature(new LineString(
                            new Coordinate[]
                            {
                                new(5, 5),
                                new(10, 10),
                                new(15, 15)
                            }
                        ),
                        new AttributesTable());
        var layer = new Layer();
        layer.Features.Add(feature);

        tile.Layers.Add(layer);

        var tileEnvelope = CoordinateConverter.DegreesToMeters(CoordinateConverter.TileBounds(0, 0, 0));
        var generatorOfRequantizationMatrices = new GeneratorOfRequantizationMatrices(10);
        var map = generatorOfRequantizationMatrices.GetMap(new QimMvtWatermarkOptions(), 0);
        var requntitizationMatrix = new RequantizationMatrix(map);
        var statisticsCollector = new StatisticsCollector(tile, requntitizationMatrix, tileEnvelope, 0);
        var polygon = GeneratorBoundsPolygon.Get(tileEnvelope, 1, 0, 0);


        var stat = statisticsCollector.Collect(polygon, out var s0, out var s1);
        Assert.Equal(1, s0);
        Assert.Equal(2, s1);
        Assert.Equal(0.3333, stat, 1E-4);
    }
}
