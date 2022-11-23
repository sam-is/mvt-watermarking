using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox.Watermarking;

namespace MvtWatermark.NoDistortionWatermark
{
    public class NoDistortionWatermark: IMvtWatermark
    {
        private int m;
        private int D;
        public NoDistortionWatermark(int m_par)
        {
            m = m_par;
        }
        public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
        {
            //BitArray keyBitArray = new BitArray(new int[] { key });

            var rand = new Random(key); // пока так, потом Random, наверное, будет создаваться выше и передаваться как параметр
            var DElementarySegmentsCount = Convert.ToInt32(2 * m * Math.Pow(2, message.Count));

            this.D = DElementarySegmentsCount;

            Console.WriteLine($"Количество элементарных сегментов:{this.D}"); // отладка

            var maxBitArray = new BitArray(message.Count, true);
            var MaxInt = WatermarkTransform.getIntFromBitArray(maxBitArray);
            var HowMuchEachValue = new int[MaxInt + 1];

            var keySequence = new int[DElementarySegmentsCount / 2];

            Console.WriteLine("Перед заполнением keySequence"); // отладка

            for (int i = 0; i < DElementarySegmentsCount / 2; i++)
            {
                int value;
                do
                {
                    value = Convert.ToInt32(rand.Next(MaxInt + 1));
                } while (HowMuchEachValue[value] >= 2);
                keySequence[i] = value;

                Console.WriteLine(value); // отладка

                HowMuchEachValue[value]++;
            } // нагенерили {Sk}

            Console.WriteLine("Последовательность сгенерирована"); // отладка
            Console.WriteLine(keySequence.ToString); // отладка

            var TileDict = tiles.WriteWM(message, this.D, keySequence);

            Console.WriteLine("После Embed"); // отладка

            var readerWM = new MapboxTileReaderWM();

            var toReturn = readerWM.Read(TileDict);

            Console.WriteLine("После ReadWM"); // отладка

            return toReturn;
        }

        public BitArray Extract(VectorTileTree tiles, int key)
        {
            var rand = new Random(key);
            var maxBitArray = new BitArray(D /(2*m), true);
            var MaxInt = WatermarkTransform.getIntFromBitArray(maxBitArray);
            var HowMuchEachValue = new int[MaxInt];
            var keySequence = new int[this.D / 2];

            for (int i = 0; i < this.D / 2; i++)
            {
                int value;
                do
                {
                    value = rand.Next(MaxInt);
                } while (HowMuchEachValue[value] >= 2);
                keySequence[i] = value;
                HowMuchEachValue[value]++;
            } // нагенерили {Sk}

            //throw new NotImplementedException();
            return maxBitArray; // это временно
        }


        public void EmbedAndWriteToFile(VectorTileTree tiles, int key, BitArray message)
        {

            var rand = new Random(key); // пока так, потом Random, наверное, будет создаваться выше и передаваться как параметр
            var DElementarySegmentsCount = Convert.ToInt32(2 * m * Math.Pow(2, message.Count));

            this.D = DElementarySegmentsCount;

            Console.WriteLine($"Количество элементарных сегментов:{this.D}"); // отладка

            var maxBitArray = new BitArray(message.Count, true);
            var MaxInt = WatermarkTransform.getIntFromBitArray(maxBitArray);
            var HowMuchEachValue = new int[MaxInt + 1];

            var keySequence = new int[DElementarySegmentsCount / 2];

            Console.WriteLine("Перед заполнением keySequence"); // отладка

            for (int i = 0; i < DElementarySegmentsCount / 2; i++)
            {
                int value;
                do
                {
                    value = Convert.ToInt32(rand.Next(MaxInt + 1));
                } while (HowMuchEachValue[value] >= 2);
                keySequence[i] = value;

                Console.WriteLine(value); // отладка

                HowMuchEachValue[value]++;
            } // нагенерили {Sk}

            Console.WriteLine("Последовательность сгенерирована"); // отладка

            string path = "C:\\SerializedTiles\\SerializedWithWatermark";
            tiles.WriteVectorTileTreeToFiles(message, this.D, keySequence, path);

            Console.WriteLine("После Embed"); // отладка
        }
    }
}
