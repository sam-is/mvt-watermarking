using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class CoordinateOrderChanger: IDistortion
{
    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();
        foreach (var tileId in tiles)
        {
            var copyTile = new VectorTile { TileId = tileId };

            foreach (var lyr in tiles[tileId].Layers)
            {
                var copyLayer = new Layer { Name = lyr.Name };

                foreach (var ftr in lyr.Features)
                {
                    var newGeom = ftr.Geometry.Copy();
                    if (ftr.Geometry is LineString)
                    {
                        // Console.WriteLine($"\n\nfeature type: {ftr.Geometry.GetType().Name}; feature geometry: {ftr.Geometry}");

                        newGeom = ftr.Geometry.Reverse();

                        // Console.WriteLine($"\nREVERSED feature type: {ftr.Geometry.GetType().Name}; feature geometry: {newGeom}");
                    }
                    else if (ftr.Geometry is MultiLineString)
                    {
                        // Console.WriteLine($"\n\nfeature type: {ftr.Geometry.GetType().Name}; feature geometry: {ftr.Geometry}");

                        newGeom = ftr.Geometry.Reverse();

                        // Console.WriteLine($"\nREVERSED feature type: {ftr.Geometry.GetType().Name}; feature geometry: {newGeom}");
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
