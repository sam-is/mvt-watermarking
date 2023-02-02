using System;
using System.Collections;
using System.Collections.Generic;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark.Auxiliary;

namespace MvtWatermark.NoDistortionWatermark;

public class NoDistortionWatermark: IMvtWatermark
{
    private readonly NoDistortionWatermarkOptions _options;
    
    public NoDistortionWatermark(NoDistortionWatermarkOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Встраивание ЦВЗ в формате BitArray в векторные тайлы в формате VectorTileTree (MVT) с использованием ключа
    /// </summary>
    /// <param name="tiles"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
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
        if (message.Length == 1 && message[0] == false) 
        {
            throw new Exception("Встраивание ЦВЗ '0' невозможно из-за особенностей алгоритма");
        }

        var firstHalfOfTheKey = (short)key;

        var tileDict = tiles.WriteWm(message, firstHalfOfTheKey, _options);

        var readerWm = new MapboxTileReaderWm();

        var toReturn = readerWm.Read(tileDict);

        return toReturn;
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
        var watermarkInts = new List<int>();
        foreach (var tileIndex in tiles) 
        {
            var extractedInt = readerWm.ExtractWm(tiles[tileIndex].GetMapboxTileFromVectorTile(), tileIndex, _options, shortenedKey);
            if (extractedInt != null)
                watermarkInts.Add(Convert.ToInt32(extractedInt));
        }

        if (watermarkInts.Count == 0)
            return new BitArray(new[] { false }); 
        // такой ЦВЗ не мог быть встроен, а значит, такой результат = "ничего не было извлечено"

        return new BitArray(new[] { watermarkInts[0] }); 
        // пока что просто возвращается первый элемент из списка вотермарок
    }
}
