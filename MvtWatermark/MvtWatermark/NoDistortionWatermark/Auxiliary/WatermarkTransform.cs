using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvtWatermark.NoDistortionWatermark
{
    public static class WatermarkTransform
    {
        public static int? getIntFromBitArrayNullable(BitArray? bitArray)
        {
            if (bitArray == null)
                return null;

            if (bitArray.Length > 32)
                throw new ArgumentException("Argument length shall be at most 32 bits.");

            int[] array = new int[1];
            bitArray.CopyTo(array, 0);
            return array[0];
        }

        public static int getIntFromBitArray(BitArray bitArray)
        {
            if (bitArray.Length > 32)
                throw new ArgumentException("Argument length shall be at most 32 bits.");

            int[] array = new int[1];
            bitArray.CopyTo(array, 0);
            return array[0];
        }
    }
}
