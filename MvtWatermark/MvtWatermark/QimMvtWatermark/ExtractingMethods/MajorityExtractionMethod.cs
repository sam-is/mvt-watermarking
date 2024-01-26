using System.Collections;
using System.Collections.Generic;

namespace MvtWatermark.QimMvtWatermark.ExtractingMethods;
public class MajorityExtractionMethod : IExtractingMethod
{
    public Dictionary<int, int> Values { get; init; }
    public int CountBits { get; init; }
    public MajorityExtractionMethod(int countBits)
    {
        CountBits = countBits;
        Values = new Dictionary<int, int>(countBits);
        for (var i = 0; i < countBits; i++)
            Values.Add(i, 0);
    }

    public void AddStatistics(int index, int s0, int s1)
    {
        if (s0 > s1)
            Values[index] -= 1;

        if (s1 > s0)
            Values[index] += 1;
    }

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
