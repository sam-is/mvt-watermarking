using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;


namespace MvtWatermarkTests;
public class Data
{
    static public string TegolaUrl { get; } = "https://tegola-osm-demo.go-spatial.org/v1/maps/osm";
    static public string QwantPath { get; } = "https://www.qwant.com/maps/default";
    static public VectorTileTree GetDbVectorTileTree(string path, int minX, int maxX, int minY, int maxY, int z)
    {
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();
        var reader = new MapboxTileReader();
        var tileTree = new VectorTileTree();

        using var command = new SqliteCommand(@"SELECT tile_column, tile_row, tile_data FROM tiles WHERE zoom_level = $z AND tile_column >= $minx AND tile_column <= $maxx AND tile_row >= $miny AND tile_row <= $maxy", sqliteConnection);
        command.Parameters.AddWithValue("$z", z);
        command.Parameters.AddWithValue("$minx", minX);
        command.Parameters.AddWithValue("$maxx", maxX);
        command.Parameters.AddWithValue("$miny", (1 << z) - maxY - 1);
        command.Parameters.AddWithValue("$maxy", (1 << z) - minY - 1);

        var dbReader = command.ExecuteReader();

        while (dbReader.Read())
        {
            var x = dbReader.GetInt32(0);
            var y = (1 << z) - dbReader.GetInt32(1) - 1;

            var stream = dbReader.GetStream(2);

            if (stream.Length == 0)
                continue;

            using var decompressor = new GZipStream(stream, CompressionMode.Decompress, false);
            var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

            tileTree[tile.TileId] = tile;
        }

        return tileTree;
    }

    static public VectorTileTree GetDbVectorTileTree(string path, int z)
    {
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();
        var reader = new MapboxTileReader();
        var tileTree = new VectorTileTree();

        using var command = new SqliteCommand(@"SELECT tile_column, tile_row, tile_data FROM tiles WHERE zoom_level = $z", sqliteConnection);
        command.Parameters.AddWithValue("$z", z);
        using var dbReader = command.ExecuteReader();

        while (dbReader.Read())
        {
            try
            {
                var x = dbReader.GetInt32(0);
                var y = (1 << z) - dbReader.GetInt32(1) - 1;

                var stream = dbReader.GetStream(2);

                using var decompressor = new GZipStream(stream, CompressionMode.Decompress, false);
                var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

                tileTree[tile.TileId] = tile;
            }
            catch
            {

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

    static public VectorTileTree GetUrlVectorTileTree(string url, int minX, int maxX, int minY, int maxY, int z)
    {
        var reader = new MapboxTileReader();
        var tileTreeTegola = new VectorTileTree();
        Parallel.For(minX, maxX, x =>
        {
            Parallel.For(minY, maxY, y =>
            {
                using var sharedClient = new HttpClient()
                {
                    BaseAddress = new Uri($"{url}/{z}/{x}/{y}"),
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

                    if (!tile.IsEmpty)
                        tileTreeTegola[tile.TileId] = tile;
                }
                catch (Exception)
                {

                }
            });
        });

        return tileTreeTegola;
    }

    static public void WriteToFile(VectorTileTree tileTree, string path, uint extent = 4096)
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

    static public VectorTileTree ReadFromFiles(string path)
    {
        var reader = new MapboxTileReader();
        var tileTree = new VectorTileTree();

        var directoryInfo = new DirectoryInfo(path);
        foreach (var z in directoryInfo.GetDirectories())
        {
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
                    catch (Exception)
                    {
                        var id = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(Convert.ToInt32(x.Name), Convert.ToInt32(y.Name), Convert.ToInt32(z.Name));
                        tileTree[id.Id] = new VectorTile();
                    }
                }
            }
        }
        return tileTree;
    }

    static public VectorTileTree WriteAndReadFromFile(VectorTileTree tileTree, string path, uint extent = 4096)
    {
        WriteToFile(tileTree, path, extent);
        var readTileTree = ReadFromFiles(path);
        Directory.Delete(path, true);

        return readTileTree;
    }
}
