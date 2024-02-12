using System.Collections;
using System.Text;

namespace MvtWatermarkConsole;
public static class MessageTransform
{
    public static Encoding Encoding { get; set; } = Encoding.UTF8;

    public static BitArray GetBitArray(string message)
    {
        var bytes = Encoding.GetBytes(message);
        return new BitArray(bytes);
    }

    public static string GetMessage(BitArray bitArray)
    {
        var bytes = new byte[(bitArray.Length - 1) / 8 + 1];
        bitArray.CopyTo(bytes, 0);
        return Encoding.GetString(bytes);
    }
}
