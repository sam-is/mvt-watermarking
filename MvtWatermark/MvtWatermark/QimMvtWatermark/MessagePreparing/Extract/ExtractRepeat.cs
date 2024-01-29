using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;

/// <summary>
/// Preparing message for extracting with <see cref="Mode.Repeat"/>.
/// </summary>
public class ExtractRepeat : IMessageFromExtract<ulong>
{
    /// <summary>
    /// Keep relate tileId with index of part message.
    /// </summary>
    public ConcurrentDictionary<ulong, int> Indecies { get; init; }
    /// <summary>
    /// Bits per tile.
    /// </summary>
    public int Size { get; init; }
    /// <summary>
    /// Result message.
    /// </summary>
    public bool[] Message { get; init; }
    /// <summary>
    /// Create a new intance of class.
    /// </summary>
    /// <param name="tileIds">Ids of tiles in tile tree</param>
    /// <param name="size">Bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
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

    /// <summary>
    /// Computs extracted message.
    /// </summary>
    /// <returns>Extracted message</returns>
    public BitArray Get() => new(Message);

    /// <summary>
    /// Save part of message by index.
    /// </summary>
    /// <param name="part">Extracted part of message</param>
    /// <param name="index">Index of tile</param>
    public void SetPart(BitArray? part, ulong index) => part?.CopyTo(Message, Indecies[index] * Size);
}
