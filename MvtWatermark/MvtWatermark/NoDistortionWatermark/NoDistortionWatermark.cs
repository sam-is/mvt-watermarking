using System;
using System.Collections;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark.Auxiliary;
using System.Linq;

namespace MvtWatermark.NoDistortionWatermark;

public class NoDistortionWatermark: IMvtWatermark
{
    private readonly NoDistortionWatermarkOptions _options;
    
    public NoDistortionWatermark(NoDistortionWatermarkOptions options)
    {
        _options = options;
        _embededMessage = null;
    }

    private BitArray? _embededMessage;
    public BitArray? EmbededMessage
    {
        get => _embededMessage;
    }

    /// <summary>
    /// Встраивание ЦВЗ в формате BitArray в векторные тайлы в формате VectorTileTree (MVT) с использованием ключа
    /// </summary>
    /// <param name="tiles"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
    {
        // message уже внутри будет делиться на фрагменты размером Nb
        /*
        if (message.Count < _options.Nb)
        {
            throw new ArgumentException("ЦВЗ меньше размера в options");
        }
        */
        // проблема со встраиванием: если в последовательности бит попадётся подпоследовательность,
        // состоящая из нулей, её не получится встроить. А такая наверняка попадётся.
        // Значит, нужно изменить алгоритм, подстроив его под возможность встраивания нуля

        /*
        //if (message.Length == 1 && message[0] == false) 
        if (WatermarkTransform.GetIntFromBitArray(message) == 0)
        {
            throw new Exception("Встраивание ЦВЗ '0' невозможно из-за особенностей алгоритма");
        }
        */

        var firstHalfOfTheKey = (short)key;

        //var tileDict = tiles.WriteWm(message, firstHalfOfTheKey, _options);

        var nonStaticMapboxTileWriterWm = new NonStaticMapboxTileWriterWm();
        var tileDict = nonStaticMapboxTileWriterWm.WriteWm(tiles, message, firstHalfOfTheKey, _options, out _embededMessage);

        var readerWm = new MapboxTileReaderWm();
        var tilesWithWatermark = readerWm.Read(tileDict);

        return tilesWithWatermark;
    }

    /// <summary>
    /// Извлечение ЦВЗ из VectorTileTree с использованием ключа
    /// </summary>
    /// <param name="tiles"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public BitArray Extract(VectorTileTree tiles, int key)
    {
        var shortenedKey = (short)key;

        var readerWm = new MapboxTileReaderWm();
        //var watermarkInts = new List<int>();
        var extractedWatermarkString = new BitArray(_options.Nb * tiles.Count());
        //var extractedWatermarkList = new List<bool>();
        var correctWatermarkTilesCounter = 0;
        var index = 0;
        foreach (var tileIndex in tiles) 
        {
            var extractedInt = readerWm.ExtractWm(tiles[tileIndex].GetMapboxTileFromVectorTile(), tileIndex, _options, shortenedKey);
            if (extractedInt != null)
            {
                //var 
                //watermarkInts.Add(Convert.ToInt32(extractedInt));
                var bitArr = new BitArray(new int[] { Convert.ToInt32(extractedInt) });
                bitArr.CopyNbBitsTo(extractedWatermarkString, index, _options.Nb);
                index += _options.Nb;
                correctWatermarkTilesCounter++;
            }
        }

        var resultWatermarkString = new BitArray(_options.Nb * correctWatermarkTilesCounter);
        extractedWatermarkString.CopyNbBitsTo(resultWatermarkString, 0, resultWatermarkString.Count);

        /*
        if (watermarkInts.Count == 0)
            return new BitArray(new[] { false });
        */
        // такой ЦВЗ не мог быть встроен, а значит, такой результат = "ничего не было извлечено"

        //return new BitArray(new[] { watermarkInts[0] }); 
        // пока что просто возвращается первый элемент из списка вотермарок

        //return extractedWatermarkString;
        return resultWatermarkString;
    }
}
