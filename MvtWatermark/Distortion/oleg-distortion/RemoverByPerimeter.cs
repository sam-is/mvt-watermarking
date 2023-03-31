using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.Features;

namespace Distortion;
public class RemoverByPerimeter : IDistortion
{
    private readonly double _relativePerimeter;

    public RemoverByPerimeter(double relativePerimeter)
    {
        _relativePerimeter = relativePerimeter;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var maximumTilePerimeter = FindMaximumTilePerimeter(tiles);
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            /*
            var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var envelopeTile = CoordinateConverter.TileBounds(tile.X, tile.Y, tile.Zoom);
            var tilePerimeter = 2 * (envelopeTile.Width + envelopeTile.Height);
            */

            var perimeter = maximumTilePerimeter * _relativePerimeter;

            /*
            Console.WriteLine($"Периметр тайла: {tilePerimeter}");
            Console.WriteLine($"Площадь тайла: {envelopeTile.Area}");
            Console.WriteLine($"Относительный периметр: {perimeter}");
            */

            var copyTile = new VectorTile { TileId = tileId };

            foreach (var layer in tiles[tileId].Layers)
            {
                var l = new Layer { Name = layer.Name };
                foreach (var feature in layer.Features)
                {
                    if (feature.Geometry.Length > perimeter)
                    {
                        //Console.WriteLine($"Длина геометрии в фиче: {feature.Geometry.Length}");
                        //Console.WriteLine($"Площадь геометрии в фиче: {feature.Geometry.Area}");
                        var copyFeature = new Feature(feature.Geometry, feature.Attributes);
                        l.Features.Add(copyFeature);
                    }
                }
                copyTile.Layers.Add(l);
            }

            copyTileTree[tileId] = copyTile;
        }

        return copyTileTree;
    }

    private static double FindMaximumTilePerimeter(VectorTileTree tiles)
    {
        double maxPerimeter = 0;
        foreach (var tileId in tiles)
        {
            var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var envelopeTile = CoordinateConverter.TileBounds(tile.X, tile.Y, tile.Zoom);
            var tilePerimeter = 2 * (envelopeTile.Width + envelopeTile.Height);

            if (tilePerimeter > maxPerimeter)
            {
                maxPerimeter = tilePerimeter;
            }
        }

        return maxPerimeter;
    }
}
