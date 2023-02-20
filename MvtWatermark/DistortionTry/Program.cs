// See https://aka.ms/new-console-template for more information
using Distortion;
using DistortionTry;
using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

Console.WriteLine("Hello, World!");

var distortionLst = new List<IDistortion>() {
    new DeletingByArea(0.1),
    new RemoverByPerimeter(0.2),
    new DeletingLayers(0.3),
    new SeparationByGeometryType(SeparationByGeometryType.Mode.Lines),
    new ShiftingPoints(0.2),
    new ObjectsRemover(0.9),
    new ObjectsAdder(0.9),
};

/*var distortion1 = new DeletingByArea(0.1);
var distortion2 = new DeletingLayers(0.3);
var distortion3 = new SeparationByGeometryType(SeparationByGeometryType.Mode.Lines);
var distortion4 = new ShiftingPoints(0.2);*/

var parameterSets = new List<CoordinateSet>()
{
    new CoordinateSet(10, 658, 332),
    new CoordinateSet(10, 658, 333),
    new CoordinateSet(10, 658, 334),
    new CoordinateSet(10, 658, 338),
            //new ZxySet(10, 658, 335), // кривой тайл, не считывается
    new CoordinateSet(10, 658, 337),
};
VectorTileTree vectorTileTree = TileSetCreator.GetVectorTileTree(parameterSets);

var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
var options = new NoDistortionWatermarkOptions(2, 3, 2, 5, aEtype, false);
var key = 123;

var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false };
var message = new BitArray(boolArr);

foreach (var distortion in distortionLst)
{
    Console.WriteLine($"\nСообщение перед проверкой искажения: {DistortionTester.GetWatermarkString(message)}"); // отладка

    DistortionTester.TestDistortion(vectorTileTree, distortion, options, key, message);
}

//Console.Clear();