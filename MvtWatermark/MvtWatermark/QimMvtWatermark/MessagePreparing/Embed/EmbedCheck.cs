using System.Collections;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;
public class EmbedCheck(BitArray message, int size) : IMessageForEmbed<int>
{
    public BitArray Message { get; } = message;
    public int Size { get; } = size;
    public BitArray GetPart(int index)
    {
        var bits = new BitArray(Size);
        for (var i = 0; i < Size; i++)
            bits[i] = Message[(i + index) % Message.Count];

        return bits;
    }
}
