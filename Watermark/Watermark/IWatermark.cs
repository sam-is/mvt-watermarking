using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Watermark
{
    public interface IWatermark
    {
        VectorTile Embed(VectorTile tile, int key, BitArray message);
        BitArray Extract(VectorTile tile, int key);
    }
}