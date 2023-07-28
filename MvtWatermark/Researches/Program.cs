using NetTopologySuite.IO.VectorTiles;
using Researches;
using Researches.Distortion;
using Researches.Parameters;
using Researches.Time;

var path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine("Database", "stp0-12zoom.mbtiles"));
var stpTileTree = Data.GetStpVectorTileTree(path, 653, 653, 333, 333, 10);
var tegolaTileTree = Data.GetTegolaVectorTileTree(292, 292, 385, 385, 10);

Time();

static void Parameters(VectorTileTree stpTileTree, VectorTileTree tegolaTileTree)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "parameters test");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    ReasearchParameters.Start(stpTileTree, Path.Combine(path, "parameter values test stp.txt"));
    ReasearchParameters.Start(tegolaTileTree, Path.Combine(path, "parameter values test tegola.txt"));
}

static void Distortion(VectorTileTree stpTileTree, VectorTileTree tegolaTileTree)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "distortion test");
    if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

    ResearchDistortions.Start(stpTileTree, Path.Combine(path, "stp"));
    ResearchDistortions.Start(tegolaTileTree, Path.Combine(path, "tegola"));
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

    ResearchTime.StartStp(pathDb, 0, 14, 2, 2, Path.Combine(path, "stp"));
    ResearchTime.StartTegola(0, 8, 6, 6, Path.Combine(path, "tegola"));
}