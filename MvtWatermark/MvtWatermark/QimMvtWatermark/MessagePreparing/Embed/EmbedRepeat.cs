using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;
public class EmbedRepeat : IMessageForEmbed<ulong>
{
    public ConcurrentDictionary<ulong, bool[]> PartsOfMessage { get; init; }
    public EmbedRepeat(BitArray message, IEnumerable<ulong> tileIds, int size)
    {
        var messages = new bool[size * tileIds.Count()];

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

    public BitArray GetPart(ulong index) => new(PartsOfMessage[index]);
}
