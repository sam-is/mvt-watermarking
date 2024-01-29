using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;

/// <summary>
/// Preparing message for extracting with <see cref="Mode.WithCheck"/>.
/// </summary>
/// <param name="tileIds">Ids of tiles in tile tree</param>
/// <param name="size">Bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
public class ExtractCheck(IEnumerable<ulong> tileIds, int size) : IMessageFromExtract<int>
{
    /// <summary>
    /// Result message.
    /// </summary>
    public bool[] Message { get; } = new bool[size * tileIds.Count()];
    /// <summary>
    /// Last index of message part.
    /// </summary>
    public int LastIndex { get; private set; }
    /// <summary>
    /// Bits per message.
    /// </summary>
    public int Size { get; } = size;

    /// <summary>
    /// Computs extracted message.
    /// </summary>
    /// <returns>Extracted message</returns>
    public BitArray Get() => new(Message.Take(LastIndex + Size).ToArray());

    /// <summary>
    /// Save part of message by index
    /// </summary>
    /// <param name="part">Extracted part of message</param>
    /// <param name="index">Index to insert extracted part</param>
    public void SetPart(BitArray? part, int index)
    {
        if (part == null)
            return;
        part.CopyTo(Message, index);
        LastIndex = index;
    }
}
