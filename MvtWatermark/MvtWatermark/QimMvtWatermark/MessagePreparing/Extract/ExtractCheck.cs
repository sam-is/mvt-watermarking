using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;
public class ExtractCheck(IEnumerable<ulong> tileIds, int size) : IMessageFromExtract<int>
{
    public bool[] Message { get; } = new bool[size * tileIds.Count()];
    public int LastIndex { get; private set; }
    public int Size { get; } = size;

    public BitArray Get() => new(Message.Take(LastIndex + Size).ToArray());
    public void SetPart(BitArray part, int index)
    {
        part.CopyTo(Message, index);
        LastIndex = index;
    }
}
