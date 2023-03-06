using Distortion;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark;
using System.Collections;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using DebugProj;

namespace DistortionTry;
public class DistortionTester
{
    public static void TestDistortion(VectorTileTree vectorTileTree, IDistortion distortion, NoDistortionWatermarkOptions options, int key, BitArray message)
    {
        var ndwm = new NoDistortionWatermark(options);
        //VectorTileTree treeWithWatermark = GetTreeWithWatermark(ndwm, vectorTileTree, key, message);
        VectorTileTree treeWithWatermark = ndwm.Embed(vectorTileTree, key, message);
        BitArray extractedMessageNoDistortion = ndwm.Extract(treeWithWatermark, key);


        Console.BackgroundColor = ConsoleColor.Cyan; // ОТЛАДКА
        Console.ForegroundColor = ConsoleColor.Black; // ОТЛАДКА
        Console.WriteLine("\nПЕРЕД ИСКАЖЕНИЕМ\n"); // ОТЛАДКА
        Console.BackgroundColor = ConsoleColor.Black; // ОТЛАДКА
        Console.ForegroundColor = ConsoleColor.White; // ОТЛАДКА


        VectorTileTree distortedTreeWithWatermark = distortion.Distort(treeWithWatermark);
        BitArray extractedMessageWithDistortion = ndwm.Extract(distortedTreeWithWatermark, key);

        ResultPrinter.Print(distortion, message, extractedMessageNoDistortion, extractedMessageWithDistortion);

        if (distortion is CoordinateOrderChanger)
        {
            vectorTileTree.Write($"{Directory.GetCurrentDirectory()}\\VectorTileTree");
            treeWithWatermark.Write($"{Directory.GetCurrentDirectory()}\\VectorTileTreeWithWatermark");
            distortedTreeWithWatermark.Write($"{Directory.GetCurrentDirectory()}\\distortedTileTrees");
        }
    }
}
