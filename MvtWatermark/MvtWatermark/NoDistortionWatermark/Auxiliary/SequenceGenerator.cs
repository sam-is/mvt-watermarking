using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvtWatermark.NoDistortionWatermark.Auxiliary
{
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
            var rand = new Random(key);

            var maxBitArray = new BitArray(Nb, true);
            int maxInt = WatermarkTransform.getIntFromBitArray(maxBitArray);
            var howMuchEachValueInstancesAreInKeySequence = new int[maxInt + 1];

            var keySequence = new int[D / 2];

            keySequence[0] = 0;
            howMuchEachValueInstancesAreInKeySequence[0] = 1;
            for (int i = 1; i < D / 2; i++)
            {
                int value;
                do
                {
                    value = rand.Next(0, maxInt + 1);
                } while (howMuchEachValueInstancesAreInKeySequence[value] >= M);
                keySequence[i] = value;
                howMuchEachValueInstancesAreInKeySequence[value]++;
            } // нагенерили {Sk}

            //Console.WriteLine($"howMuchEachValueInstancesAreInKeySequence: {ConsoleWriter.GetArrayStr<int>(howMuchEachValueInstancesAreInKeySequence)}"); // отладка
            //Console.WriteLine($"keySequence: {ConsoleWriter.GetArrayStr<int>(keySequence)}"); // отладка

            return keySequence;
        }
    }
}
