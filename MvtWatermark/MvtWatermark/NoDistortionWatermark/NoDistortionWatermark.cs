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

namespace MvtWatermark.NoDistortionWatermark;

public class NoDistortionWatermark: IMvtWatermark
{
    private NoDistortionWatermarkOptions _options;
    
    public NoDistortionWatermark(NoDistortionWatermarkOptions options)
    {
        _options = options;
    }

    public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
    {
        // message уже внутри будет делиться на фрагменты размером Nb
        if (message.Count < _options.Nb)
        {
            throw new Exception("ЦВЗ меньше размера в options");
        }
        // проблема со встраиванием: если в последовательности бит попадётся подпоследовательность,
        // состоящая из нулей, её не получится встроить. А такая наверняка попадётся.
        // Значит, нужно изменить алгоритм, подстроив его под возможность встраивания нуля
        else if (message.Length == 1 && message[0] == false) 
        {
            throw new Exception("Встраивание ЦВЗ '0' невозможно из-за особенностей алгоритма");
        }

        var firstHalfOfTheKey = (short)key;

        //var TileDict = tiles.WriteWM(message, key, _options);
        var tileDict = tiles.WriteWM(message, firstHalfOfTheKey, _options);

        var readerWM = new MapboxTileReaderWM();

        var toReturn = readerWM.Read(tileDict);

        return toReturn;
    }

    /*
    public VectorTileTree Embed(VectorTile vt, int key, BitArray message)
    {
        // message уже внутри будет делиться на фрагменты размером Nb
        if (message.Count < _options.Nb)
        {
            throw new Exception("ЦВЗ меньше размера в options");
        }
        else if (message.Length == 1 && message[0] == false)
        {
            throw new Exception("Встраивание ЦВЗ '0' невозможно из-за особенностей алгоритма");
        }

        var TileDict = vt.WriteWM(message, key, vt.TileId, _options);

        //Console.WriteLine("После Embed"); // отладка

        var readerWM = new MapboxTileReaderWM();

        var toReturn = readerWM.Read(TileDict);

        //Console.WriteLine("После ReadWM"); // отладка

        return toReturn;
    }
    */

    public BitArray Extract(VectorTileTree tiles, int key)
    {
        var shortenedKey = (short)key;

        var readerWM = new MapboxTileReaderWM();
        var watermarkInts = new List<int>();
        foreach (var tileIndex in tiles) 
        {
            var extractedInt = readerWM.ExtractWM(tiles[tileIndex].GetMapboxTileFromVectorTile(), tileIndex, _options, shortenedKey);
            if (extractedInt != null)
                watermarkInts.Add(Convert.ToInt32(extractedInt));
        }

        if (watermarkInts.Count == 0)
            return new BitArray(new bool[] { false }); 
        // такой ЦВЗ не мог быть встроен, а значит, такой результат = "ничего не было извлечено"

        return new BitArray(new int[] { watermarkInts[0] }); 
        // пока что просто возвращается первый элемент из списка вотермарок
        // но потом ЦВЗ будет собираться в один из множества фрагментов, которые должны храниться в этом списке
    }

    /*
    public void EmbedAndWriteToFile(VectorTileTree tiles, int key, BitArray message, string path)
    {
        if (message.Count < _options.Nb)
        {
            throw new Exception("ЦВЗ меньше размера в options");
        }
        else if (message.Length == 1 && message[0] == false)
        {
            throw new Exception("Встраивание ЦВЗ '0' невозможно из-за особенностей алгоритма");
        }

        //Console.WriteLine($"Количество элементарных сегментов:{_options.D}"); // отладка

        tiles.WriteVectorTileTreeToFiles(message, key, path, _options);

        //Console.WriteLine("После Embed"); // отладка
    }
    */
}
