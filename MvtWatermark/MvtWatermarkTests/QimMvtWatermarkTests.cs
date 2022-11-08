using System;
using System.IO.Compression;
using System.IO;
using Xunit;
using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using MvtWatermark.QimMvtWatermark;
using System.Collections;

namespace MvtWatermarkTests
{
    public class QimMvtWatermarkTests
    {
        [Fact]
        public void OneTileOneBit()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "stp.mbtiles");
            var x = 658;
            var y = 334;
            var z = 10;
            using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
            sqliteConnection.Open();

            using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
            command.Parameters.AddWithValue("$z", z);
            command.Parameters.AddWithValue("$x", x);
            command.Parameters.AddWithValue("$y", (1 << z) - y - 1);
            var bytes = (byte[])command.ExecuteScalar();

            using var memoryStream = new MemoryStream(bytes);
            var reader = new MapboxTileReader();

            memoryStream.Seek(0, SeekOrigin.Begin);
            using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
            var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

            var options = new QimMvtWatermarkOptions(0.6, 0.5, 4096, 20, 2, 10, 100);

            var message = new BitArray(new bool[] {true});

            var watermark = new QimMvtWatermark(options);
            var tileWatermarked = watermark.Embed(tile, tile.TileId, 0, message);
            var m = watermark.Extract(tileWatermarked, tileWatermarked.TileId, 0);

            Assert.True(m[0] == message[0]);
        }
    }
}
