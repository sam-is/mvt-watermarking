using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using NetTopologySuite.IO.VectorTiles;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NoDistortionWatermarkMetrics.MetricAnalyzer;

namespace NoDistortionWatermarkMetrics;
public static class TileSetCreator
{
    /// <summary>
    /// Возвращает объект VectorTileTree, созданный на основе тайлов из БД, взятых согласно переданным параметрам
    /// </summary>
    /// <param name="parameterSets"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static VectorTileTree CreateVectorTileTree(IEnumerable<Additional.ZxySet> parameterSets)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var connectionString = $"Data Source = {path}";
        using var sqliteConnection = new SqliteConnection(connectionString);
        sqliteConnection.Open();

        Console.WriteLine($"Connection string = {connectionString}");

        var vtTree = new VectorTileTree();
        var areAnyCorrectTilesHere = false;

        foreach (var parameterSet in parameterSets)
        {
            var vt = GetSingleVectorTileFromDB(sqliteConnection, parameterSet.Zoom, parameterSet.X, parameterSet.Y);
            if (vt != null)
            {
                areAnyCorrectTilesHere = true;
                vtTree[vt.TileId] = vt;
            }
        }

        if (!areAnyCorrectTilesHere)
            throw new Exception("There are no correct vector tiles in this vector tile tree");

        return vtTree;
    }

    /// <summary>
    /// Проверка тайла на валидность геометрии (для библиотеки NetTopologySuite)
    /// 
    /// Ремарка: некоторые MVT-тайлы являются валидными для QGis, но невалидными для NetTopologySuite
    /// </summary>
    /// <param name="parameterSet"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static bool TestVectorTileIsCorrect(Additional.ZxySet parameterSet)
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var connectionString = $"Data Source = {dbPath}";
        using var sqliteConnection = new SqliteConnection(connectionString);
        sqliteConnection.Open();

        var vt = GetSingleVectorTileFromDB(sqliteConnection, parameterSet.Zoom, parameterSet.X, parameterSet.Y);
        if (vt == null)
            return false;

        var filePath = $"C:\\SerializedTiles\\SerializedWM_Metric\\{parameterSet.Zoom}\\{parameterSet.X}\\{parameterSet.Y}.mvt";

        using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate))
        {
            vt.Write(fileStream);
        }

        var reader = new MapboxTileReader();
        var tileDefinition = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(vt.TileId);

        using (var fs = new FileStream(filePath, FileMode.Open))
        {
            vt = reader.Read(fs, tileDefinition);
            foreach (var l in vt.Layers)
            {
                var features = l.Features;
                Console.WriteLine("\n");

                foreach (var f in features)
                {
                    if (!f.Geometry.IsValid)
                        throw new Exception("Невалидная геометрия!");
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Возвращает MVT-тайл из Sqlite-базы данных
    /// </summary>
    /// <param name="sqliteConnection"></param>
    /// <param name="zoom"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private static VectorTile? GetSingleVectorTileFromDB(SqliteConnection? sqliteConnection, int zoom, int x, int y)
    {
        using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
        command.Parameters.AddWithValue("$z", zoom);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", (1 << zoom) - y - 1);
        var obj = command.ExecuteScalar();

        if (obj == null)
        {
            Console.WriteLine("obj = null");
            return null;
        }
        else
        {
            Console.WriteLine("Successfully got the tile");
        }

        var bytes = (byte[])obj!;

        using var memoryStream = new MemoryStream(bytes);
        var reader = new MapboxTileReader();

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        var vt = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom));

        return vt;
    }
}
