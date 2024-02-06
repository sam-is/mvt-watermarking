using MvtWatermark.QimMvtWatermark.Requantization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using System;

namespace MvtWatermark.QimMvtWatermark;

/// <summary>
/// Changes coordinates of vector tile. For new tile create a new instance.
/// </summary>
/// <param name="countToChange">Count of point with <c>value</c> that must be changed to opposite value</param>
/// <param name="value">Value which count must be increase</param>
/// <param name="count">Count of all points with <c>value</c></param>
/// <param name="requantizationMatrix">Re-quantization matrix</param>
public class CoordinatesChanger(int countToChange, bool value, int count, RequantizationMatrix requantizationMatrix)
{
    /// <summary>
    /// Count of point with <see cref="Value"/> that must be changed to opposite value.
    /// </summary>
    public int CountToChange { get; } = countToChange;

    /// <summary>
    /// Value which count must be increase.
    /// </summary>
    public bool Value { get; } = value;

    /// <summary>
    /// Count of all points with <see cref="Value"/>.
    /// </summary>
    public int Count { get; } = count;

    /// <summary>
    /// Count points that value changed.
    /// </summary>
    public int CountChanged { get; private set; }

    /// <summary>
    /// Count suited points
    /// </summary>
    public int CountSuited { get; private set; }

    /// <summary>
    /// Re-quantization matrix
    /// </summary>
    public RequantizationMatrix RequantizationMatrix { get; private set; } = requantizationMatrix;

    /// <summary>
    /// Changes coordinates of geometry points in vector tile.
    /// </summary>
    /// <param name="tile">Vector tile with geometry where needed to change coordinates</param>
    /// <param name="polygon">The polygon inside which should be points whose coordinates need to be changed</param>
    /// <param name="tileEnvelope">Envelope that bounding tile</param>
    public void ChangeCoordinate(VectorTile tile, Polygon polygon, Envelope tileEnvelope)
    {
        var extentDistance = tileEnvelope.Height / RequantizationMatrix.Extent;
        var step = (int)Math.Floor((double)Count / CountToChange);
        if (step == 0)
            step = 1;
        CountSuited = 0;
        CountChanged = 0;
        foreach (var layer in tile.Layers)
        {
            foreach (var feature in layer.Features)
            {
                var geometry = feature.Geometry;

                switch (geometry.OgcGeometryType)
                {
                    case OgcGeometryType.MultiPoint:
                        {
                            var multi = geometry as MultiPoint;
                            for (var i = 0; i < multi!.Count; i++)
                                multi.Geometries[i] = ChangeCoordinate(multi[i], step, polygon, tileEnvelope, extentDistance);
                            break;
                        }
                    case OgcGeometryType.MultiLineString:
                        {
                            var multi = geometry as MultiLineString;
                            for (var i = 0; i < multi!.Count; i++)
                                multi.Geometries[i] = ChangeCoordinate(multi[i], step, polygon, tileEnvelope, extentDistance);
                            break;
                        }
                    case OgcGeometryType.MultiPolygon:
                        {
                            var multi = geometry as MultiPolygon;
                            for (var i = 0; i < multi!.Count; i++)
                                multi.Geometries[i] = ChangeCoordinate(multi[i], step, polygon, tileEnvelope, extentDistance);
                            break;
                        }
                    default:
                        geometry = ChangeCoordinate(geometry, step, polygon, tileEnvelope, extentDistance);
                        break;
                }

                feature.Geometry = geometry;
            }
        }
    }

    /// <summary>
    /// Changes coordinates of geometry points.
    /// </summary>
    /// <param name="geometry">Geometry which coordinate changing</param>
    /// <param name="step">Step for found points</param>
    /// <param name="polygon">Bounds of M^M square</param>
    /// <param name="tileEnvelope">Tile envelope</param>
    /// <param name="extentDistance">Distances in meters for difference i and i+1 for extent</param>
    /// <returns>Changed geometry</returns>
    private Geometry ChangeCoordinate(Geometry geometry, int step, Polygon polygon, Envelope tileEnvelope, double extentDistance)
    {
        var coordinates = geometry.Coordinates;
        for (var j = 0; j < coordinates.Length; j++)
        {
            if (CountChanged >= CountToChange)
                return geometry;

            var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinates[j]);
            if (polygon.Contains(new Point(coordinateMeters)))
            {
                var intCoordinate = CoordinateConverter.MetersToInteger(coordinateMeters, tileEnvelope, extentDistance);

                var mapValue = RequantizationMatrix[intCoordinate];
                if (mapValue == null || mapValue == Value)
                    continue;

                CountSuited++;

                if (CountSuited % step == 0)
                {
                    var listPoints = RequantizationMatrix.FindOppositeIndices(intCoordinate);

                    foreach (var point in listPoints)
                    {
                        var geometryCopy = geometry.Copy();

                        var area = geometryCopy.Area;

                        var changedCoordinate = CoordinateConverter.MetersToDegrees(CoordinateConverter.IntegerToMeters(point, tileEnvelope, extentDistance));
                        var countChangedForPoint = 0;

                        if (intCoordinate.X != point.X)
                            geometryCopy.Coordinates[j].X = changedCoordinate.X;

                        if (intCoordinate.Y != point.Y)
                            geometryCopy.Coordinates[j].Y = changedCoordinate.Y;

                        countChangedForPoint++;

                        if (area != 0 && Math.Max(geometryCopy.Area, area) / Math.Min(geometryCopy.Area, area) > 3)
                            continue;

                        if (!geometryCopy.IsValid)
                        {
                            if (geometryCopy.GeometryType == "Polygon")
                            {
                                geometryCopy.Coordinates[^1].X = geometryCopy.Coordinates[0].X;
                                geometryCopy.Coordinates[^1].Y = geometryCopy.Coordinates[0].Y;
                                countChangedForPoint++;
                            }

                            if (!geometryCopy.IsValid)
                                continue;
                        }

                        CountChanged += countChangedForPoint;
                        geometry = geometryCopy;
                        break;
                    }
                }
            }
        }

        return geometry;
    }
}
