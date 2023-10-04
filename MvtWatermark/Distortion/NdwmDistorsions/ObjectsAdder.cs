using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class ObjectsAdder: IDistortion
{
    private readonly double _relativeNumberFeatures;

    public ObjectsAdder(double relativeNumberFeatures)
    {
        if (relativeNumberFeatures is < 0 or > 1)
            throw new ArgumentException("RelativeNumberFeatures must be within the interval [0, 1]", $"relativeNumberFeatures = {relativeNumberFeatures}");

        _relativeNumberFeatures = relativeNumberFeatures;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var vectorTile = tiles[tileId];
            var copyTile = new VectorTile { TileId = tileId };

            foreach (var layer in vectorTile.Layers)
            {
                var copyLayer = new Layer { Name = layer.Name };

                foreach(var feature in layer.Features)
                {
                    copyLayer.Features.Add(feature);
                }

                var newFeaturesCount = (int)Math.Floor(layer.Features.Count * _relativeNumberFeatures);

                for (var i = 0; i < newFeaturesCount; i++)
                {
                    copyLayer.Features.Add(GenerateFeature(i));
                }
                copyTile.Layers.Add(copyLayer);
            }
            copyTileTree[tileId] = copyTile;
        }
        return copyTileTree;
    }

    private static Feature GenerateFeature(int number)
    {
        var random = new Random();
        var feature = new Feature();
        var featureType = random.Next(0, 3);

        switch (featureType)
        {
            case 0: 
                feature = GeneratePoint(random, number);
                break;
            case 1: 
                feature = GenerateLineString(random, number);
                break;
            case 2: 
                feature = GeneratePolygon(random, number);
                break;
        }

        return feature;
    }

    private static Feature GeneratePoint(Random random, int number)
    {
        var xCoord = random.Next(-179, 178) + 0.5;
        var yCoord = random.Next(-89, 88) + 0.5;
        var point = new Point(new Coordinate(xCoord, yCoord));
        return new Feature
        {
            Geometry = point,
            Attributes = new AttributesTable(new Dictionary<string, object>()
            {
                ["LN_ID"] = $"generated_feature_number_{number}",
                ["type"] = "Point",
            })
        };
    }

    private static Feature GenerateLineString(Random random, int number)
    {
        var coordinateCollection = new List<Coordinate>();
        for (var i = 0; i < random.Next(2, 60); i++)
        {
            var xCoord = random.Next(-179, 178) + 0.5;
            var yCoord = random.Next(-89, 88) + 0.5;

            coordinateCollection.Add(new Coordinate(xCoord, yCoord));
        }
        var coordinateArray = coordinateCollection.ToArray();

        var geom = new LineString(coordinateArray);

        return new Feature
        {
            Geometry = geom,
            Attributes = new AttributesTable(new Dictionary<string, object>()
            {
                ["LN_ID"] = $"generated_feature_number_{number}",
                ["type"] = "Linestring",
            })
        };
    }

    private static Feature GeneratePolygon(Random random, int number)
    {
        var coordinateCollection = new List<Coordinate>();

        var startXCoord = random.Next(-179, 178) + 0.5;
        var startYCoord = random.Next(-89, 88) + 0.5;
        coordinateCollection.Add(new Coordinate(startXCoord, startYCoord));

        for (var i = 0; i < random.Next(2, 60); i++)
        {
            var xCoord = random.Next(-179, 178) + 0.5;
            var yCoord = random.Next(-89, 88) + 0.5;
            coordinateCollection.Add(new Coordinate(xCoord, yCoord));
        }
        coordinateCollection.Add(new Coordinate(startXCoord, startYCoord));

        var coordinateArray = coordinateCollection.ToArray();
        var linearRing = new LinearRing(coordinateArray);

        var geom = new Polygon(linearRing);

        return new Feature
        {
            Geometry = geom,
            Attributes = new AttributesTable(new Dictionary<string, object>()
            {
                ["LN_ID"] = $"generated_feature_number_{number}",
                ["type"] = "Polygon",
            })
        };
    }
}
