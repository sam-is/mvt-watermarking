using NetTopologySuite.IO.VectorTiles;
using System.Collections;
using System.Collections.Generic;

namespace Watermark
{
    public interface IWatermarkTiles
    {
        List<VectorTile> Embed(List<VectorTile> tiles, int key, BitArray message);
        BitArray Extract(List<VectorTile> tile, int key);
    }
}