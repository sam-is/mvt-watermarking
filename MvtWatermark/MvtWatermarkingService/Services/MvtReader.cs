using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
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
        catch(FormatException)
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
}
