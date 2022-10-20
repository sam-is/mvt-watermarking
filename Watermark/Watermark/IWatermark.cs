using NetTopologySuite.IO.VectorTiles;
using System.Collections;

namespace Watermark
{
    public interface IWatermark
    {
        VectorTile Embed(VectorTile tile, BitArray message);
        BitArray Extract(VectorTile tile);
    }
}