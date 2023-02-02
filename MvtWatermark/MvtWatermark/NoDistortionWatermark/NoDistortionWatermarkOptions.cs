﻿using System;

namespace MvtWatermark.NoDistortionWatermark;

public class NoDistortionWatermarkOptions
{
    /// <summary>
    /// Виды нетипичной геометрии для встраивания
    /// </summary>
    public enum AtypicalEncodingTypes
    {
        MtLtLt,
        MtLtMt, // нормально не работает, от этого придётся отказаться, судя по всему
        NLtCommands
    }
    /// <summary>
    /// Количество элементарных сегментов.
    /// Вычисление этого параметра происходит в конструкторе
    /// </summary>
    public int D { get; init; }

    /// <summary>
    /// Количество элементарных сегментов с одинаковым значением (для каждого значения)
    /// </summary>
    public int M { get; init; }

    /// <summary>
    /// Количество бит в ЦВЗ
    /// </summary>
    public int Nb { get; init; }

    /// <summary>
    /// Ключ для генерации массива размером, равным количеству реальных сегментов в одном элементарном 
    /// (разное в зависимости от конкретного лайнстринга). Массив состоит из нулей и единиц. 
    /// Затем этот массив анализируется уже в методе Encode. 
    /// Если по индексу реального элемента в массиве 1, то встраивается нетипичная геометрия, если 0, то ничего не встраивается.
    /// </summary>
    public int Ls { get; init; }

    /// <summary>
    /// Количество элементов LineString, в которые нужно встроить ЦВЗ, в каждом тайле.
    /// Похоже, тоже лучше передавать ключ, а потом уже в самом тайле разбираться
    /// </summary>
    public int Lf { get; init; }

    /// <summary>
    /// Выбранный вид нетипичной геометрии
    /// </summary>
    public AtypicalEncodingTypes AtypicalEncodingType { get; set; }

    /// <summary>
    /// Флаг, показывающий, используется ли первая половина объекта LineString, или вторая.
    /// По умолчанию (false) используется первая половина.
    /// </summary>
    public bool SecondHalfOfLineStringIsUsed { get; set; } = false;

    public NoDistortionWatermarkOptions(int m, int nb, int ls, int lf, 
        AtypicalEncodingTypes atypicalEncodingType = AtypicalEncodingTypes.MtLtLt, bool secondHalfOfLineStringIsUsed = false)
    {
        M = m;
        Nb = nb;
        Ls = ls;
        Lf = lf;
        AtypicalEncodingType = atypicalEncodingType;
        SecondHalfOfLineStringIsUsed = secondHalfOfLineStringIsUsed;
        D = Convert.ToInt32(2 * m * Math.Pow(2, Nb));
    }
}