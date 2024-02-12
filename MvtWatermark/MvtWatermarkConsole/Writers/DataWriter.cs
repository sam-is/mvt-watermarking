using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.IO.Compression;

namespace MvtWatermarkConsole.Writers;
public static class DataWriter
{
    public static void Write(VectorTileTree tileTree, string path, uint extent = 4096)
    {
        foreach (var tileId in tileTree)
        {
            var tileInfo = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var pathFile = Path.Combine(path, tileInfo.Zoom.ToString(), tileInfo.X.ToString());
            if (!Directory.Exists(pathFile))
                Directory.CreateDirectory(pathFile);

            using var compressedStream = new MemoryStream();
            using var compressor = new GZipStream(compressedStream, CompressionMode.Compress, true);

            tileTree[tileId].Write(compressor, extent);
            compressor.Flush();

            File.WriteAllBytes(Path.Combine(pathFile, tileInfo.Y.ToString()), compressedStream.ToArray());
        }
    }
}
