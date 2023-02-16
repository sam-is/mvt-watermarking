using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;

public class DeletingLayersDistortion : IDistortion
{
    private readonly double _relativeNumberLayers;

    public DeletingLayersDistortion(double relativeNumberLayers)
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
                var num = random.Next(0, count);

                while (indexList.Contains(num))
                    num = random.Next(0, count);

                indexList.Add(num);
            }
            indexList.Sort();

            var copyTile = new VectorTile { TileId = tileId };

            foreach (var index in indexList)
            {
                var layer = tile.Layers[index];
                var l = new Layer { Name = layer.Name };
                foreach (var feature in layer.Features)
                    l.Features.Add(new Feature(feature.Geometry.Copy(), feature.Attributes));

                copyTile.Layers.Add(l);
            }

            copyTileTree[tileId] = copyTile;
        }

        return copyTileTree;
    }
}
