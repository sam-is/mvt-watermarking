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
        public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
        {
            BitArray keyBitArray = new BitArray(new int[] { key });
            var TileDict = tiles.WriteWM(message, keyBitArray);

            var readerWM = new MapboxTileReaderWM();

            return readerWM.Read(TileDict);
        }

        public BitArray Extract(VectorTileTree tiles, int key)
        {
            throw new NotImplementedException();
        }
    }
}
