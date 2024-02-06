using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;

/// <summary>
/// Preparing message for embed with <see cref="Mode.WithTilesMajorityVote"/>.
/// </summary>
public class EmbedRepeat : IMessageForEmbed<ulong>
{
    public ConcurrentDictionary<ulong, bool[]> PartsOfMessage { get; init; }

    /// <summary>
    /// Create a new instance of class.
    /// </summary>
    /// <param name="message">Embedded message</param>
    /// <param name="tileIds">Ids of tiles in tile tree</param>
    /// <param name="size">Bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
    public EmbedRepeat(BitArray message, List<ulong> tileIds, int size)
    {
        var messages = new bool[size * tileIds.Count];

        for (var i = 0; i < messages.Length; i++)
            messages[i] = message[i % message.Count];

        PartsOfMessage = new ConcurrentDictionary<ulong, bool[]>();
        var iter = 0;
        foreach (var tileId in tileIds)
        {
            PartsOfMessage[tileId] = messages.Take(new Range(iter, iter + size)).ToArray();
            iter++;
        }
    }

    /// <summary>
    /// Computes and returns part of message by tile index.
    /// </summary>
    /// <param name="index">Index of tile</param>
    /// <returns>Part of message</returns>
    public BitArray GetPart(ulong index) => new(PartsOfMessage[index]);
}