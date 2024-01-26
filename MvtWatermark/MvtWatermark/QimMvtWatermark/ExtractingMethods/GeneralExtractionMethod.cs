using System;
using System.Collections;
using System.Collections.Generic;

namespace MvtWatermark.QimMvtWatermark.ExtractingMethods;
public class GeneralExtractionMethod : IExtractingMethod
{
    public class PairOfStatistics(int s0, int s1)
    {
        public int S0 { get; set; } = s0;
        public int S1 { get; set; } = s1;
    };

    public Dictionary<int, PairOfStatistics> Values { get; init; }
    public int CountBits { get; init; }
    public double RelativeNumber { get; init; }
    public GeneralExtractionMethod(int countBits, double relativeNumber)
    {
        CountBits = countBits;
        RelativeNumber = relativeNumber;
        Values = new Dictionary<int, PairOfStatistics>(countBits);
        for (var i = 0; i < countBits; i++)
            Values.Add(i, new(0, 0));
    }

    public void AddStatistics(int index, int s0, int s1)
    {
        Values[index].S0 += s0;
        Values[index].S1 += s1;
    }

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
