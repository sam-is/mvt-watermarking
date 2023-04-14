using System;
using System.Collections;

namespace MvtWatermark.NoDistortionWatermark.Auxiliary;

internal static class SequenceGenerator
{
    /// <summary>
    /// Генерирует случайную последовательность Sk на основе ключа key и параметров nb, d, m
    /// </summary>
    /// <param name="key"></param>
    /// <param name="nb"></param>
    /// <param name="d"></param>
    /// <param name="m"></param>
    /// <returns></returns>
    internal static int[] GenerateSequence(int key, int nb, int d, int m)
    {
        var rand = new Random(key);

        var maxBitArray = new BitArray(nb, true);
        var maxInt = WatermarkTransform.GetIntFromBitArray(maxBitArray);
        var howMuchEachValueInstancesAreInKeySequence = new int[maxInt + 1];

        var keySequence = new int[d / 2];

        keySequence[0] = 0;
        howMuchEachValueInstancesAreInKeySequence[0] = 1;
        for (var i = 1; i < d / 2; i++)
        {
            int value;
            do
            {
                value = rand.Next(0, maxInt + 1);
            } while (howMuchEachValueInstancesAreInKeySequence[value] >= m);
            keySequence[i] = value;
            howMuchEachValueInstancesAreInKeySequence[value]++;
        } 

        return keySequence;
    }
}
