using System;
using System.Collections;
using System.Collections.Generic;

namespace MvtWatermark.QimMvtWatermark.ExtractingMethods;

/// <summary>
/// Use in extracting function. Accumulates statistics for other M^M squres from tile and returns extracted bits.
/// For every bit accumulates general statistics of <see cref="PairOfStatistics.S0"/> and <see cref="PairOfStatistics.S1"/> and then computed extracted bit.
/// </summary>
public class GeneralExtractionMethod : IExtractingMethod
{
    /// <summary>
    /// Pair of statistics that got from tile.
    /// </summary>
    /// <param name="s0"></param>
    /// <param name="s1"></param>
    public class PairOfStatistics(int s0, int s1)
    {
        /// <summary>
        /// Count points of feature with value 0 from re-quantization matrix.
        /// </summary>
        public int S0 { get; set; } = s0;
        /// <summary>
        /// Count points of feature with value 1 from re-quantization matrix.
        /// </summary>
        public int S1 { get; set; } = s1;
    };

    /// <summary>
    /// Keep statistics for every index of message.
    /// </summary>
    public Dictionary<int, PairOfStatistics> Values { get; init; }
    /// <summary>
    /// Count bits per tile.
    /// </summary>
    public int CountBits { get; init; }
    /// <summary>
    /// Minimum value, if computed value will be smaller that this, computed value not counted.
    /// </summary>
    public double RelativeNumber { get; init; }
    /// <summary>
    /// Create a new instance of class.
    /// </summary>
    /// <param name="countBits">Count bits per tile (parameter <see cref="QimMvtWatermarkOptions.Nb"/>)</param>
    /// <param name="relativeNumber">Minimum value, if computed value will be smaller that this, computed value not counted (in options parameter <c>T2</c>)</param>
    public GeneralExtractionMethod(int countBits, double relativeNumber)
    {
        CountBits = countBits;
        RelativeNumber = relativeNumber;
        Values = new Dictionary<int, PairOfStatistics>(countBits);
        for (var i = 0; i < countBits; i++)
            Values.Add(i, new(0, 0));
    }

    /// <summary>
    /// Added statistics to <see cref="Values"/>.
    /// </summary>
    /// <param name="index">Index to add</param>
    /// <param name="s0">Count points with value 0</param>
    /// <param name="s1">Count points with value 1</param>
    public void AddStatistics(int index, int s0, int s1)
    {
        Values[index].S0 += s0;
        Values[index].S1 += s1;
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
            var s0 = Values[i].S0;
            var s1 = Values[i].S1;
            if ((double)Math.Abs(s0 - s1) / (s1 + s0) > RelativeNumber)
            {
                if (s1 > s0)
                    bits[i] = true;
                if (s0 > s1)
                    bits[i] = false;
            }
        }

        return bits;
    }
}
