// See https://aka.ms/new-console-template for more information
using Distortion;
using DistortionTry;
using MvtWatermark.NoDistortionWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
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
    //new DeleterByBounds(60, 50, 50, 52)
    //new DeleterByBounds(53, 52, 51.4, 51.5)
    new DeleterByBounds(53, 52, 50, 52),
    new CoordinateOrderChanger()
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