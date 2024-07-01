using System;
using System.Collections;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.BufferWatermark.Auxiliary;
using System.Linq;

namespace MvtWatermark.BufferWatermark;

// -1 если бит был встроен, но не смогли извлечь;
// 0, если не был встроен и не был извлечён;
// 1, если был встроен и извлечён;
// 2, если не был встроен, но был извлечён

public class BufferWatermark: IMvtWatermark
{
    private readonly BufferWatermarkOptions _options;
    
    public BufferWatermark(BufferWatermarkOptions options)
    {
        _options = options;
        _embededMessageInfo = null;
    }

    private BitArray? _embededMessageInfo;
    private int[] _extractedMessageInfo;
    public BitArray? EmbededMessageInfo
    {
        get => _embededMessageInfo;
    }
    public int[] ExtractedMessageInfo
    {
        get => _extractedMessageInfo;
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

        // сюда нужно вставить проверку по буферу и обрезать, если он не соответствует реальности.
        // Этим, наверное, будет заниматься отдельный класс

        var intToCompare = (int)short.MaxValue + 1;
        var key1 = (short)key;
        short key2;
        if (key >= intToCompare) // сомнительно, но окэй?
            key2 = (short)(key >> 16);
        else
            key2 = (short)(intToCompare + (int)key1);

        var bwmMapboxTileWriterWm = new BwmMapboxTileWriterWm(_options);
        var tileDict = bwmMapboxTileWriterWm.WriteWm(tiles, message, key1, key2, out _embededMessageInfo);

        var readerWm = new BwmMapboxTileReaderWm(_options);

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
        //tiles здесь не сортируются, сделать сортировку здесь или же предполагать, что приходят уже отсортированные?
        var intToCompare = (int)short.MaxValue + 1;
        var key1 = (short)key;
        short key2;
        if (key >= intToCompare) // сомнительно, но окэй?
            key2 = (short)(key >> 16);
        else
            key2 = (short)(intToCompare + (int)key1);

        var readerWm = new BwmMapboxTileReaderWm(_options);
        var writerWm = new BwmMapboxTileWriterWm(_options);

        var tilesCount = tiles.Count();

        var extractedWatermarkString = new BitArray(_options.Nb * tilesCount);
        var embededMessageInfoCopy = new BitArray(_embededMessageInfo);
        _extractedMessageInfo = new int[_options.Nb * tilesCount];

        var index = 0;
        foreach (var tileIndex in tiles) 
        {
            var embededFragmentInfo = new BitArray(_options.Nb);
            for (var i = 0; i < _options.Nb; i++)
            {
                embededFragmentInfo[i] = embededMessageInfoCopy[i];
            }
            (BitArray extractedFragment, var extractedFragmentInfo) = 
                readerWm.ExtractWm(writerWm.GetMapboxTileFromVectorTile(tiles[tileIndex]), 
                tileIndex, _options, key1, key2, embededFragmentInfo);
            extractedFragment.CopyNbBitsTo(extractedWatermarkString, index, _options.Nb);
            for (var i = 0; i < _options.Nb; i++)
            {
                _extractedMessageInfo[i + index] = extractedFragmentInfo[i];
            }
            //extractedFragmentInfo.CopyNbBitsTo(extractedWatermarkString, index, _options.Nb);
            index += _options.Nb;
            embededMessageInfoCopy.RightShift(_options.Nb);
        }

        return extractedWatermarkString;
    }
}
