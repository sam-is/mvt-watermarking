using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.IO.Compression;

namespace Researches;
public class Data
{
    static public VectorTileTree GetStpVectorTileTree(string path, int minX, int maxX, int minY, int maxY, int z)
    {
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();
        var reader = new MapboxTileReader();
        var tileTree = new VectorTileTree();

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
                command.Parameters.AddWithValue("$z", z);
                command.Parameters.AddWithValue("$x", x);
                command.Parameters.AddWithValue("$y", (1 << z) - y - 1);
                var obj = command.ExecuteScalar();

                if (obj == null)
                    continue;

                var bytes = (byte[])obj!;

                using var memoryStream = new MemoryStream(bytes);

                memoryStream.Seek(0, SeekOrigin.Begin);
                using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
                var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

                tileTree[tile.TileId] = tile;
            }
        }
        return tileTree;
    }

    static public (int, int, int, int) GetMinMax(string path, int z)
    {
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        using var sqlCommand = new SqliteCommand(@"SELECT min(tile_column), max(tile_column), min(tile_row), max(tile_row) FROM tiles WHERE zoom_level = $z", sqliteConnection);
        sqlCommand.Parameters.AddWithValue("$z", z);

        var sqlReader = sqlCommand.ExecuteReader();

        if (sqlReader.Read())
        {
            var minX = sqlReader.GetInt32(0);
            var maxX = sqlReader.GetInt32(1);
            var minY = sqlReader.GetInt32(2);
            var maxY = sqlReader.GetInt32(3);
            var tmpMinY = (1 << z) - maxY - 1;
            maxY = (1 << z) - minY - 1;
            minY = tmpMinY;
            return (minX, maxX, minY, maxY);
        }
        else
            return (0, 0, 0, 0);
    }

    static public VectorTileTree GetTegolaVectorTileTree(int minX, int maxX, int minY, int maxY, int z)
    {
        var reader = new MapboxTileReader();
        var tileTreeTegola = new VectorTileTree();
        Parallel.For(minX, maxX, x =>
        {
            Parallel.For(minY, maxY, y =>
            {
                using var sharedClient = new HttpClient()
                {
                    BaseAddress = new Uri($"https://tegola-osm-demo.go-spatial.org/v1/maps/osm/{z}/{x}/{y}"),
                };

                sharedClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 QGIS/32210");
                sharedClient.DefaultRequestHeaders.Add("accept-encoding", "gzip");

                try
                {
                    var response = sharedClient.GetByteArrayAsync("").Result;
                    using var memoryStream = new MemoryStream(response);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
                    var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

                    if(!tile.IsEmpty)
                        tileTreeTegola[tile.TileId] = tile;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{z}, {x}, {y} throw exception: {e.Message}");
                }
            });
        });

        return tileTreeTegola;
    }
}
