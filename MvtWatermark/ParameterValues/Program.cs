using Microsoft.Data.Sqlite;
using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using ParameterValues;
using System.IO.Compression;
using static ParameterValues.CheckParameters;

var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
const int z = 10;

using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
sqliteConnection.Open();

var reader = new MapboxTileReader();
var tileTree = new VectorTileTree();

for (var x = 653; x < 655; x++)
    for (var y = 333; y < 335; y++)
    {
        using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
        command.Parameters.AddWithValue("$z", z);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", (1 << z) - y - 1);
        var obj = command.ExecuteScalar();

        if (obj == null)
            continue;

        var bytes = (byte[])obj!;

        using var memoryStream = new MemoryStream(bytes);

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

        tileTree[tile.TileId] = tile;
    }

var options = new QimMvtWatermarkOptions(0.6, 0.3, 20, 4096, 2, 5, 20, null, false);
var checkParameters = new CheckParameters { Options = options };

Run(tileTree, checkParameters, "Parameter values test stp general extraction.txt");

options = new QimMvtWatermarkOptions(0.6, 0.3, 20, 4096, 2, 5, 20, null, true);
checkParameters = new CheckParameters { Options = options };

Run(tileTree, checkParameters, "Parameter values test stp general extraction.txt");


var tileTreeTegola = new VectorTileTree();
for (var x = 242; x < 246; x++)
    for (var y = 390; y < 394; y++)
    {
        using var sharedClient = new HttpClient()
        {
            BaseAddress = new Uri($"https://tegola-osm-demo.go-spatial.org/v1/maps/osm/{z}/{x}/{y}"),
        };

        sharedClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 QGIS/32210");
        sharedClient.DefaultRequestHeaders.Add("accept-encoding", "gzip");

        var response = sharedClient.GetByteArrayAsync("").Result;
        using var memoryStream = new MemoryStream(response);

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        var tile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

        tileTreeTegola[tile.TileId] = tile;
    }

var optionsTegola = new QimMvtWatermarkOptions(0.6, 0.3, 5, 4096, 2, 5, 10, null, false);
var checkParametersTegola = new CheckParameters { Options = optionsTegola };

Run(tileTreeTegola, checkParametersTegola, "Parameter values test tegola.txt");

optionsTegola = new QimMvtWatermarkOptions(0.6, 0.3, 5, 4096, 2, 5, 10, null, true);
checkParametersTegola = new CheckParameters { Options = optionsTegola };

Run(tileTreeTegola, checkParametersTegola, "Parameter values test tegola general extraction.txt");

void Run(VectorTileTree tileTree, CheckParameters checkParameters, string path)
{
    var valuesT2AndK = new double[] { 0.01, 0.05, 0.08, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 0.98, 1 };
    var valuesT1 = new double[] { 1, 2, 3, 5, 8, 10, 15, 20, 30, 40, 50, 75, 100, 200, 300, 500, 1000 };
    var valuesDistance = new double[] { 1, 2, 3};
    var valuesR = new double[] { 1, 2, 3, 5, 8, 10, 15, 20, 40, 60, 80, 100, 150, 200 };
    var valuesExtent = new double[] { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };

    using var streamWriter = new StreamWriter(path);

    var t2 = checkParameters.Compute(tileTree, ParamName.T2, valuesT2AndK);
    WriteToFile(streamWriter, valuesT2AndK, t2, nameof(ParamName.T2));

    var k = checkParameters.Compute(tileTree, ParamName.K, valuesT2AndK);
    WriteToFile(streamWriter, valuesT2AndK, k, nameof(ParamName.K));

    var t1 = checkParameters.Compute(tileTree, ParamName.T1, valuesT1);
    WriteToFile(streamWriter, valuesT1, t1, nameof(ParamName.T1));

    var distance = checkParameters.Compute(tileTree, ParamName.DISTANCE, valuesDistance);
    WriteToFile(streamWriter, valuesDistance, distance, nameof(ParamName.DISTANCE));

    var r = checkParameters.Compute(tileTree, ParamName.R, valuesR);
    WriteToFile(streamWriter, valuesR, r, nameof(ParamName.R));

    var extent = checkParameters.Compute(tileTree, ParamName.EXTENT, valuesExtent);
    WriteToFile(streamWriter, valuesExtent, extent, nameof(ParamName.EXTENT));
}

void WriteToFile(StreamWriter streamWriter, double[] values,Measures measure, string name)
{
    streamWriter.Write($"{name}\n");
    streamWriter.Write(string.Format("{0, -4}\t{1, -8}\t{2, -12}\t{3, -12}\n", "value", "accuracy", "avg Hausdorff", "avg Frechet"));

    for (var i = 0; i < values.Length; i++)
        streamWriter.Write(string.Format("{0, -4}\t{1, -8}\t{2, -12:f7}\t{3, -12:f7}\n", values[i], measure.Accuracy![i], measure.AvgHausdorff![i], measure.AvgFrechet![i]));

    streamWriter.Write("\n\n\n");
}