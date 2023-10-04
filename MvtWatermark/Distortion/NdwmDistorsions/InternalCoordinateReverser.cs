using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class InternalCoordinateReverser : IDistortion
{
    private readonly double _relativeObjectsToReverse;
    public InternalCoordinateReverser(double relativeObjectsToReverse)
    {
        _relativeObjectsToReverse = relativeObjectsToReverse;
    }
    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();
        foreach (var tileId in tiles)
        {
            var copyTile = new VectorTile { TileId = tileId };

            foreach (var lyr in tiles[tileId].Layers)
            {
                var copyLayer = new Layer { Name = lyr.Name };

                var featuresNum = (int)Math.Floor(_relativeObjectsToReverse * lyr.Features.Count);
                var counter = 0;
                foreach (var ftr in lyr.Features)
                {
                    var newGeom = ftr.Geometry.Copy();
                    if (counter < featuresNum && (ftr.Geometry is LineString || ftr.Geometry is MultiLineString))
                    {
                        newGeom = ftr.Geometry.Reverse();
                        counter++;
                    }

                    var copyFeature = new Feature(newGeom, ftr.Attributes);
                    copyLayer.Features.Add(copyFeature);
                }

                copyTile.Layers.Add(copyLayer);
            }

            copyTileTree[tileId] = copyTile;
        }

        return copyTileTree;
    }
}
