using NetTopologySuite.IO.VectorTiles;

namespace Distortion;

public interface IDistortion
{
    VectorTileTree Distort(VectorTileTree tiles);
}
