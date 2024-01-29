using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;

/// <summary>
/// Preparing message for embed with <see cref="Mode.WithTilesMajorityVote"/>.
/// </summary>
public class EmbedMajorityVoice : IMessageForEmbed<ulong>
{
    /// <summary>
    /// Keep parts of message.
    /// </summary>
    public ConcurrentDictionary<int, bool[]> PartsOfMessage { get; init; }
    /// <summary>
    /// Embeded message.
    /// </summary>
    public BitArray Message { get; init; }
    /// <summary>
    /// Bits per tile.
    /// </summary>
    public int Size { get; init; }
    /// <summary>
    /// Create a new intance of class.
    /// </summary>
    /// <param name="message">Embeded message</param>
    /// <param name="size">Bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
    public EmbedMajorityVoice(BitArray message, int size)
    {
        Message = message;
        Size = size;
        PartsOfMessage = new ConcurrentDictionary<int, bool[]>();
        var step = (double)message.Length / size;
        var boolArray = new bool[message.Length];
        message.CopyTo(boolArray, 0);
        var iter = 0;
        for (var i = 0; i < step; i++)
        {
            PartsOfMessage[i] = boolArray.Take(new Range(iter, iter + size)).ToArray();
            iter += size;
        }
    }

    /// <summary>
    /// Computes and returns part of message by tile index.
    /// </summary>
    /// <param name="index">Index of tile</param>
    /// <returns>Part of message</returns>
    public BitArray GetPart(ulong index) => new(PartsOfMessage[Convert.ToInt32(index % (ulong)Math.Floor((double)Message.Length / Size))]);
}
