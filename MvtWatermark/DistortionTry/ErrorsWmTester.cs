using MvtWatermark.NoDistortionWatermark;
using System.Collections;
using Distortion;
using NetTopologySuite.IO.VectorTiles;

namespace DistortionTry;
public static class ErrorsWmTester
{
    public static void Test()
    {
        var paramSetsStp = new List<CoordinateSet>()
        {
            new CoordinateSet(10, 653, 333),
            new CoordinateSet(10, 653, 334),
            new CoordinateSet(10, 658, 332),
            new CoordinateSet(10, 658, 333),
            new CoordinateSet(10, 658, 334),
            new CoordinateSet(10, 658, 338)
        };
        var paramSetsTegola = new List<CoordinateSet>()
        {
            //new CoordinateSet(10, 292, 385),
            //new CoordinateSet(10, 293, 385)
        };
        //VectorTileTree tiles = TileSetCreator.CreateRandomVectorTileTree(paramSetsStp);
        //VectorTileTree tiles = TileSetCreator.CreateRandomVectorTileTreeOneLineString(paramSetsStp, 180);
        VectorTileTree tiles = TileSetCreator.GetVectorTileTree(paramSetsStp, paramSetsTegola);
        var key = 123;
        var m = 6;
        //var encodingType = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var encodingType = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        var options = new NoDistortionWatermarkOptions(m, 3, 3, 15, encodingType);
        var ndwm = new NoDistortionWatermark(options);

        var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false,
        true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false, true, true, true, false};
        var message = new BitArray(boolArr);

        var tilesWithWatermark = ndwm.Embed(tiles, key, message);
        var extractedWatermarkBeforeDistortion = ndwm.Extract(tilesWithWatermark, key);

        Console.BackgroundColor = ConsoleColor.Blue;
        Console.WriteLine("\n\n-------------D I S T O R T I O N-------------\n");
        Console.BackgroundColor = ConsoleColor.Black;

        var reverseDistortion = new CoordinateOrderReverser(1);
        VectorTileTree distortedTiles = reverseDistortion.Distort(tilesWithWatermark);
        var extractedWatermarkAfterDistortion = ndwm.Extract(distortedTiles, key);

        Console.WriteLine($"\n\nextractedWatermarkBeforeDistortion: {ResultPrinter.GetWatermarkString(extractedWatermarkBeforeDistortion)}");
        Console.WriteLine($"extractedWatermarkAfterDistortion: {ResultPrinter.GetWatermarkString(extractedWatermarkAfterDistortion)}");
    }
}
