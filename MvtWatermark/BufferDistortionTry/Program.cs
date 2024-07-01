/*
using System;
using MvtWatermark.BufferWatermark.Auxiliary;

//var sectormanager = new SectorManager();
List<Sector> sectors = SectorManager.DivideBySectors(8);
for (var i = 0; i < sectors.Count; i++)
{
    //Console.WriteLine($"\nSector #{i}: from {sectors[i].LowerBorder} (tg: {Math.Tan(sectors[i].LowerBorder * Math.PI/180)}) " +
    //    $"to {sectors[i].UpperBorder} (tg: {Math.Tan(sectors[i].UpperBorder * Math.PI/180)});");
    Console.WriteLine($"\nSector #{i}: from {sectors[i].LowerBorder / Math.PI}_PI (tg: {Math.Tan(sectors[i].LowerBorder)}) " +
        $"to {sectors[i].UpperBorder / Math.PI}_PI (tg: {Math.Tan(sectors[i].UpperBorder)});");
}

//Console.WriteLine(Math.Tan(Math.PI / 2));
*/

using DistortionTry;
using System.Collections;
using MvtWatermark.BufferWatermark;
using NetTopologySuite.IO.VectorTiles;
using OutOfBoundsObjectsTest;


var parameterSets = new List<CoordinateSet>()
{
    new CoordinateSet(7, 80, 40),
    new CoordinateSet(7, 80, 41),
    new CoordinateSet(7, 81, 40),
    new CoordinateSet(7, 81, 41),

    //new CoordinateSet(7, 81, 42),
    //new CoordinateSet(7, 82, 41),
    //new CoordinateSet(7, 82, 42), // тут нули сплошные...
            //new ZxySet(10, 658, 335), // кривой тайл, не считывается
    //new CoordinateSet(10, 658, 337),
};

var connectionString = "Data Source=C:/Users/user/source/repos/OutOfBoundsObjectsTest/OutOfBoundsObjectsTest/6_40_20_test_itowns.mbtiles;Version=3;";
//var connectionString = "Data Source=C:/Users/user/source/repos/OutOfBoundsObjectsTest/OutOfBoundsObjectsTest/6_40_20_test_v3_b10.mbtiles;Version=3;";
var options = new BufferWatermarkOptions(6, 160, 1); // стандартный extent = 4096, стандартный буфер = 5, 1 единица буфера = 1/256 экстента.
                                                     // Но у меня на тайлах, возможно, другой буфер, нужно проверить!!!
                                                     // Upd: буфер, который я использовал, скорее всего, равен 10
                                                     // Upd 2: при проверке длины тайла и выхода за границы на itowns выяснил, что буфер равен 5

//var boolArr = new bool[] { true, false, true, false, true };
var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, true, false, true,
      true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, false, true, false, true, true, true, false};
var message = new BitArray(boolArr);
var key = int.MaxValue - 1;

Console.WriteLine("task started");
Test(parameterSets, message, key, options, connectionString);
Console.WriteLine("task completed");

void Test(List<CoordinateSet> parameterSets, BitArray message, int key, BufferWatermarkOptions options, string connectionString)
{
    var tiles = new VectorTileTree();
    var loader = new VectorTilesLoaderSQLite(connectionString);
    foreach (var set in parameterSets)
    {
        VectorTile vt = loader.LoadTile(set);
        tiles[vt.TileId] = vt;
    }
    var bufferWatermark = new BufferWatermark(options);
    var tilesWithWatermark = bufferWatermark.Embed(tiles, key, message);
    var extractedWatermark = bufferWatermark.Extract(tilesWithWatermark, key);
    var embededMessageInfo = bufferWatermark.EmbededMessageInfo;
    var extractedMessageInfo = bufferWatermark.ExtractedMessageInfo;
    Console.WriteLine("message: ");
    ConsoleWriteLineBitArray(message, options.Nb);
    Console.WriteLine("extractedWatermark: ");
    ConsoleWriteLineBitArray(extractedWatermark, options.Nb);
    Console.WriteLine("embededMessageInfo: ");
    ConsoleWriteLineBitArray(embededMessageInfo, options.Nb);
    ConsoleWriteLineExtractedMessageInfo(extractedMessageInfo, options.Nb);
    var numOfBits = options.Nb * parameterSets.Count;
    var comparsionResult = CompareResults(extractedMessageInfo, message, extractedWatermark, numOfBits);
    Console.WriteLine("\nРезультат сравнения:\n");
    ConsoleWriteLineExtractedMessageInfo(comparsionResult, options.Nb);
}

void ConsoleWriteLineBitArray(BitArray arr, int nb)
{
    var index = 0;
    foreach (var bit in arr)
    {
        var divider = index % nb == 0 ? "| " : "";
        Console.Write($"{divider}{bit} ");
        index++;
    }
    Console.Write("\n");
}

void ConsoleWriteLineExtractedMessageInfo(int[] mesInfo, int nb)
{
    var index = 0;
    foreach (var elem in mesInfo)
    {
        var divider = index % nb == 0 ? "| " : "";
        Console.Write($"{divider}{elem} ");
        index++;
    }
    Console.Write("\n");
}

int[] CompareResults(int[] extractedMessageInfo, BitArray initialMessage, BitArray extractedMessage, 
    int numOfUsedBits)
{
    var upperBorder = numOfUsedBits <= initialMessage.Count ? numOfUsedBits : initialMessage.Count;
    var comparsionResult = new int[upperBorder];
    for (var i = 0; i < upperBorder; i++)
    {
        if (extractedMessageInfo[i] == 1)
        {
            comparsionResult[i] = initialMessage[i] == extractedMessage[i] ? 1 : -1;
        }
        else
        {
            comparsionResult[i] = 0;
        }
    }
    return comparsionResult;

    // -1 если бит был встроен, но не смогли извлечь;
    // 0, если не был встроен и не был извлечён;
    // 1, если был встроен и извлечён;
    // 2, если не был встроен, но был извлечён

}
