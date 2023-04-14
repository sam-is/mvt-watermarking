using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class CoordinateOrderReverser: IDistortion
{
    private readonly double _relativeObjectsToReverse;
    public CoordinateOrderReverser(double relativeObjectsToReverse)
    {
        if (relativeObjectsToReverse < 0 || relativeObjectsToReverse > 1)
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
                            var reversedMltlnstrngIenum = ((MultiLineString)ftr.Geometry.Reverse()).AsEnumerable().Reverse();
                            var reversedMltlnstrngList = new List<Geometry>(reversedMltlnstrngIenum);
                            var reversedMltlnstrngArr = new LineString[reversedMltlnstrngList.Count];

                            for (var i = 0; i < reversedMltlnstrngList.Count; i++)
                            {
                                reversedMltlnstrngArr[i] = (LineString)reversedMltlnstrngList[i];
                            }
                            newGeom = new MultiLineString(reversedMltlnstrngArr);
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
