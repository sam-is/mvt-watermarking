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

    /// <summary>
    /// Количество секторов всего, считается в конструкторе (D = M * Nb)
    /// </summary>
    public int D { get; init; }

    /// <summary>
    /// Буфер для данного набора тайлов
    /// </summary>
    public int Buffer {  get; init; }

    public BufferWatermarkOptions(uint nb, uint buffer/*=дефолтное значение*/, int m=1) // дефолтное значение посчитать надо или чо
    {
        M = m;
        Nb = (int)nb;
        Buffer = (int)buffer;
        D = m * Nb;
    }
}
