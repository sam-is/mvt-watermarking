using System.Collections;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;
public interface IMessageFromExtract<T>
{
    public void SetPart(BitArray? part, T index);
    public BitArray Get();
}
