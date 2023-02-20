using System;
using System.Collections;

namespace MvtWatermark.NoDistortionWatermark.Auxiliary;

public static class WatermarkTransform
{
    /// <summary>
    /// Возвращает Int32 в десятичной системе счисления, полученный из двоичного nullable BitArray.
    /// Если bitArray равен null или не содержит элементов, возвращается null
    /// </summary>
    /// <param name="bitArray"></param>
    /// <returns></returns>
    public static int? GetIntFromBitArrayNullable(BitArray? bitArray)
    {
        if (bitArray == null || bitArray.Count == 0)
            return null;

        return GetIntFromBitArray(bitArray);
    }

    /// <summary>
    /// Возвращает Int32 в десятичной системе счисления, полученный из двоичного BitArray.
    /// Если bitArray пуст или содержит в себе больше 32-х значений, выбрасывается исключение
    /// </summary>
    /// <param name="bitArray"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static int GetIntFromBitArray(BitArray bitArray)
    {
        if (bitArray.Length == 0 || bitArray.Length > 32)
            throw new ArgumentException("Argument length should be bigger then 0 and less than 32 bits.");

        var array = new int[1];
        bitArray.CopyTo(array, 0);
        return array[0];
    }

    /// <summary>
    /// Сравнивает два объекта класса BitArray. Расширяет класс BitArray
    /// </summary>
    /// <param name="aArr"></param>
    /// <param name="bArr"></param>
    /// <returns></returns>
    public static bool AreEqual(this BitArray aArr, BitArray bArr)
    {
        if (aArr.Count != bArr.Count)
            return false; // возможно, так не стоит делать

        for (var i = 0; i < aArr.Count; i++)
        {
            if (aArr[i] != bArr[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Копирует nb элементов (битов) из исходного BitArray в другой BitArray, начиная с передаваемого индекса
    /// </summary>
    /// <param name="thisBitArr"></param>
    /// <param name="destinationBitArr"></param>
    /// <param name="index"></param>
    /// <param name="nb"></param>
    /// <exception cref="ArgumentException"></exception>
    public static void CopyNbBitsTo(this BitArray thisBitArr, BitArray destinationBitArr, int index, int nb)
    {
        if (destinationBitArr.Count < index + nb)
            throw new ArgumentException($"Cannot copy: destinationBitArr.Count ({destinationBitArr.Count}) < index + nb ({index + nb})");

        /*
        if (thisBitArr.Count != nb)
            throw new Exception($"Cannot copy: thisBitArr.Count ({thisBitArr.Count}) != nb ({nb})");
        */

        for (var i = 0; i < nb; i++)
        {
            destinationBitArr[i + index] = thisBitArr[i];
        }
    }
}
