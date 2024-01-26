using System;
using System.Collections;
using System.Collections.Concurrent;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;
internal class ExtractMajorityVoice : IMessageFromExtract<ulong>
{
    public ConcurrentDictionary<int, int[]> PartsOfMessage { get; init; }
    public int SizeMessage { get; init; }
    public int Size { get; init; }
    public ExtractMajorityVoice(int? sizeMessage, int size)
    {
        SizeMessage = sizeMessage ?? throw new ArgumentNullException(nameof(sizeMessage));
        Size = size;
        PartsOfMessage = new ConcurrentDictionary<int, int[]>();
        var indices = (int)Math.Floor((double)sizeMessage / size);
        for (var i = 0; i < indices; i++)
            PartsOfMessage[i] = new int[size];
    }

    public BitArray Get()
    {
        var countIndices = PartsOfMessage.Keys.Count;
        var result = new bool[Size * countIndices];

        for (var i = 0; i < countIndices; i++)
        {
            for (var j = 0; j < PartsOfMessage[i].Length; j++)
            {
                if (PartsOfMessage[i][j] > 0)
                    result[i * Size + j] = true;
                else
                    result[i * Size + j] = false;
            }
        }

        return new BitArray(result);
    }

    public void SetPart(BitArray? part, ulong index)
    {
        if (part == null)
            return;

        var indexInDictionary = Convert.ToInt32(index % (ulong)Math.Floor((double)SizeMessage / Size));

        for (var i = 0; i < part.Length; i++)
            PartsOfMessage[indexInDictionary][i] += part[i] == true ? 1 : -1;
    }
}
