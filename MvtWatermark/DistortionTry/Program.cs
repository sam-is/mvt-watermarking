// See https://aka.ms/new-console-template for more information
using Distortion;
using DistortionTry;
using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.IO.VectorTiles;
using System.Collections;

var parameterSets = new List<CoordinateSet>()
{
    new CoordinateSet(10, 658, 332),
    new CoordinateSet(10, 658, 333),
    new CoordinateSet(10, 658, 334),
    new CoordinateSet(10, 658, 338),
            //new ZxySet(10, 658, 335), // кривой тайл, не считывается
    new CoordinateSet(10, 658, 337),
};

DistortionTester.TestDistortionsWithDifferentParameters(parameterSets);

/*
var distortionLst = new List<IDistortion>() {
    new DeletingLayersDistortion(0.3),
    new SeparationByGeometryTypeDistortion(SeparationByGeometryTypeDistortion.Mode.Lines),
    new DeleterByBounds(60, 50, 50, 52),

    // new DeleterByBounds(53, 52, 51.4, 51.5),
    // new DeleterByBounds(53, 52, 50, 52),

    new DeletingByAreaDistortion(0.1),
    new RemoverByPerimeter(0.05),
    new ObjectsAdder(0.9),
    new ObjectsRemover(0.1),
    new CoordinateOrderChanger(false),
    new ObjectsMagnifier(0.5),

    // new ShiftingPointsDistortion(0.2),
    // new ReducingNumberOfPointsDistortion(0.8, false),
    // new ReducingNumberOfPointsDistortion(0.8, true),
};

VectorTileTree vectorTileTree = TileSetCreator.GetVectorTileTree(parameterSets);
//VectorTileTree vectorTileTree = TileSetCreator.CreateRandomVectorTileTree(parameterSets);

//var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.MtLtLt;
var aEtype = NoDistortionWatermarkOptions.AtypicalEncodingTypes.NLtCommands;
var options = new NoDistortionWatermarkOptions(2, 3, 2, 5, aEtype, false);
var key = 123;

var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false };
var message = new BitArray(boolArr);

foreach (var distortion in distortionLst)
{
    DistortionTester.TestDistortion(vectorTileTree, distortion, options, key, message);
}
*/

//var ndwm = new NoDistortionWatermark(options);
//var twwm = ndwm.Embed(vectorTileTree, key, message);
//Console.WriteLine($"Что встроилось в дерево с тайлами: \t\t{ResultPrinter.GetWatermarkString(ndwm.Extract(twwm, key))}");

/*VectorTile vt = new();
Layer lyr = new();
NetTopologySuite.Features.IFeature ftr = new NetTopologySuite.Features.Feature();
var tile1 = new MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile();
var tile = NetTopologySuite.IO.VectorTiles.Mapbox.Tile();*/

//var tile = new MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile(10, 658, 333);

//Console.WriteLine($"Zero elem of vectorTileTree: {vectorTileTree.ElementAt(0)}\n");

/*
var tile = new MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile(vectorTileTree.ElementAt(0));
var biggestTop = tile.Top;
var smallestBottom = tile.Bottom;
var smallestLeft = tile.Left;
var biggestRight = tile.Right;
foreach (var tileId in vectorTileTree)
{
    //Console.WriteLine($"Current tileId: {tileId}");
    tile = new MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile(tileId);
    var boundsString = $"\nBOUNDS => top: {tile.Top}, bottom: {tile.Bottom}, left: {tile.Left}, right: {tile.Right}";
    Console.WriteLine(boundsString);

    if (biggestTop < tile.Top)
        biggestTop = tile.Top;
    if (smallestBottom > tile.Bottom)
        smallestBottom = tile.Bottom;
    if (smallestLeft > tile.Left)
        smallestLeft = tile.Left;
    if (biggestRight < tile.Right)
        biggestRight = tile.Right;
}

var resultedBoundsString = $"\n\nRESULTED BOUNDS => top: {biggestTop}, bottom: {smallestBottom}, left: {smallestLeft}, right: {biggestRight}";
Console.WriteLine(resultedBoundsString);

foreach (var tileId in vectorTileTree)
{
    foreach (var lyr in vectorTileTree[tileId].Layers)
    {
        foreach (var ftr in lyr.Features)
        {
            //ftr.Geometry.
            //var z = new Geometry();
            var coordinateString = "";
            foreach (var coord in ftr.Geometry.Coordinates)
            {
                coordinateString += $"{coord} ";
            }
            //Console.WriteLine($"\n\nfeature type: {ftr.Geometry.GetType().Name}; feature geometry: {coordinateString}");
            Console.WriteLine($"\n\nfeature type: {ftr.Geometry.GetType().Name}; feature geometry: {ftr.Geometry}");

            var reversedCoords = ftr.Geometry.Coordinates.Reverse();
            //ftr.Geometry.Coordinates = reversedCoords;

            var newGeom = ftr.Geometry.Copy();
            if (ftr.Geometry is LineString) {
                newGeom = ftr.Geometry.Reverse();
            }
            else if (ftr.Geometry is MultiLineString)
            {
                newGeom = ftr.Geometry.Reverse();
            }

            //var reversedFtr = new Feature { Geometry = ftr.Geometry.GetType().GetConstructors()[0].Invoke() };
            var reversedFtr = new Feature(newGeom, ftr.Attributes);

            coordinateString = "";
            foreach (var coord in reversedCoords)
            {
                coordinateString += $"{coord} ";
            }
            //Console.WriteLine($"\nREVERSED feature type: {ftr.Geometry.GetType().Name}; feature geometry: {coordinateString}");
            Console.WriteLine($"\n\nfeature type: {ftr.Geometry.GetType().Name}; feature geometry: {ftr.Geometry}");
        }
    }
}
//var tile = new MvtWatermark.NoDistortionWatermark.Auxiliary.NtsArtefacts.Tile(0, 0, 0);

*/

//Console.Clear();