using System.Collections;

namespace MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;

public interface IMessageForEmbed<in T>
{
    public BitArray GetPart(T index);
}
