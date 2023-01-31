using System;
using System.Collections;

namespace MvtWatermark.NoDistortionWatermark.Auxiliary;

internal static class SequenceGenerator
{
    /// <summary>
    /// Генерирует случайную последовательность Sk на основе ключа key и параметров Nb, D, M
    /// </summary>
    /// <param name="key">ключ, используется как seed для рандомайзера</param>
    /// <param name="Nb"></param>
    /// <param name="D"></param>
    /// <param name="M"></param>
    /// <returns></returns>
    internal static int[] GenerateSequence(int key, int Nb, int D, int M)
    {
        // Возможно, алгоритм стоит переделать. Например, с использованием упорядоченного множества
        var rand = new Random(key);

        var maxBitArray = new BitArray(Nb, true);
        var maxInt = WatermarkTransform.GetIntFromBitArray(maxBitArray);
        var howMuchEachValueInstancesAreInKeySequence = new int[maxInt + 1];

        var keySequence = new int[D / 2];

        keySequence[0] = 0;
        howMuchEachValueInstancesAreInKeySequence[0] = 1;
        for (var i = 1; i < D / 2; i++)
        {
            int value;
            do
            {
                value = rand.Next(0, maxInt + 1);
            } while (howMuchEachValueInstancesAreInKeySequence[value] >= M);
            keySequence[i] = value;
            howMuchEachValueInstancesAreInKeySequence[value]++;
        } 

        return keySequence;
    }
}
