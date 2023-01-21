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
