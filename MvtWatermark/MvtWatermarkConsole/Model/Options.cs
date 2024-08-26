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

    [Option('c', "config", Required = false, HelpText = "Path to config file with parameters for watermarking algorithm. \n" +
        "If not selected, the standard options will be selected.")]
    public string? ConfigPath { get; set; }

    [Option('w', "watermark", HelpText = "Watermark for embed. Required if select Mode Embed.")]
    public string? Watermark { get; set; }

    [Option('o', "output", HelpText =
        """
        Output path to save. 
        For mode Embed, where to save watermarked data. 
        For mode Extract, where to save watermarked message.
        For Embed required. If the source parameter is a mbtiles and the output parameter is not set, it will overwrite the tiles in the source.
        For extract optional, if not select watermark print into console.
        """)]
    public string? OutputPath { get; set; }

    [Option("minz", HelpText = "Optional. Minimum zoom.")]
    public int? MinZ { get; set; }

    [Option("maxz", HelpText = "Optional. Maximum zoom.")]
    public int? MaxZ { get; set; }

    [Option('g', "generate-config", HelpText = "Optional. If selected on Embed mode, it will generate a config file on path from the config parameter.")]
    public bool IsGenerateConfig { get; set; }

    [Option('u', "update-config", HelpText = "Optional. If selected on Embed mode, it will write length of message in the config file on path from the config parameter.")]
    public bool IsUpdateConfig { get; set; }

    [Option("no-tile-compression", HelpText = "Optional. Select if tile in source without compression")]
    public bool IsNoCompression { get; set; }
}
