using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Tile = NetTopologySuite.IO.VectorTiles.Tiles.Tile;

namespace MvtWatermarkingService.Services;

public partial class MvtReader
{
    [GeneratedRegex("\\d+\\/\\d+\\/\\d+")]
    private static partial Regex GetZXYRegex();
    public static VectorTile? Read(string url)
    {
        var tileId = GetTileIdFromUrl(url);
        if (tileId == null)
            return null;

        var reader = new MapboxTileReader();

        using var sharedClient = new HttpClient()
        {
            BaseAddress = new Uri(url),
        };

        sharedClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 QGIS/32210");
        sharedClient.DefaultRequestHeaders.Add("accept-encoding", "gzip");
        VectorTile? tile = null;
        try
        {
            var response = sharedClient.GetByteArrayAsync("").Result;
            using var memoryStream = new MemoryStream(response);
            memoryStream.Seek(0, SeekOrigin.Begin);
            using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
            tile = reader.Read(decompressor, tileId);
        }
        catch (Exception)
        {
            return null;
        };

        return tile;
    }

    public static VectorTileTree? Read(string url, int minX, int maxX, int minY, int maxY, int z)
    {
        var reader = new MapboxTileReader();
        var tileTree = new VectorTileTree();
        var dict = new ConcurrentDictionary<ulong, VectorTile>();
        Parallel.For(minX, maxX + 1, x =>
        {
            Parallel.For(minY, maxY + 1, y =>
            {
                var tile = Read($"{url}/{z}/{x}/{y}");

                if (tile != null && !tile.IsEmpty)
                    //tileTree[tile.TileId] = tile;
                    dict[tile.TileId] = tile;
            });
        });

        foreach (var tile in dict.Values)
            tileTree[tile.TileId] = tile;

        return tileTree;
    }

    public static Tile? GetTileIdFromUrl(string url)
    {
        var match = GetZXYRegex().Match(url).Groups[0].Value;
        try
        {
            var numbers = match.Split("/").Select(num => Convert.ToInt32(num)).ToList();
            if (numbers.Count < 3)
                return null;
            return new Tile(numbers[1], numbers[2], numbers[0]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static byte[] VectorTileToByteArray(VectorTile tile)
    {
        using var compressedStream = new MemoryStream();
        using var compressor = new GZipStream(compressedStream, CompressionMode.Compress, true);

        tile.Write(compressor);
        compressor.Flush();
        compressedStream.Seek(0, SeekOrigin.Begin);
        return compressedStream.ToArray();
    }

    public static byte[] VectorTileTreeToZip(VectorTileTree tileTree)
    {
        using var compressedFileStream = new MemoryStream();

        using (var zipArchive = new ZipArchive(compressedFileStream, ZipArchiveMode.Create, true))
        {

            foreach (var tileId in tileTree)
            {
                var bytes = VectorTileToByteArray(tileTree[tileId]);
                var tileInfo = new Tile(tileId);
                var name = $"{tileInfo.Zoom}/{tileInfo.X}/{tileInfo.Y}";

                var zipEntry = zipArchive.CreateEntry(name, CompressionLevel.NoCompression);

                using var originalFileStream = new MemoryStream(bytes);
                using var zipEntryStream = zipEntry.Open();
                originalFileStream.CopyTo(zipEntryStream);
            }
        }

        return compressedFileStream.ToArray();
    }
}
