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
        if (message.Count < _options.Nb * tiles.Count())
        {
            throw new ArgumentException("Message size is less than [options.Nb * tiles.Count]. " +
                "The number of bits in the message is not enough to embed the watermark in the tile tree");
        }

        var firstHalfOfTheKey = (short)key;

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
        var extractedWatermarkString = new BitArray(_options.Nb * tiles.Count());
        var correctWatermarkTilesCounter = 0;
        var index = 0;
        foreach (var tileIndex in tiles) 
        {
            var extractedInt = readerWm.ExtractWm(tiles[tileIndex].GetMapboxTileFromVectorTile(), tileIndex, _options, shortenedKey);
            if (extractedInt != null)
            {
                var bitArr = new BitArray(new int[] { Convert.ToInt32(extractedInt) });
                bitArr.CopyNbBitsTo(extractedWatermarkString, index, _options.Nb);
                index += _options.Nb;
                correctWatermarkTilesCounter++;
            }
        }

        var resultWatermarkString = new BitArray(_options.Nb * correctWatermarkTilesCounter);
        extractedWatermarkString.CopyNbBitsTo(resultWatermarkString, 0, resultWatermarkString.Count);

        return resultWatermarkString;
    }
}
