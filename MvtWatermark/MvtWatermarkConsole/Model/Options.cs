using CommandLine;

namespace MvtWatermarkConsole.Model;

public class Options
{
    [Option('s', "source", Required = true, HelpText = "Source of data. May be: mbtiles or folder with tile tree.")]
    public required string Source { get; set; }

    [Option('m', "mode", Required = true, HelpText = "Mode. Embed for embeding, Extract for extracting.")]
    public required Mode Mode { get; set; }

    [Option('k', "key", Required = true, HelpText = "Secret key. Must be Integer.")]
    public required int Key { get; set; }

    [Option('c', "config", Required = false, HelpText = "Path to config file with parameters for watermarking algorthm. \n" +
        "If not selected, the standard options will be selected.")]
    public string? ConfigPath { get; set; }

    [Option('w', "watermark", HelpText = "Watermark for embed. Required if select Mode Embed.")]
    public string? Watermark { get; set; }

    [Option('o', "output", HelpText = "Output path to save. For Mode Embed, where to save watermarked data. \n" +
        "For Mode Extract, where to save watermarked message. \n" +
        "For Embed required. For extract optional, if not select watermark print into console.")]
    public string? OutputPath { get; set; }

    [Option("minz", HelpText = "Optional. Minimum zoom.")]
    public int? MinZ { get; set; }

    [Option("maxz", HelpText = "Optional. Maximum zoom.")]
    public int? MaxZ { get; set; }
}
