using Distortion;
using NetTopologySuite.IO.VectorTiles;
using MvtWatermark.NoDistortionWatermark;
using System.Collections;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using DebugProj;

namespace DistortionTry;
public static class DistortionTester
{
    public static async void TestDistortionsWithDifferentParameters(List<CoordinateSet> parameterSets)
    {
        VectorTileTree vectorTileTree = TileSetCreator.GetVectorTileTree(parameterSets);
        //VectorTileTree vectorTileTree = TileSetCreator.CreateRandomVectorTileTree(parameterSets);

        //var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
        var options = new NoDistortionWatermarkOptions(2, 3, 2, 5, aEtype, false);
        var key = 123;

        var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false };
        var message = new BitArray(boolArr);

        double param = 0;
        while (param <= 1)
        {
            var distortionLst = new List<IDistortion>() {
                new DeletingLayersDistortion(param),
                //new SeparationByGeometryTypeDistortion(SeparationByGeometryTypeDistortion.Mode.Lines),
                //new DeleterByBounds(60, 50, 50, 52),

                // new DeleterByBounds(53, 52, 51.4, 51.5),
                // new DeleterByBounds(53, 52, 50, 52),

                new DeletingByAreaDistortion(param),
                new RemoverByPerimeter(param),
                new ObjectsAdder(param),
                new ObjectsRemover(param),
                //new CoordinateOrderChanger(false),
                new ObjectsMagnifier(param),

                //new ShiftingPointsDistortion(param),
                new ReducingNumberOfPointsDistortion(param, false),
                new ReducingNumberOfPointsDistortion(param, true),
            };

            foreach (var distortion in distortionLst)
            {
                await TestDistortion(vectorTileTree, distortion, options, key, message, param);
            }

            param += 0.05;
        }

        
    }
    public static async Task TestDistortion(VectorTileTree vectorTileTree, IDistortion distortion, NoDistortionWatermarkOptions options, 
        int key, BitArray message, double param)
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


        Console.WriteLine("\nПеред дистортом\n"); // ОТЛАДКА
        Console.WriteLine($"\n\n[TestDistortion] Искажение: {distortion.GetType()}");
        VectorTileTree distortedTreeWithWatermark = distortion.Distort(treeWithWatermark);
        Console.WriteLine("\nПосле дисторта\n"); // ОТЛАДКА
        BitArray extractedMessageWithDistortion = ndwm.Extract(distortedTreeWithWatermark, key);
        Console.WriteLine("\nПосле извлечения\n"); // ОТЛАДКА

        ResultPrinter.Print(distortion, message, extractedMessageNoDistortion, extractedMessageWithDistortion);
        Console.WriteLine("\nПеред логом\n"); // ОТЛАДКА
        await ResultPrinter.Log(distortion, message, extractedMessageNoDistortion, extractedMessageWithDistortion, "..\\..\\..\\distortionTests", param);
        Console.WriteLine("\nПосле лога\n"); // ОТЛАДКА

        if (distortion is CoordinateOrderChanger)
        {
            vectorTileTree.Write($"{Directory.GetCurrentDirectory()}\\VectorTileTree");
            treeWithWatermark.Write($"{Directory.GetCurrentDirectory()}\\VectorTileTreeWithWatermark");
            distortedTreeWithWatermark.Write($"{Directory.GetCurrentDirectory()}\\distortedTileTrees");
        }
    }
}
