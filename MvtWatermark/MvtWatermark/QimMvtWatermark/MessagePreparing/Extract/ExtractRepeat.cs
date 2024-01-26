using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;
public class ExtractRepeat : IMessageFromExtract<ulong>
{
    public ConcurrentDictionary<ulong, int> Indecies { get; init; }
    public int Size { get; init; }
    public bool[] Message { get; init; }
    public ExtractRepeat(IEnumerable<ulong> tileIds, int size)
    {
        Size = size;
        Message = new bool[size * tileIds.Count()];
        Indecies = new ConcurrentDictionary<ulong, int>();
        var i = 0;
        foreach (var tileId in tileIds)
        {
            Indecies[tileId] = i;
            i++;
        }
    }

    public BitArray Get() => new(Message);

    public void SetPart(BitArray? part, ulong index) => part?.CopyTo(Message, Indecies[index] * Size);
}
