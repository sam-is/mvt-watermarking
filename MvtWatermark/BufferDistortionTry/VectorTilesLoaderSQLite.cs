using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.IO.VectorTiles;
using System.Data.SQLite;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System.IO.Compression;
using NetTopologySuite.Features;
using ProtoBuf;
using System.IO;
using DistortionTry;
using Microsoft.Data.Sqlite;

namespace OutOfBoundsObjectsTest;

public class VectorTilesLoaderSQLite
{
    private SQLiteConnection _connection;

    /// <summary>
    /// Creates a new instance of VectorTilesLoaderSQLite class with connection based on given connection string
    /// </summary>
    /// <param name="connectionString"></param>
    /// <exception cref="Exception"></exception>
    public VectorTilesLoaderSQLite(string connectionString)
    {
        try
        {
            _connection = new SQLiteConnection(connectionString);
        }
        catch
        {
            throw new ArgumentException("invalid connection string");
        }
    }

    /// <summary>
    /// Creates a VectorTile object based on certain tile from the connected mbtiles database
    /// </summary>
    /// <param name="zoom"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="tms"></param>
    /// <returns>VectorTile object if the tile with given coordinates exists in the database. Else null</returns>
    public VectorTile? LoadTile(int zoom, int x, int y, bool tms = false)
    {
        _connection.Open();
        using var command = new SQLiteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", _connection);
        command.Parameters.AddWithValue("$z", zoom);
        command.Parameters.AddWithValue("$x", x);
        if (!tms)
            command.Parameters.AddWithValue("$y", (1 << zoom) - y - 1);
        else
            command.Parameters.AddWithValue("$y", y);
        //var dbY = (1 << zoom) - y - 1;
        //y = (1 << zoom) - dbY - 1;

        var obj = command.ExecuteScalar();

        if (obj is null)
            return null;

        var bytes = (byte[])obj!;

        using var memoryStream = new MemoryStream(bytes);
        var reader = new MapboxTileReader();

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var decompressor = new GZipStream(memoryStream, CompressionMode.Decompress, false);
        // ОТЛАДКА
        //Tile tile = Serializer.Deserialize<Tile>(decompressor);
        //Console.WriteLine($"Layer extent: {tile.Layers[0].Extent}");
        // ОТЛАДКА
        var vectorTile = reader.Read(decompressor, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, zoom));

        _connection.Close();

        return vectorTile;
    }

    /// <summary>
    /// Takes out coordinates from CoordinateSet and Calls upper LoadTile method with these coordinates
    /// </summary>
    /// <param name="set"></param>
    /// <param name="tms"></param>
    /// <returns>Result of calling upper LoadTile() method</returns>
    public VectorTile? LoadTile(CoordinateSet set, bool tms = false)
    {
        return LoadTile(set.Zoom, set.X, set.Y, tms);
    }

    /// <summary>
    /// Loads VectorTile from connected mbtiles DB and writes it to .mvt file
    /// </summary>
    /// <param name="set"></param>
    /// <param name="directoryPath"></param>
    /// <param name="fileName"></param>
    /// <param name="tms"></param>
    /// <exception cref="ArgumentException"></exception>
    public void WriteTileToFileFromDb(CoordinateSet set, string directoryPath, string? fileName = null, bool tms = false)
    {
        var vt = LoadTile(set, tms);

        if (!Directory.Exists(directoryPath))
            throw new ArgumentException($"No such directory: {directoryPath}");

        if (fileName is null)
        {
            if (!tms)
                fileName = $"{set.Zoom}_{set.X}_{set.Y}.mvt";
            else
                fileName = $"{set.Zoom}_{set.X}_{(1 << set.Zoom) - set.Y - 1}.mvt";
        }

        var filePath = $"{directoryPath}\\{fileName}";

        //var vtToWrite = new VectorTile { TileId = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(set.X, set.Y, set.Zoom).Id };
        //foreach (var lyr in vt.Layers)
        //{
        //    var newLyr = new Layer { Name = lyr.Name };
        //    foreach (var ftr in lyr.Features)
        //    {
        //        var newFtr = new Feature(ftr.Geometry, ftr.Attributes) { BoundingBox = ftr.BoundingBox };
        //        newLyr.Features.Add(newFtr);
        //    }
        //    vtToWrite.Layers.Add(newLyr);
        //}

        using (var fs = new FileStream(filePath, FileMode.Create))
        {
            vt.Write(fs);
            //vtToWrite.Write(fs);
        }
    }

    /// <summary>
    /// calls upper WriteTileToFileFromDb for the group of CoordinateSet instances
    /// </summary>
    /// <param name="sets"></param>
    /// <param name="directoryPath"></param>
    /// <param name="tms"></param>
    public void WriteTileToFileFromDb(IEnumerable<CoordinateSet> sets, string directoryPath, bool tms = false)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        foreach (var set in sets)
        {
            WriteTileToFileFromDb(set, directoryPath, tms: tms);
        }
    }
}