using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Watermark
{
    public interface IWatermark
    {
        VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message);
        BitArray Extract(VectorTileTree tile, int key);
    }
}