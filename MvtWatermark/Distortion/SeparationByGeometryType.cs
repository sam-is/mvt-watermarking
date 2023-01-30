using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class SeparationByGeometryType : IDistortion
{

    private readonly Mode _mode;
    public enum Mode
    {
        All,
        Points,
        Lines,
        Polygons
    }

    public SeparationByGeometryType(Mode mode)
    {
        _mode = mode;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var id in tiles)
        {
            var pointLayer = new Layer { Name = "points" };
            var lineLayer = new Layer { Name = "lines" };
            var polygonLayer = new Layer { Name = "polygons" };

            foreach (var layer in tiles[id].Layers)
            {
                foreach (var feature in layer.Features)
                {
                    var copyFeature = new Feature(feature.Geometry, feature.Attributes);

                    switch (feature.Geometry.GeometryType)
                    {
                        case "Point":
                            pointLayer.Features.Add(copyFeature);
                            break;
                        case "LineString":
                            lineLayer.Features.Add(copyFeature);
                            break;
                        case "Polygon":
                            polygonLayer.Features.Add(copyFeature);
                            break;
                        case "MultiPoint":
                            pointLayer.Features.Add(copyFeature);
                            break;
                        case "MultiLineString":
                            lineLayer.Features.Add(copyFeature);
                            break;
                        case "MultiPolygon":
                            polygonLayer.Features.Add(copyFeature);
                            break;
                    }
                }
            }

            var tile = new VectorTile { TileId = id };

            switch (_mode)
            {
                case Mode.All:
                    tile.Layers.Add(pointLayer);
                    tile.Layers.Add(lineLayer);
                    tile.Layers.Add(polygonLayer);
                    break;
                case Mode.Points:
                    tile.Layers.Add(pointLayer);
                    break;
                case Mode.Lines:
                    tile.Layers.Add(lineLayer);
                    break;
                case Mode.Polygons:
                    tile.Layers.Add(polygonLayer);
                    break;
            }

            copyTileTree[id] = tile;
        }

        return copyTileTree;
    }
}
