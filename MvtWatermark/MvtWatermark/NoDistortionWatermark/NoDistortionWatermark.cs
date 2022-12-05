using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private NoDistortionWatermarkOptions _options;
        int[] _keySequence; // Убрать: Sk будет генерироваться в кажом тайле своя
        
        public NoDistortionWatermark(NoDistortionWatermarkOptions options)
        {
            _options = options;
        }

        public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
        {
            if (message.Count < _options.Nb)
            {
                throw new Exception("ЦВЗ меньше размера в options");
            }
            // message уже внутри будет делиться на фрагменты размером Nb

            var TileDict = tiles.WriteWM(message, key, _options);

            Console.WriteLine("После Embed"); // отладка

            var readerWM = new MapboxTileReaderWM();

            var toReturn = readerWM.Read(TileDict);

            Console.WriteLine("После ReadWM"); // отладка

            return toReturn;
        }

        public BitArray? Extract(VectorTileTree tiles, int key)
        {
            var readerWM = new MapboxTileReaderWM();
            var WatermarkInts = new List<int>();
            foreach (var tileIndex in tiles) // тут проверочки организовать пустое дерево или нет
            {
                WatermarkInts.Add(readerWM.ExtractWM(tiles[tileIndex].GetMapboxTileFromVectorTile(), tileIndex, _options, key));
            }

            return new BitArray(new int[] { WatermarkInts[0] });

            //throw new NotImplementedException();
        }


        public void EmbedAndWriteToFile(VectorTileTree tiles, int key, BitArray message, string path)
        {
            if (message.Count < _options.Nb)
            {
                throw new Exception("ЦВЗ меньше размера в options");
            }

            Console.WriteLine($"Количество элементарных сегментов:{_options.D}"); // отладка

            tiles.WriteVectorTileTreeToFiles(message, key, path, _options);

            Console.WriteLine("После Embed"); // отладка
        }
    }
}
