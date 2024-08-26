using MvtWatermark.QimMvtWatermark;
using System.Text.Json;

namespace MvtWatermarkConsole.Writers;
public class MvtWatermarkOptionsWriter
{
    private static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions { WriteIndented = true };
    public static void Write(QimMvtWatermarkOptions options, string path)
    {
        var dict = new Dictionary<string, object?>
        {
            { "k", options.Delta2 / options.T2 },
            { "t2", options.T2 },
            { "t1", options.T1 },
            { "extent", options.Extent },
            { "distance", options.Distance },
            { "nb", options.Nb },
            { "r", options.R },
            { "countMaps", options.Maps.Count },
            { "isGeneralExtractionMethod", options.IsGeneralExtractionMethod },
            { "mode", options.Mode },
            { "messageLength", options.MessageLength }
        };

        var json = JsonSerializer.Serialize(dict, JsonSerializerOptions);

        File.WriteAllText(path, json);
    }
}
