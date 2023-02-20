using Distortion;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark;
using System.Collections;
using MvtWatermark.NoDistortionWatermark.Auxiliary;

namespace DistortionTry;
public class DistortionTester
{
    public static void TestDistortion(VectorTileTree vectorTileTree, IDistortion distortion, NoDistortionWatermarkOptions options, int key, BitArray message)
    {
        var ndwm = new NoDistortionWatermark(options);
        VectorTileTree treeWithWatermark = GetTreeWithWatermark(ndwm, vectorTileTree, key, message);
        BitArray extractedMessageNoDistortion = ExtractWatermark(ndwm, treeWithWatermark, key);

        VectorTileTree distortedTree = distortion.Distort(treeWithWatermark);
        BitArray extractedMessageWithDistortion = ExtractWatermark(ndwm, distortedTree, key);

        Console.WriteLine($"Watermark from original tree: {GetWatermarkString(extractedMessageNoDistortion)}");
        Console.WriteLine($"Watermark from distorted tree: {GetWatermarkString(extractedMessageWithDistortion)}");

        Console.WriteLine($"Both extracted messages (with and without distortion) are equal? - " +
            $"{extractedMessageNoDistortion.AreEqual(extractedMessageWithDistortion)}");
    }

    private static BitArray ExtractWatermark(NoDistortionWatermark ndwm, VectorTileTree treeWithWatermark, int key) 
        => ndwm.Extract(treeWithWatermark, key);

    private static VectorTileTree GetTreeWithWatermark(NoDistortionWatermark ndwm, VectorTileTree vectorTileTree, int key, BitArray message) 
        => ndwm.Embed(vectorTileTree, key, message);

    public static string GetWatermarkString(BitArray message)
    {
        var messageStr = "";
        foreach (var bit in message)
        {
            messageStr += $"{bit} ";
        }
        return messageStr;
    }
}
