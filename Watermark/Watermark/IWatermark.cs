using NetTopologySuite.IO.VectorTiles;
using System.Collections.Generic;

namespace Watermark
{
    public interface IWatermark
    {
        VectorTile Embed(VectorTile tile, byte[] message, int key, List<double> parameters);
        byte[] Extract(VectorTile tile, int key, List<double> parameters);
    }
}