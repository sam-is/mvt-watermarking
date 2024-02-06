using NetTopologySuite.Geometries;
using System;

namespace MvtWatermark.QimMvtWatermark;

public static class CoordinateConverter
{
    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    /// <param name="deg">Digrees</param>
    /// <returns>Radians</returns>
    public static double DegToRad(double deg) => deg * Math.PI / 180;
    /// <summary>
    /// Converts longitude with known zoom to x.
    /// </summary>
    /// <param name="lon">Longitude</param>
    /// <param name="z">Zoom</param>
    /// <returns>X</returns>
    public static int LongToTileX(double lon, int z) => (int)Math.Floor((lon + 180.0) / 360.0 * (1 << z));

    /// <summary>
    /// Converts latitude with known zoom to y.
    /// </summary>
    /// <param name="lat">Latitude</param>
    /// <param name="z">Zoom</param>
    /// <returns>Y</returns>
    public static int LatToTileY(double lat, int z) => (int)Math.Floor((1 - Math.Log(Math.Tan(DegToRad(lat)) + 1 / Math.Cos(DegToRad(lat))) / Math.PI) / 2 * (1 << z));

    /// <summary>
    /// Converts tile x with known zoom to longitude.
    /// </summary>
    /// <param name="x">X</param>
    /// <param name="z">Zoom</param>
    /// <returns>Longitude</returns>
    public static double TileXToLong(int x, int z) => x / (double)(1 << z) * 360.0 - 180;

    /// <summary>
    /// Converts tile y with known zoom to latitude.
    /// </summary>
    /// <param name="y">Y</param>
    /// <param name="z">Zoom</param>
    /// <returns>Latitude</returns>
    public static double TileYToLat(int y, int z)
    {
        var n = Math.PI - 2.0 * Math.PI * y / (1 << z);
        return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
    }

    /// <summary>
    /// Computes bounds of tile in degrees.
    /// </summary>
    /// <param name="x">X of tile</param>
    /// <param name="y">Y of tile</param>
    /// <param name="z">Zoom of tile</param>
    /// <returns>Bounds of tile in degrees</returns>
    public static Envelope TileBounds(int x, int y, int z)
    {
        var w = TileXToLong(x, z);
        var e = TileXToLong(x + 1, z);
        var n = TileYToLat(y, z);
        var s = TileYToLat(y + 1, z);
        return new Envelope(w, e, n, s);
    }

    /// <summary>
    /// Converts degrees coordinate to meters.
    /// </summary>
    /// <param name="coordinate">Coordinate in degrees</param>
    /// <returns>Coordinate in meters</returns>
    public static Coordinate DegreesToMeters(Coordinate coordinate)
    {
        var x = coordinate.X * 20037508.34 / 180;
        var y = Math.Log(Math.Tan((90 + coordinate.Y) * Math.PI / 360)) / (Math.PI / 180);
        y = y * 20037508.34 / 180;
        return new Coordinate(x, y);
    }

    /// <summary>
    /// Converts degrees envelope to meters.
    /// </summary>
    /// <param name="envelope">Envelope in degrees</param>
    /// <returns>Envelope in meters</returns>
    public static Envelope DegreesToMeters(Envelope envelope)
    {
        var max = new Coordinate(envelope.MaxX, envelope.MaxY);
        var min = new Coordinate(envelope.MinX, envelope.MinY);
        return new Envelope(DegreesToMeters(max), DegreesToMeters(min));
    }

    /// <summary>
    /// Converts geometry coordinates from degrees to meters.
    /// </summary>
    /// <param name="geometry">Geometry with degrees coordinates</param>
    /// <returns>Geometry with meters coordinates</returns>
    public static Geometry DegreesToMeters(Geometry geometry)
    {
        var copy = geometry.Copy();
        for (var i = 0; i < copy.Coordinates.Length; i++)
        {
            var coordinateMeters = DegreesToMeters(copy.Coordinates[i]);
            copy.Coordinates[i].X = coordinateMeters.X;
            copy.Coordinates[i].Y = coordinateMeters.Y;
        }
        return copy;
    }

    /// <summary>
    /// Converts meters coordinate to degrees.
    /// </summary>
    /// <param name="coordinate">Coordinate in meters</param>
    /// <returns>Coordinate in degrees</returns>
    public static Coordinate MetersToDegrees(Coordinate coordinate)
    {
        var x = coordinate.X * 180 / 20037508.34;
        var y = coordinate.Y / (20037508.34 / 180);
        y = Math.Atan(Math.Exp(Math.PI / 180 * y));
        y /= Math.PI / 360;
        y -= 90;
        return new Coordinate(x, y);
    }

    /// <summary>
    /// Point with integer coordinate
    /// </summary>
    /// <param name="X">X coordinate</param>
    /// <param name="Y">Y coordinate</param>
    public record IntPoint(int X, int Y);

    /// <summary>
    /// Converts meters coordinate to integer in tile.
    /// </summary>
    /// <param name="coordinate">Coordinate in meters</param>
    /// <param name="tileEnvelope">Tile envelope</param>
    /// <param name="extentDistance">Meters that equal distance between two nearest integer coordinate</param>
    /// <returns>Integer coordinate</returns>
    public static IntPoint MetersToInteger(Coordinate coordinate, Envelope tileEnvelope, double extentDistance)
    {
        var x = Convert.ToInt32((coordinate.X - tileEnvelope.MinX) / extentDistance);
        var y = Convert.ToInt32((coordinate.Y - tileEnvelope.MinY) / extentDistance);

        return new IntPoint(x, y);
    }

    /// <summary>
    /// Converts integer coordinate to meters.
    /// </summary>
    /// <param name="coordinate">Integer coordinate</param>
    /// <param name="tileEnvelope">Tile envelope</param>
    /// <param name="extentDistance">Meters that equal distance between two nearest integer coordinate</param>
    /// <returns></returns>
    public static Coordinate IntegerToMeters(IntPoint coordinate, Envelope tileEnvelope, double extentDistance)
    {
        var x = tileEnvelope.MinX + coordinate.X * extentDistance;
        var y = tileEnvelope.MinY + coordinate.Y * extentDistance;

        return new Coordinate(x, y);
    }
}