using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;

public class DeletingByArea : IDistortion
{
    private readonly double _relativeArea;

    public DeletingByArea(double relativeArea)
    {
        _relativeArea = relativeArea;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var envelopeTile = CoordinateConverter.TileBounds(tile.X, tile.Y, tile.Zoom);
            var tileArea = envelopeTile.Area;
            var area = tileArea * _relativeArea;

            var copyTile = new VectorTile { TileId = tileId };

            foreach (var layer in tiles[tileId].Layers)
            {
                var l = new Layer { Name = layer.Name };
                foreach (var feature in layer.Features)
                {
                    if (feature.Geometry.Area > area)
                    {
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
}
