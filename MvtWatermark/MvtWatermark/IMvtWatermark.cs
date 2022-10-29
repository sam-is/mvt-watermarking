using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace MvtWatermark
{
    public interface IMvtWatermark
    {
        VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message);
        BitArray Extract(VectorTileTree tile, int key);
    }
}