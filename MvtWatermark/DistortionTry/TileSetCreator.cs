using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistortionTry;
public class TileSetCreator
{
    public static VectorTileTree GetVectorTileTree(IEnumerable<CoordinateSet> parameterSets)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "stp10zoom.mbtiles");
        var connectionString = $"Data Source = {path}";
        using var sqliteConnection = new SqliteConnection(connectionString);
        sqliteConnection.Open();

        Console.WriteLine($"Connection string = {connectionString}");

        var vectorTileTree = new VectorTileTree();
        var areAnyCorrectTilesHere = false;

        foreach (var parameterSet in parameterSets)
        {
            var vt = GetSingleVectorTileFromDB(sqliteConnection, parameterSet.Zoom, parameterSet.X, parameterSet.Y);
            if (vt != null)
            {
                areAnyCorrectTilesHere = true;
                vectorTileTree[vt.TileId] = vt;
            }
        }

        if (!areAnyCorrectTilesHere)
            throw new ArgumentException("No correct tiles have been found");

        return vectorTileTree;
    }

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
