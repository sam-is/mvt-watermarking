using NetTopologySuite.IO.VectorTiles;
using Researches;
using Researches.Distortion;
using Researches.Parameters;
using Researches.RelationAccuracyFromData;
using Researches.Time;
using System.Collections;
using System.Text;


var pathDb = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine("Database", "stp0-12zoom.mbtiles"));
//var stpTileTree = Data.GetStpVectorTileTree(path, 653, 653, 333, 333, 10);
//var tegolaTileTree = Data.GetTegolaVectorTileTree(292, 292, 385, 385, 10);

//var path = Path.Combine(Directory.GetCurrentDirectory(), "TegolaTiles");

//var tegolaTileTree = Data.GetTegolaVectorTileTreeFromFiles(path, 292, 292, 385, 386, 10);

//var stpTileTree = Data.GetStpVectorTileTree(pathDb, 652, 652, 333, 334, 10);

//foreach (var tileId in stpTileTree)
//    tegolaTileTree[tileId] = stpTileTree[tileId];
////var pathLandPlot = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine("LandPlot.mbtiles"));
////var stptileTree = Data.GetStpVectorTileTree(pathLandPlot, 12);
////RelationAccuracyFromData(stptileTree);
//ParametersNew(tegolaTileTree);
//Distortion(tegolaTileTree);

//Time();

LandPlot();

static void RelationAccuracyFromData(VectorTileTree tileTree)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "relation accuracy from data");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    Reaserch.Start(tileTree, path);
}


static void ParametersNew(VectorTileTree tileTree)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "parameters test new");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    ReasearchParameters.StartMatrix(tileTree, path);
}

static void Parameters(VectorTileTree stpTileTree, VectorTileTree tegolaTileTree)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "parameters test");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    ReasearchParameters.Start(stpTileTree, Path.Combine(path, "parameter values test stp.txt"));
    ReasearchParameters.Start(tegolaTileTree, Path.Combine(path, "parameter values test tegola.txt"));
}

static void Distortion(VectorTileTree tileTree)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "distortion test new extent = 2048");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    ResearchDistortions.Start(tileTree, path);
}

static void DistortionWithKey(VectorTileTree stpTileTree, VectorTileTree tegolaTileTree)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "distortion with key test");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    ResearchDistortions.StartWithKey(stpTileTree, Path.Combine(path, "stp"));
    ResearchDistortions.StartWithKey(tegolaTileTree, Path.Combine(path, "tegola"));
}

static void Time()
{
    var pathDb = Path.Combine(Directory.GetCurrentDirectory(), "Database");

    var path = Path.Combine(Directory.GetCurrentDirectory(), "time test");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    ResearchTime.StartStp(pathDb, 0, 14, 4, 4, Path.Combine(path, "stp"));
    ResearchTime.StartTegola(0, 8, 6, 6, Path.Combine(path, "tegola"));
}

static void LandPlot()
{
    var pathDb = Path.Combine(Directory.GetCurrentDirectory(), "LandPlot.mbtiles");

    var path = Path.Combine(Directory.GetCurrentDirectory(), "landplot test");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    LandPlots.Start(pathDb, 17, 17, 1, 1, Path.Combine(path, "LandPlot"));
}