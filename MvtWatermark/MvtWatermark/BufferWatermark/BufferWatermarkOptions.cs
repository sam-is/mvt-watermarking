using System;

namespace MvtWatermark.BufferWatermark;

public class BufferWatermarkOptions
{
    /// <summary>
    /// Количество бит в каждом фрагменте ЦВЗ
    /// </summary>
    public int Nb { get; init; }

    /// <summary>
    /// Количество секторов, определяющих одинаковый бит фрагмента ЦВЗ
    /// </summary>
    public int M { get; init; }

    public int D { get; init; }

    public BufferWatermarkOptions(uint nb, int m=1)
    {
        M = m;
        Nb = Nb;
        D = m * Convert.ToInt32(Math.Pow(2, Nb));
    }
}
