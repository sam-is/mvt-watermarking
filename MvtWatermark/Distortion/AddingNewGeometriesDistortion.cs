using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;

public class AddingNewGeometriesDistortion : IDistortion
{
    private readonly int _count;

    private Geometry GetRandomGeometry(Envelope envelope)
    {
        Geometry geometry;

        var random = new Random();

        var num = random.Next(0, 2);

        switch (num)
        {
            case 0:
                geometry = new Point(new Coordinate(random.NextDouble() * (envelope.MaxX - envelope.MinX) + envelope.MinX,
                                                    random.NextDouble() * (envelope.MaxY - envelope.MinY) + envelope.MinY));
                return geometry;
            case 1:
                num = random.Next(2, 100);
                var coordinates = new Coordinate[num];
                for (var i = 0; i < num; i++)
                    coordinates[i] = new Coordinate(random.NextDouble() * (envelope.MaxX - envelope.MinX) + envelope.MinX,
                                                    random.NextDouble() * (envelope.MaxY - envelope.MinY) + envelope.MinY);
                geometry = new LineString(coordinates);
                return geometry;
            case 2:
                num = random.Next(5, 100);
                coordinates = new Coordinate[num];
                for (var i = 0; i < num; i++)
                    coordinates[i] = new Coordinate(random.NextDouble() * (envelope.MaxX - envelope.MinX) + envelope.MinX,
                                                    random.NextDouble() * (envelope.MaxY - envelope.MinY) + envelope.MinY);
                geometry = new Polygon(new LinearRing(coordinates));
                return geometry;
        }
        return new Point(envelope.Centre);

    }
    public AddingNewGeometriesDistortion(int count)
    {
        _count = count;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var tile = new VectorTile { TileId = tileId };

            var t = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);

            foreach (var layer in tiles[tileId].Layers)
            {
                var copyLayer = new Layer { Name = layer.Name };
                foreach (var feature in layer.Features)
                    copyLayer.Features.Add(new Feature(feature.Geometry, feature.Attributes));

                for (var i = 0; i < _count; i++)
                    copyLayer.Features.Add(new Feature(GetRandomGeometry(envelopeTile), new AttributesTable()));

                tile.Layers.Add(copyLayer);
            }

            copyTileTree[tileId] = tile;
        }

        return copyTileTree;
    }
}
