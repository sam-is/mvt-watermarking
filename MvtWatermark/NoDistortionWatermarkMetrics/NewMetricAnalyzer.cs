using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MvtWatermark.NoDistortionWatermark;
using MvtWatermark.NoDistortionWatermark.Auxiliary;

namespace NoDistortionWatermarkMetrics;
public class NewMetricAnalyzer
{
    public static void DisplayMetricForDBTileSet()
    {

    }

    public static void TestAlgorithm(IEnumerable<Additional.ZxySet> parameterSets)
    {
        var vtTree = TileSetCreator.CreateVectorTileTree(parameterSets);
        var key = 123;
        //var message = new BitArray() { true, false, true, false, true, true}
        var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false };
        var message = new BitArray(boolArr);

        Console.WriteLine($"Изначальное сообщение: {DebugClasses.ConsoleWriter.GetBitArrayStr(message)}\n");

        var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
        var options = new NoDistortionWatermarkOptions(2, 3, 2, 5, aEtype, false);


        var shortenedMessage = new BitArray(4 * options.Nb);
        message.CopyNbBitsTo(shortenedMessage, 0, 4 * options.Nb);

        var embededMessageString = ""; // отладка
        foreach (var elem in shortenedMessage) // отладка
        {
            embededMessageString += $"{elem} "; // отладка
        }
        Console.WriteLine($"Изначальное укороченное: {embededMessageString}"); // отладка


        var ndwm = new NoDistortionWatermark(options);
        var vtTreeWithWatermark = ndwm.Embed(vtTree, key, message);
        var extractedMessage = ndwm.Extract(vtTreeWithWatermark, key);

        Console.WriteLine($"Извлеченное сообщение: {DebugClasses.ConsoleWriter.GetBitArrayStr(extractedMessage)}");
        Console.WriteLine($"Встроенное и извлечённое сообщения равны? - {shortenedMessage.AreEqual(extractedMessage)}");
    }
}
