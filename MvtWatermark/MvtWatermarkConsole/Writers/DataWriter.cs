using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.IO.Compression;

namespace MvtWatermarkConsole.Writers;
public static class DataWriter
{
    public static void Write(VectorTileTree tileTree, string path, bool isNoCompression, uint extent = 4096)
    {
        if (TypeChecker.IsMbtiles(path))
            WriteInMbtiles(tileTree, path, isNoCompression, extent);
        else
            WriteInFolder(tileTree, path, isNoCompression, extent);
    }

    public static void WriteInFolder(VectorTileTree tileTree, string path, bool isNoCompression, uint extent = 4096)
    {
        foreach (var tileId in tileTree)
        {
            var tileInfo = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var pathFile = Path.Combine(path, tileInfo.Zoom.ToString(), tileInfo.X.ToString());
            if (!Directory.Exists(pathFile))
                Directory.CreateDirectory(pathFile);

            using var memoryStream = new MemoryStream();

            if (!isNoCompression)
            {
                using var compressor = new GZipStream(memoryStream, CompressionMode.Compress, true);
                tileTree[tileId].Write(compressor, extent);
                compressor.Flush();
            }
            else
            {
                tileTree[tileId].Write(memoryStream, extent);
            }

            File.WriteAllBytes(Path.Combine(pathFile, tileInfo.Y.ToString()), memoryStream.ToArray());
        }
    }

    public static void WriteInMbtiles(VectorTileTree tileTree, string path, bool isNoCompression, uint extent = 4096)
    {
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        foreach (var tileId in tileTree)
        {
            var tileInfo = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);

            using var memoryStream = new MemoryStream();

            if (!isNoCompression) 
            {
                using var compressor = new GZipStream(memoryStream, CompressionMode.Compress, true);
                tileTree[tileId].Write(compressor, extent);
                compressor.Flush();
            }
            else
            {
                tileTree[tileId].Write(memoryStream, extent);
            }
            

            using var command = new SqliteCommand("UPDATE tiles SET tile_data = @Tile WHERE tile_column = @X AND tile_row = @Y AND zoom_level = @Z", sqliteConnection);
            command.Parameters.AddWithValue("X", tileInfo.X);
            command.Parameters.AddWithValue("Y", (1 << tileInfo.Zoom) - tileInfo.Y - 1);
            command.Parameters.AddWithValue("Z", tileInfo.Zoom);
            command.Parameters.AddWithValue("Tile", memoryStream.ToArray());
            command.ExecuteNonQuery();
        }
    }
}
