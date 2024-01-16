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
        var tilewatermarked = qimMvtWatermark.Embed(tile, Math.Abs(key + (int)tile.TileId), MessageProcessor.GetBitArray(message));

        if (tilewatermarked == null)
            return BadRequest();

        return File(MvtReader.VectorTileToByteArray(tilewatermarked), "application/vnd.mapbox-vector-tile", $"{tilewatermarked.TileId}.pbf");
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
}
