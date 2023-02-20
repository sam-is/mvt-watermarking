using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;

public class DeletingLayers : IDistortion
{
    private readonly double _relativeNumberLayers;

    public DeletingLayers(double relativeNumberLayers)
    {
        _relativeNumberLayers = relativeNumberLayers;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var tile = tiles[tileId];
            var count = tile.Layers.Count;
            var countDelete = (int)Math.Floor(count * _relativeNumberLayers);

            var indexList = new List<int>();
            var random = new Random();
            for (var i = 0; i < count - countDelete; i++)
            {
                var num = random.Next(0, count - 1);

                while (indexList.Contains(num))
                    num = random.Next(0, count - 1); // ошибочка видимо, надо не count - 1, а просто count

                indexList.Add(num);
            }
            indexList.Sort();

            var copyTile = new VectorTile { TileId = tileId };

            foreach (var index in indexList)
            {
                var layer = tile.Layers[index];
                var l = new Layer { Name = layer.Name };
                foreach (var feature in layer.Features)
                {
                    var copyFeature = new Feature(feature.Geometry, feature.Attributes);
                    l.Features.Add(copyFeature);
                }
                copyTile.Layers.Add(l);
            }

            copyTileTree[tileId] = copyTile;
        }

        return copyTileTree;
    }
}
