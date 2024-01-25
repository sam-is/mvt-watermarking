using System.Collections;

namespace MvtWatermark.QimMvtWatermark.ExtractingMethods;
public interface IExtractingMethod
{
    public void AddStatistics(int index, int s0, int s1);
    public BitArray GetBits();
}
