using System.Collections;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;

/// <summary>
/// Preparing message for embed with <see cref="Mode.WithCheck"/>.
/// </summary>
/// <param name="message">Embedded message</param>
/// <param name="size">Bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
public class EmbedCheck(BitArray message, int size) : IMessageForEmbed<int>
{
    /// <summary>
    /// Embedded message.
    /// </summary>
    public BitArray Message { get; } = message;

    /// <summary>
    /// Bits per tile.
    /// </summary>
    public int Size { get; } = size;

    /// <summary>
    /// Computes and returns part of message by index.
    /// </summary>
    /// <param name="index">Index of part</param>
    /// <returns>Part of message</returns>
    public BitArray GetPart(int index)
    {
        var bits = new BitArray(Size);
        for (var i = 0; i < Size; i++)
            bits[i] = Message[(i + index) % Message.Count];

        return bits;
    }
}
