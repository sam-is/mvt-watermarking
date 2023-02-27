using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class DeleterByBounds: IDistortion
{
    private double _top;
    private double _bottom;
    private double _left;
    private double _right;

    public DeleterByBounds(double top, double bottom, double left, double right)
    {
        if (top < bottom || right < left)
            throw new ArgumentException("Wrong bounds: top bound < bottom bound or right bound < left bound");
        _top = top;
        _bottom = bottom;
        _left = left;
        _right = right;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();
        foreach (var tileId in tiles)
        {
            var tile = new MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile(tileId);
            var vectorTile = tiles[tileId];
            if (tile.Top <= _top && tile.Bottom >= _bottom && tile.Left >= _left && tile.Right <= _right)
            {
                var copyTile = new VectorTile { TileId = tileId };
                foreach (var layer in vectorTile.Layers)
                {
                    var copyLayer = new Layer { Name = layer.Name };
                    foreach (var feature in layer.Features)
                    {
                        var copyFeature = new Feature(feature.Geometry, feature.Attributes);
                        copyLayer.Features.Add(copyFeature);
                    }
                    copyTile.Layers.Add(copyLayer);
                }
                copyTileTree[tileId] = copyTile;
            }
        }
        return copyTileTree;
    }
}
