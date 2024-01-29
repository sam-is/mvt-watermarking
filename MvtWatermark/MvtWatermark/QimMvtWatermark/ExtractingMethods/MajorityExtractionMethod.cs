using System.Collections;
using System.Collections.Generic;

namespace MvtWatermark.QimMvtWatermark.ExtractingMethods;

/// <summary>
/// Use in extracting function. Accumulates statistics for other M^M squres from tile and returns extracted bits.
/// For every M^M squres compute bit that embed in squre. Resulted bit get with majority voice of all M^M squres that consist bit with same index.
/// </summary>
public class MajorityExtractionMethod : IExtractingMethod
{
    /// <summary>
    /// Keep statistics for every index of message.
    /// </summary>
    public Dictionary<int, int> Values { get; init; }
    /// <summary>
    /// Count bits per tile.
    /// </summary>
    public int CountBits { get; init; }
    /// <summary>
    /// Create a new instance of class.
    /// </summary>
    /// <param name="countBits">Count bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
    public MajorityExtractionMethod(int countBits)
    {
        CountBits = countBits;
        Values = new Dictionary<int, int>(countBits);
        for (var i = 0; i < countBits; i++)
            Values.Add(i, 0);
    }

    /// <summary>
    /// Added statistics to <see cref="Values"/>.
    /// </summary>
    /// <param name="index">Index to add</param>
    /// <param name="s0">Count points with value 0</param>
    /// <param name="s1">Count points with value 1</param>
    public void AddStatistics(int index, int s0, int s1)
    {
        if (s0 > s1)
            Values[index] -= 1;

        if (s1 > s0)
            Values[index] += 1;
    }

    /// <summary>
    /// Compute and return bits that extracted from tile.
    /// </summary>
    /// <returns>Extracted bits</returns>
    public BitArray GetBits()
    {
        var bits = new BitArray(CountBits, false);
        for (var i = 0; i < CountBits; i++)
        {
            if (Values[i] > 0)
                bits[i] = true;
            if (Values[i] < 0)
                bits[i] = false;
        }

        return bits;
    }
}
