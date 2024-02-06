using Microsoft.Data.Sqlite;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace MvtWatermarkTests;

public class QimMvtWatermarkTests
{
    [Theory]
    [InlineData(Mode.WithCheck)]
    [InlineData(Mode.Repeat)]
    [InlineData(Mode.WithTilesMajorityVote)]
    public void EmbedOneTile(Mode mode)
    {
        var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "TmpTiles");
        var tileTree = new VectorTileTree();

        var tile = new VectorTile
        {
            TileId = 0
        };

        var feature = new Feature(new LineString(
                            new Coordinate[]
                            {
                                new(5, 5),
                                new(10, 10),
                                new(15, 15)
                            }
                        ),
                        new AttributesTable());
        var layer = new Layer();
        layer.Features.Add(feature);

        tile.Layers.Add(layer);
        tileTree[tile.TileId] = tile;

        var bits = new bool[] { false };
        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.9, 0.2, 0, 2048, 2, message.Count, 1, null, mode: mode, messageLength: message.Length);
        var qimMvtWatermark = new QimMvtWatermark(options);

        var watermarkedTileTree = qimMvtWatermark.Embed(tileTree, 0, message);

        var readTileTree = Data.WriteAndReadFromFile(watermarkedTileTree, pathToSave);

        var m = qimMvtWatermark.Extract(readTileTree, 0);

        Assert.NotNull(m);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m![i] == message[i]);
    }

    [Theory]
    [InlineData(Mode.WithCheck)]
    [InlineData(Mode.Repeat)]
    [InlineData(Mode.WithTilesMajorityVote)]
    public void EmbedTileTree(Mode mode)
    {
        var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "TmpTiles");
        var tileTree = new VectorTileTree();

        for (var i = 0; i < 10; i++)
        {
            var tile = new VectorTile
            {
                TileId = (ulong)i
            };

            var tileInfo = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile.TileId);

            var envelope = CoordinateConverter.TileBounds(tileInfo.X, tileInfo.Y, tileInfo.Zoom);

            var coordinates = new Coordinate[]
            {
                new(envelope.MinX + 0.15 * envelope.Width, envelope.MinY + 0.15 * envelope.Height),
                new(envelope.MinX + 0.5 * envelope.Width, envelope.MinY + 0.5 * envelope.Height),
                new(envelope.MinX + 0.7 * envelope.Width, envelope.MinY + 0.7 * envelope.Height)
            };

            var feature = new Feature(new LineString(coordinates), new AttributesTable());
            var layer = new Layer();
            layer.Features.Add(feature);

            tile.Layers.Add(layer);
            tileTree[tile.TileId] = tile;
        }

        var bits = new bool[10];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.9, 0.2, 0, 2048, 2, 1, 1, null, mode: mode, messageLength: message.Length);
        var qimMvtWatermark = new QimMvtWatermark(options);

        var watermarkedTileTree = qimMvtWatermark.Embed(tileTree, 0, message);

        var readTileTree = Data.WriteAndReadFromFile(watermarkedTileTree, pathToSave);

        var m = qimMvtWatermark.Extract(readTileTree, 0);

        Assert.NotNull(m);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m![i] == message[i]);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 50)]
    [InlineData(4, 25)]
    [InlineData(5, 20)]
    [InlineData(10, 10)]
    [InlineData(1, 49)]
    [InlineData(2, 2)]
    [InlineData(8, 8)]
    [InlineData(5, 1)]
    [InlineData(4, 1)]
    public void OneTileStp(int sizeMessage, int r)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "TmpTiles");
        const int x = 658;
        const int y = 333;
        const int z = 10;

        var tileTree = Data.GetDbVectorTileTree(path, x, x, y, y, z);

        var tile = tileTree[tileTree.First()];

        var bits = new bool[sizeMessage];
        for (var i = 0; i < sizeMessage; i++)
            bits[i] = true;

        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.9, 0.4, 5, 2048, 2, message.Count, r, null);

        var watermark = new QimMvtWatermark(options);
        var tileWatermarked = watermark.Embed(tile, 0, message);

        Assert.NotNull(tileWatermarked);

        var tileTreeWatermarked = new VectorTileTree
        {
            [tileWatermarked!.TileId] = tileWatermarked
        };

        var readTileTree = Data.WriteAndReadFromFile(tileTreeWatermarked, pathToSave);

        var readTile = readTileTree[readTileTree.First()];
        Assert.False(readTile.IsEmpty);

        var m = watermark.Extract(readTile, 0);

        Assert.NotNull(m);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m![i] == message[i]);
    }

    [Fact]
    public void VectorTileTreeFromOneTileStp()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "TmpTiles");
        const int x = 658;
        const int y = 334;
        const int z = 10;

        var tileTree = Data.GetDbVectorTileTree(path, x, x, y, y, z);

        var message = new BitArray(new[] { true, true, true, true });

        var options = new QimMvtWatermarkOptions(0.8, 0.2, 5, 2048, 2, message.Count, 15, null, 10, false, Mode.Repeat);

        var watermark = new QimMvtWatermark(options);
        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);

        Assert.NotNull(tileTreeWatermarked);

        var readTileTree = Data.WriteAndReadFromFile(tileTreeWatermarked, pathToSave);

        var m = watermark.Extract(readTileTree, 0);

        Assert.NotNull(m);

        for (var i = 0; i < message.Count; i++)
            Assert.True(m![i] == message[i]);
    }

    [Fact]
    public void VectorTileTreeFromFewTilesStp()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "TmpTiles");
        const int z = 10;
        const int minX = 650;
        const int maxX = 660;
        const int minY = 330;
        const int maxY = 335;

        using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
        sqliteConnection.Open();

        var tileTree = Data.GetDbVectorTileTree(path, minX, maxX, minY, maxY, z);

        var bits = new bool[40];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.9, 0.3, 5, 2048, 2, 5, 20, null, 10, false, Mode.WithTilesMajorityVote, message.Length);

        var watermark = new QimMvtWatermark(options);
        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);

        Assert.NotNull(tileTreeWatermarked);

        var readTileTree = Data.WriteAndReadFromFile(tileTreeWatermarked, pathToSave);

        var m = watermark.Extract(readTileTree, 0);

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
    //[InlineData(690151)]
    [InlineData(684008)]
    public void TestOpenAfterWatermarking(ulong id)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var tileId = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(id);
        var x = tileId.X;
        var y = tileId.Y;
        var z = tileId.Zoom;
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

        var options = new QimMvtWatermarkOptions(0.9, 0.3, 5, 2048, 2, message.Count, 3, null);
        var watermark = new QimMvtWatermark(options);

        var tileWatermarked = watermark.Embed(tile, (int)tile.TileId, message);

        Assert.NotNull(tileWatermarked);
        Assert.True(VectorTileUtils.IsValidForRead(tileWatermarked, 4096 * 8));
    }

    [Fact]
    public void VectorTileTreeFromFewTilesTegola()
    {
        var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "TmpTiles");
        const int z = 10;
        const int minX = 240;
        const int maxX = 250;
        const int minY = 390;
        const int maxY = 400;
        var tileTree = Data.GetUrlVectorTileTree(Data.TegolaUrl, minX, maxX, minY, maxY, z);

        var bits = new bool[24];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = true;
        var message = new BitArray(bits);

        var options = new QimMvtWatermarkOptions(0.9, 0.3, 5, 2048, 2, 3, 10, null, 10, false, Mode.WithTilesMajorityVote, message.Length);

        var watermark = new QimMvtWatermark(options);
        var tileTreeWatermarked = watermark.Embed(tileTree, 0, message);

        var readTileTree = Data.WriteAndReadFromFile(tileTreeWatermarked, pathToSave);

        var m = watermark.Extract(readTileTree, 0);

        for (var i = 0; i < message.Count; i++)
            Assert.Equal(m[i], message[i]);
    }
}