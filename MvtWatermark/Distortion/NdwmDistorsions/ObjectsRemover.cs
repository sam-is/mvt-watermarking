using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class ObjectsRemover: IDistortion
{
    private readonly double _relativeNumberFeatures;
    public ObjectsRemover(double relativeNumberFeatures)
    {
        if (relativeNumberFeatures < 0 || relativeNumberFeatures > 1)
            throw new ArgumentException("RelativeNumberFeatures must be within the interval [0, 1]", $"relativeNumberFeatures = {relativeNumberFeatures}");

        _relativeNumberFeatures = relativeNumberFeatures;
    }
    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();
        var random = new Random();

        foreach (var tileId in tiles)
        {
            var vectorTile = tiles[tileId];
            var copyTile = new VectorTile { TileId = tileId };

            foreach (var layer in vectorTile.Layers)
            {
                var count = layer.Features.Count;
                var indexList = new List<int>();
                var countDelete = (int)Math.Floor(count * _relativeNumberFeatures);

                for (var i = 0; i < count - countDelete; i++)
                {
                    var num = random.Next(0, count - 1);

                    while (indexList.Contains(num))
                        num = random.Next(0, count);

                    indexList.Add(num);
                }
                indexList.Sort();

                var copyLayer = new Layer() { Name = layer.Name };
                foreach (var index in indexList)
                {
                    copyLayer.Features.Add(new Feature(layer.Features[index].Geometry, layer.Features[index].Attributes));
                }

                copyTile.Layers.Add(copyLayer);
            }

            copyTileTree[tileId] = copyTile;
        }

        return copyTileTree;
    }
}
