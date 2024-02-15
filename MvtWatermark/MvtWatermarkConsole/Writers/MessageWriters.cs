namespace MvtWatermarkConsole.Writers;
public static class MessageWriters
{
    public static void Write(string path, string message)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine(message);
    }
}
