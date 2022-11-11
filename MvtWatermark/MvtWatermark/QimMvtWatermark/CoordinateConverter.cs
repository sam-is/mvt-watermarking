using NetTopologySuite.Geometries;
using System;

namespace MvtWatermark.QimMvtWatermark;

public static class CoordinateConverter
{
    private static double DegToRad(double deg)
    {
        return deg * Math.PI / 180;
    }

    public static int LongToTileX(double lon, int z)
    {
        return (int)Math.Floor((lon + 180.0) / 360.0 * (1 << z));
    }

    public static int LatToTileY(double lat, int z)
    {
        return (int)Math.Floor((1 - Math.Log(Math.Tan(DegToRad(lat)) + 1 / Math.Cos(DegToRad(lat))) / Math.PI) / 2 * (1 << z));
    }

    public static double TileXToLong(int x, int z)
    {
        return x / (double)(1 << z) * 360.0 - 180;
    }

    public static double TileYToLat(int y, int z)
    {
        var n = Math.PI - 2.0 * Math.PI * y / (1 << z);
        return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
    }

    public static Envelope TileBounds(int x, int y, int z)
    {
        var w = TileXToLong(x, z);
        var e = TileXToLong(x + 1, z);
        var n = TileYToLat(y, z);
        var s = TileYToLat(y + 1, z);
        return new Envelope(new Coordinate(w, n), new Coordinate(e, s));
    }

    public static Coordinate DegreesToMeters(Coordinate coordinate)
    {
        var x = coordinate.X * 20037508.34 / 180;
        var y = Math.Log(Math.Tan((90 + coordinate.Y) * Math.PI / 360)) / (Math.PI / 180);
        y = y * 20037508.34 / 180;
        return new Coordinate(x, y);
    }

    public static Envelope DegreesToMeters(Envelope envelope)
    {
        var max = new Coordinate(envelope.MaxX, envelope.MaxY);
        var min = new Coordinate(envelope.MinX, envelope.MinY);
        return new Envelope(DegreesToMeters(max), DegreesToMeters(min));
    }

    public static Geometry DegreesToMeters(Geometry geometry)
    {
        for (var i = 0; i < geometry.Coordinates.Length; i++)
        {
            var coordinateMeters = DegreesToMeters(geometry.Coordinates[i]);
            geometry.Coordinates[i].X = coordinateMeters.X;
            geometry.Coordinates[i].Y = coordinateMeters.Y;
        }
        return geometry;
    }

    public static Coordinate MetersToDegrees(Coordinate coordinate)
    {
        var x = coordinate.X * 180 / 20037508.34;
        var y = coordinate.Y / (20037508.34 / 180);
        y = Math.Atan(Math.Exp(Math.PI / 180 * y));
        y /= Math.PI / 360;
        y -= 90;
        return new Coordinate(x, y);
    }
}