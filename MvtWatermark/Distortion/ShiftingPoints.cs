using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.Features;
using MvtWatermark.QimMvtWatermark;

namespace Distortion;
public class ShiftingPoints : IDistortion
{
    private readonly double _relativeNumberPoints;

    public ShiftingPoints(double relativeNumberPoints)
    {
        _relativeNumberPoints = relativeNumberPoints;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var tile = new VectorTile { TileId = tileId };
            foreach (var layer in tiles[tileId].Layers)
            {
                var l = new Layer
                {
                    Name = layer.Name
                };
                foreach (var feature in layer.Features)
                {
                    var f = new Feature(feature.Geometry, feature.Attributes);
                    l.Features.Add(f);
                }
                tile.Layers.Add(l);
            }
            copyTileTree[tileId] = tile;
        }

        foreach (var tileId in copyTileTree)
        {
            var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var envelopeTile = CoordinateConverter.TileBounds(tile.X, tile.Y, tile.Zoom);
            envelopeTile = CoordinateConverter.DegreesToMeters(envelopeTile);
            var extentDist = envelopeTile.Height / 4096;

            var random = new Random();

            foreach (var layer in tiles[tileId].Layers)
            {
                foreach (var feature in layer.Features)
                {
                    var geometry = feature.Geometry;
                    var length = geometry.Coordinates.Length;
                    var step = (int)Math.Ceiling(length / (length * _relativeNumberPoints));

                    for (var i = 0; i < length; i += step)
                    {
                        var coordinateMeters = CoordinateConverter.DegreesToMeters(geometry.Coordinates[i]);
                        var randomNumber = random.Next(0, 3);

                        switch (randomNumber)
                        {

                            case 0:
                                coordinateMeters.X += extentDist;
                                coordinateMeters.Y += extentDist;
                                break;

                            case 1:
                                coordinateMeters.X -= extentDist;
                                coordinateMeters.Y += extentDist;
                                break;

                            case 2:
                                coordinateMeters.X += extentDist;
                                coordinateMeters.Y -= extentDist;
                                break;

                            case 3:
                                coordinateMeters.X -= extentDist;
                                coordinateMeters.Y -= extentDist;
                                break;
                        }

                        var coordinateDegrees = CoordinateConverter.MetersToDegrees(coordinateMeters);
                        geometry.Coordinates[i].X = coordinateDegrees.X;
                        geometry.Coordinates[i].Y = coordinateDegrees.Y;
                    }


                }
            }
        }

        return copyTileTree;
    }
}
