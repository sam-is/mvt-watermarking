using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;
public class EmbedMajorityVoice : IMessageForEmbed<ulong>
{
    public ConcurrentDictionary<int, bool[]> PartsOfMessage { get; init; }
    public BitArray Message { get; init; }
    public int Size { get; init; }
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

    public BitArray GetPart(ulong index) => new(PartsOfMessage[Convert.ToInt32(index % (ulong)Math.Floor((double)Message.Length / Size))]);
}
