using System;
using System.Collections;
using System.Collections.Concurrent;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;

/// <summary>
/// Preparing message for extracting with <see cref="Mode.WithTilesMajorityVote"/>.
/// </summary>
internal class ExtractMajorityVoice : IMessageFromExtract<ulong>
{
    /// <summary>
    /// Parts of message.
    /// </summary>
    public ConcurrentDictionary<int, int[]> PartsOfMessage { get; init; }
    /// <summary>
    /// Size of embeded message.
    /// </summary>
    public int SizeMessage { get; init; }
    /// <summary>
    /// Bits per tile.
    /// </summary>
    public int Size { get; init; }
    /// <summary>
    /// Create a new intance of class.
    /// </summary>
    /// <param name="sizeMessage">Size of embeded message</param>
    /// <param name="size">Bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
    /// <exception cref="ArgumentNullException"></exception>
    public ExtractMajorityVoice(int? sizeMessage, int size)
    {
        SizeMessage = sizeMessage ?? throw new ArgumentNullException(nameof(sizeMessage));
        Size = size;
        PartsOfMessage = new ConcurrentDictionary<int, int[]>();
        var indices = (int)Math.Floor((double)sizeMessage / size);
        for (var i = 0; i < indices; i++)
            PartsOfMessage[i] = new int[size];
    }

    /// <summary>
    /// Computs extracted message.
    /// </summary>
    /// <returns>Extracted message</returns>
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

    /// <summary>
    /// Save part of message by index.
    /// </summary>
    /// <param name="part">Extracted part of message</param>
    /// <param name="index">Index of tile</param>
    public void SetPart(BitArray? part, ulong index)
    {
        if (part == null)
            return;

        var indexInDictionary = Convert.ToInt32(index % (ulong)Math.Floor((double)SizeMessage / Size));

        for (var i = 0; i < part.Length; i++)
            PartsOfMessage[indexInDictionary][i] += part[i] == true ? 1 : -1;
    }
}
