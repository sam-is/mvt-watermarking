using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.IO.Compression;

namespace MvtWatermarkConsole.Readers;
public static class DataReader
{
    public static VectorTileTree Read(string path, int minZ = 0, int maxZ = 22)
    {
        return IsMbtiles(path) ? ReadFromMbtiles(path, minZ, maxZ) : ReadFromFolder(path, minZ, maxZ);
    }

    public static VectorTileTree ReadFromMbtiles(string path, int minZ, int maxZ)
    {
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();
        var reader = new MapboxTileReader();
        var tileTree = new VectorTileTree();

        using var command = new SqliteCommand(@"SELECT tile_column, tile_row, zoom_level, tile_data FROM tiles WHERE zoom_level BETWEEN $minz AND $maxz", sqliteConnection);
        command.Parameters.AddWithValue("$minz", minZ);
        command.Parameters.AddWithValue("$maxz", maxZ);
        using var dbReader = command.ExecuteReader();

        while (dbReader.Read())
        {
            try
            {
                var x = dbReader.GetInt32(0);
                var z = dbReader.GetInt32(2);
                var y = (1 << z) - dbReader.GetInt32(1) - 1;

                var stream = dbReader.GetStream(3);

                using var decompressor = new GZipStream(stream, CompressionMode.Decompress, false);
                var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

                tileTree[tile.TileId] = tile;
            }
            catch
            {
                // ignored
            }
        }

        return tileTree;
    }

    public static VectorTileTree ReadFromFolder(string path, int minZ, int maxZ)
    {
        var reader = new MapboxTileReader();
        var tileTree = new VectorTileTree();

        var directoryInfo = new DirectoryInfo(path);
        foreach (var z in directoryInfo.GetDirectories())
        {
            if (Convert.ToInt32(z.Name) < minZ || Convert.ToInt32(z.Name) > maxZ)
                continue;
            foreach (var x in z.GetDirectories())
            {
                foreach (var y in x.GetFiles())
                {
                    try
                    {
                        using var fileStream = y.Open(FileMode.Open);
                        fileStream.Seek(0, SeekOrigin.Begin);
                        using var decompressor = new GZipStream(fileStream, CompressionMode.Decompress, false);
                        var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(Convert.ToInt32(x.Name), Convert.ToInt32(y.Name), Convert.ToInt32(z.Name)));

                        if (!tile.IsEmpty)
                            tileTree[tile.TileId] = tile;
                    }
                    catch
                    {
                        var id = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(Convert.ToInt32(x.Name), Convert.ToInt32(y.Name), Convert.ToInt32(z.Name));
                        tileTree[id.Id] = new VectorTile();
                    }
                }
            }
        }

        return tileTree;
    }

    public static bool IsMbtiles(string path) => Path.GetExtension(path) == ".mbtiles";
}
