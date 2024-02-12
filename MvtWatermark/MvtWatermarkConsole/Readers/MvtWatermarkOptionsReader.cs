using MvtWatermark.QimMvtWatermark;
using System.Text.Json;

namespace MvtWatermarkConsole.Readers;
public static class MvtWatermarkOptionsReader
{
    public static QimMvtWatermarkOptions Read(string path)
    {
        var json = File.ReadAllText(path);
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? throw new NullReferenceException();

        var k = Get<double>(dictionary, "k") ?? 0.9;
        var t1 = Get<int>(dictionary, "t1") ?? 5;
        var t2 = Get<double>(dictionary, "t2") ?? 0.2;
        var extent = Get<int>(dictionary, "extent") ?? 2048;
        var distance = Get<int>(dictionary, "distance") ?? 2;
        var nb = Get<int>(dictionary, "nb") ?? 8;
        var r = Get<int>(dictionary, "r") ?? 8;
        var m = Get<int>(dictionary, "m");
        var countMaps = Get<int>(dictionary, "countMaps") ?? 10;
        var isGeneralExtractionMethod = Get<bool>(dictionary, "isGeneralExtractionMethod") ?? false;
        var mode = GetMode(dictionary, "mode") ?? Mode.WithTilesMajorityVote;
        var messengeLength = Get<int>(dictionary, "messageLength");

        return new QimMvtWatermarkOptions(k, t2, t1, extent, distance, nb, r, m, countMaps, isGeneralExtractionMethod, mode, messengeLength);
    }

    public static T? Get<T>(Dictionary<string, object> dictionary, string key) where T : struct => dictionary.TryGetValue(key, out var value) ? value != null ? ((JsonElement)value).Deserialize<T>() : null : null;
    public static Mode? GetMode(Dictionary<string, object> dictionary, string key) => dictionary.TryGetValue(key, out var value) ? value != null ? Enum.Parse<Mode>(((JsonElement)value).ToString()) : null : null;
}
