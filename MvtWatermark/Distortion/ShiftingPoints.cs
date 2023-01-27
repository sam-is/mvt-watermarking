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
                    var count = geometry.Coordinates.Count();
                    var step = (int)Math.Ceiling(count / (count * _relativeNumberPoints));

                    for (var i = 0; i < count; i += step)
                    {
                        if (i >= count)
                            break;
                        var coordinateMeters = CoordinateConverter.DegreesToMeters(geometry.Coordinates[i]);
                        var randomNumber = random.Next(0, 3);
                        if (randomNumber == 0)
                        {
                            coordinateMeters.X += extentDist;
                            coordinateMeters.Y += extentDist;
                        }

                        if (randomNumber == 1)
                        {
                            coordinateMeters.X -= extentDist;
                            coordinateMeters.Y += extentDist;
                        }

                        if (randomNumber == 2)
                        {
                            coordinateMeters.X += extentDist;
                            coordinateMeters.Y -= extentDist;
                        }

                        if (randomNumber == 3)
                        {
                            coordinateMeters.X -= extentDist;
                            coordinateMeters.Y -= extentDist;
                        }

                        var coor = CoordinateConverter.MetersToDegrees(coordinateMeters);
                        geometry.Coordinates[i].X = coor.X;
                        geometry.Coordinates[i].Y = coor.Y;
                    }


                }
            }
        }

        return copyTileTree;
    }
}
