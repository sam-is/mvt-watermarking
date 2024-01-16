using System.Collections;
using System.Text;

namespace MvtWatermarkingService.Services;

public class MessageProcessor
{
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    public BitArray GetBitArray(string message)
    {
        var bytes = Encoding.GetBytes(message);
        return new BitArray(bytes);
    }

    public string GetMessage(BitArray bitArray)
    {
        var bytes = new byte[(bitArray.Length - 1) / 8 + 1];
        bitArray.CopyTo(bytes, 0);
        return Encoding.GetString(bytes);
    }
}
