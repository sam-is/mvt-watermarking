using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class CoordinateOrderReverser: IDistortion
{
    private readonly double _relativeObjectsToReverse;
    public CoordinateOrderReverser(double relativeObjectsToReverse)
    {
        if (relativeObjectsToReverse is < 0 or > 1)
            throw new ArgumentException("relativeObjectsToReverse should be inside the [0, 1] interval");
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
                    if (counter < featuresNum)
                    {
                        if (ftr.Geometry is LineString)
                        {
                            newGeom = ftr.Geometry.Reverse();
                            counter++;
                        }
                        else if (ftr.Geometry is MultiLineString)
                        {
                            var reversedMultiLineStringIenum = ((MultiLineString)ftr.Geometry.Reverse()).AsEnumerable().Reverse();
                            var reversedMultiLineStringList = new List<Geometry>(reversedMultiLineStringIenum);
                            var reversedMultiLineStringArr = new LineString[reversedMultiLineStringList.Count];

                            for (var i = 0; i < reversedMultiLineStringList.Count; i++)
                            {
                                reversedMultiLineStringArr[i] = (LineString)reversedMultiLineStringList[i];
                            }
                            newGeom = new MultiLineString(reversedMultiLineStringArr);
                            counter++;
                        }
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
