namespace MvtWatermarkConsole;
public static class TypeChecker
{
    public static bool IsMbtiles(string path) => Path.GetExtension(path) == ".mbtiles";
}
