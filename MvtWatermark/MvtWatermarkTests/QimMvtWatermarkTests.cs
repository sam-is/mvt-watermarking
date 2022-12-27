using Microsoft.Data.Sqlite;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
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
    public void OneTileStp(int sizeMessage, int r)
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

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, message.Count, r, null);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(tile, 0, message);

        Assert.NotNull(tileWatermarked);

        var m = watermark.Extract(tileWatermarked!, 0);

        Assert.NotNull(m);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m![i] == message[i]);
    }

    [Fact]
    public void VectorTileTreeFromOneTileStp()
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

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, message.Count, 15, null);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(vt, 0, message);
        var m = watermark.Extract(tileWatermarked, 0);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m[i] == message[i]);
    }

    [Fact]
    public void VectorTileTreeFromFewTilesStp()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        const int z = 10;

        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        var reader = new MapboxTileReader();
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

                memoryStream.Seek(0, SeekOrigin.Begin);
                using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
                var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

                tileTree[tile.TileId] = tile;
            }

        var bits = new bool[80];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, 5, 20, null);

        var watermark = new QimMvtWatermark(options);
        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);
        var m = watermark.Extract(tileTreeWatermarked, 0);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m[i] == message[i]);
    }

    [Fact]
    public void EmptyTile()
    {
        var tile = new VectorTile { TileId = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(0, 0, 0).Id };
        tile.Layers.Add(new Layer { Name = "test" });

        var message = new BitArray(new[] { true });

        var options = new QimMvtWatermarkOptions(0.6, 0.5, 20, 4096, 2, message.Count, 5, null);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(tile, 0, message);

        Assert.Null(tileWatermarked);

        var m = watermark.Extract(tile, 0);

        Assert.Null(m);
    }

    [Theory]
    [InlineData(687072)]
    [InlineData(688101)]
    [InlineData(690148)]
    [InlineData(693216)]
    [InlineData(687073)]
    [InlineData(692194)]
    [InlineData(693218)]
    [InlineData(686051)]
    [InlineData(693219)]
    [InlineData(686052)]
    [InlineData(686053)]
    [InlineData(693221)]
    [InlineData(684006)]
    [InlineData(685030)]
    [InlineData(692198)]
    [InlineData(684007)]
    [InlineData(690151)]
    [InlineData(684008)]
    public void TestOpenAfterWatermarking(ulong id)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var tileid = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(id);
        int x = tileid.X;
        int y = tileid.Y;
        int z = tileid.Zoom;
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

        var bits = new bool[3];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;

        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.5, 0.6, 20, 4096, 2, message.Count, 10, null);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(tile, (int)tile.TileId, message);

        Assert.NotNull(tileWatermarked);

        var m = watermark.Extract(tileWatermarked!, (int)tile.TileId);
        Assert.NotNull(m);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m![i] == message[i]);

        using var mem = new MemoryStream();
        tileWatermarked.Write(mem);
        mem.Seek(0, SeekOrigin.Begin);
        var t = reader.Read(mem, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileid.Id));
        Assert.NotNull(t);
    }

    [Fact]
    public void VectorTileTreeFromFewTilesTegola()
    {
        const int z = 10;
        var tileTree = new VectorTileTree();
        for (var x = 242; x < 246; x++)
            for (var y = 390; y < 394; y++)
            {
                using var sharedClient = new HttpClient()
                {
                    BaseAddress = new Uri($"https://tegola-osm-demo.go-spatial.org/v1/maps/osm/{z}/{x}/{y}"),
                };

                sharedClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 QGIS/32210");
                sharedClient.DefaultRequestHeaders.Add("accept-encoding", "gzip");

                var response = sharedClient.GetByteArrayAsync("").Result;
                using var memoryStream = new MemoryStream(response);

                var reader = new MapboxTileReader();
                memoryStream.Seek(0, SeekOrigin.Begin);
                using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
                var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

                tileTree[tile.TileId] = tile;
            }

        var bits = new bool[60];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.3, 0.3, 5, 4096, 2, 5, 10, null);

        var watermark = new QimMvtWatermark(options);
        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);

        Assert.NotNull(tileTreeWatermarked);

        var m = watermark.Extract(tileTreeWatermarked!, 0);

        Assert.NotNull(m);

        for (var i = 0; i < message!.Count; i++)
            Assert.Equal(m![i], message[i]);
    }
}