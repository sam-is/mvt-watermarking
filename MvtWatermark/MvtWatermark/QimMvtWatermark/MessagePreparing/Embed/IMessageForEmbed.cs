using System.Collections;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;
public interface IMessageForEmbed<T>
{
    public BitArray GetPart(T index);
}
