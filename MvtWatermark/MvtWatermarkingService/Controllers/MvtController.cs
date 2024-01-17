using Microsoft.AspNetCore.Mvc;
using MvtWatermark.QimMvtWatermark;
using MvtWatermarkingService.Services;

namespace MvtWatermarkingService.Controllers;

[Route("mvt_watermarking")]
public class MvtController(OptionsBuilder optionsBuilder, MvtReader mvtReader, MessageProcessor messageProcessor) : Controller
{
    public OptionsBuilder OptionsBuilder { get; } = optionsBuilder;
    public MvtReader MvtReader { get; } = mvtReader;
    public MessageProcessor MessageProcessor { get; } = messageProcessor;

    [Route("embed")]
    [HttpGet]
    public IActionResult Embed(string url, string message, int key)
    {
        var tile = MvtReader.Read(url);
        if (tile == null)
            return NotFound();

        var qimMvtWatermark = new QimMvtWatermark(OptionsBuilder.GetOptions());
        var tileWatermarked = qimMvtWatermark.Embed(tile, Math.Abs(key + (int)tile.TileId), MessageProcessor.GetBitArray(message));

        if (tileWatermarked == null)
            return BadRequest();

        return File(MvtReader.VectorTileToByteArray(tileWatermarked), "application/vnd.mapbox-vector-tile", $"{tileWatermarked.TileId}.pbf");
    }

    [Route("extract")]
    [HttpGet]
    public IActionResult Extract(string url, int key)
    {
        var tile = MvtReader.Read(url);
        if (tile == null)
            return NotFound();

        var qimMvtWatermark = new QimMvtWatermark(OptionsBuilder.GetOptions());
        var message = qimMvtWatermark.Extract(tile, Math.Abs(key + (int)tile.TileId));

        if (message == null)
            return BadRequest();

        return Ok(message);
    }

    [Route("embed_bbox")]
    [HttpGet]
    public IActionResult EmbedBbox(string url, int minX, int maxX, int minY, int maxY, int z, string message, int key)
    {
        var tileTree = MvtReader.Read(url, minX, maxX, minY, maxY, z);
        if (tileTree == null)
            return NotFound();

        var qimMvtWatermark = new QimMvtWatermark(OptionsBuilder.GetOptions());
        var tileTreeWatermarked = qimMvtWatermark.Embed(tileTree, key, MessageProcessor.GetBitArray(message));

        if (tileTreeWatermarked == null)
            return BadRequest();

        return File(MvtReader.VectorTileTreeToZip(tileTreeWatermarked), "application/zip", $"tiles.zip");
    }

    [Route("extract_bbox")]
    [HttpGet]
    public IActionResult ExtractBbox(string url, int minX, int maxX, int minY, int maxY, int z, int key, int messageLength)
    {
        var tileTree = MvtReader.Read(url, minX, maxX, minY, maxY, z);
        if (tileTree == null)
            return NotFound();

        var options = OptionsBuilder.GetOptions();
        options.MessageLength = messageLength;
        var qimMvtWatermark = new QimMvtWatermark(options);
        
        var message = qimMvtWatermark.Extract(tileTree, key);

        if (message == null)
            return BadRequest();

        return Ok(message);
    }
}
