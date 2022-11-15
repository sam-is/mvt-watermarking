using Microsoft.Data.Sqlite;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace MvtWatermarkTests;

public class QimMvtWatermarkTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 50)]
    [InlineData(4, 25)]
    [InlineData(5, 20)]
    [InlineData(10, 10)]
    [InlineData(1, 50)]
    [InlineData(2, 25)]
    [InlineData(4, 20)]
    [InlineData(5, 10)]
    [InlineData(10, 5)]
    public void OneTileOneBit(int sizeMessage, int r)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        const int x = 658;
        const int y = 334;
        const int z = 10;
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
        command.Parameters.AddWithValue("$z", z);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", (1 << z) - y - 1);
        var obj = command.ExecuteScalar();

        Assert.NotNull(obj);

        var bytes = (byte[])obj!;

        using var memoryStream = new MemoryStream(bytes);
        var reader = new MapboxTileReader();

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

        var bits = new bool[sizeMessage];
        for (var i = 0; i < sizeMessage; i++)
            bits[i] = true;

        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, message.Count, r);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(tile, 0, message);

        Assert.NotNull(tileWatermarked);

        var m = watermark.Extract(tileWatermarked!, 0);

        Assert.NotNull(m);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m![i] == message[i]);
    }

    [Fact]
    public void VectorTileTreeFromOneTile()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        const int x = 658;
        const int y = 334;
        const int z = 10;
        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
        command.Parameters.AddWithValue("$z", z);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", (1 << z) - y - 1);
        var obj = command.ExecuteScalar();

        Assert.NotNull(obj);

        var bytes = (byte[])obj!;

        using var memoryStream = new MemoryStream(bytes);
        var reader = new MapboxTileReader();

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

        var message = new BitArray(new[] { true, true, true, true });

        var vt = new VectorTileTree
        {
            [tile.TileId] = tile
        };

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, message.Count, 15);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(vt, 0, message);
        var m = watermark.Extract(tileWatermarked, 0);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m[i] == message[i]);
    }

    [Fact]
    public void VectorTileTreeFromFewTiles()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        const int z = 10;

        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        var tileTree = new VectorTileTree();
        for (var x = 653; x < 658; x++)
            for (var y = 330; y < 334; y++)
            {
                using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
                command.Parameters.AddWithValue("$z", z);
                command.Parameters.AddWithValue("$x", x);
                command.Parameters.AddWithValue("$y", (1 << z) - y - 1);
                var obj = command.ExecuteScalar();

                Assert.NotNull(obj);

                var bytes = (byte[])obj!;

                using var memoryStream = new MemoryStream(bytes);
                var reader = new MapboxTileReader();

                memoryStream.Seek(0, SeekOrigin.Begin);
                using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
                var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

                tileTree[tile.TileId] = tile;
            }

        var bits = new bool[100];
        for (var i = 0; i < 100; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, (int)Math.Floor((double)message.Count / tileTree.Count()), 20);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(tileTree, 0, message);
        var m = watermark.Extract(tileWatermarked, 0);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m[i] == message[i]);
    }

    [Fact]
    public void EmptyTile()
    {
        var tile = new VectorTile { TileId = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(0, 0, 0).Id };
        tile.Layers.Add(new Layer { Name = "test" });

        var message = new BitArray(new[] { true });

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, message.Count, 5);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(tile, 0, message);

        Assert.Null(tileWatermarked);

        var m = watermark.Extract(tile, 0);

        Assert.Null(m);
    }
}

