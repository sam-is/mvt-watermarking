using MvtWatermark.QimMvtWatermark.Requantization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using System;

namespace MvtWatermark.QimMvtWatermark;
public class CoordinatesChanger(int countToChange, bool value, int count, RequantizationMatrix requantizationMatrix)
{
    public int CountToChange { get; } = countToChange;
    public bool Value { get; } = value;
    public int Count { get; } = count;
    public int CountChanged { get; set; } = 0;
    public int CountSuited { get; set; } = 0;
    public RequantizationMatrix RequantizationMatrix { get; set; } = requantizationMatrix;

    /// <summary>
    /// Changes coordinates of geometry points in a certain area
    /// </summary>
    /// <param name="tile">Vector tile with geometry where needed to change coordinates</param>
    /// <param name="polygon">The polygon inside which should be points whose coordinates need to be changed</param>
    /// <param name="tileEnvelope">Envelope that bounding tile</param>
    /// <param name="extentDist">Distances in meters for difference i and i+1 for extent</param>
    /// <param name="map">Matrix re-quantization</param>
    /// <param name="value">The value that corresponds to the value in the re-quantization matrix to which the coordinates will need to be shifted</param>
    /// <param name="countToChange">The number of points that need to change coordinates</param>
    /// <param name="count">Total number of points</param>
    public void ChangeCoordinate(VectorTile tile, Polygon polygon, Envelope tileEnvelope,
                                  double extentDist)
    {
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

                if (geometry.GeometryType == "MultiPolygon")
                {
                    var multipolygon = geometry as MultiPolygon;
                    for (var i = 0; i < multipolygon!.Count; i++)
                    {
                        var p = multipolygon[i];
                        Geometry newGeometry;
                        newGeometry = ChangeCoordinate(p, step, polygon, tileEnvelope, extentDist);
                        multipolygon.Geometries[i] = newGeometry;
                    }
                }
                else
                {
                    geometry = ChangeCoordinate(geometry, step, polygon, tileEnvelope, extentDist);
                }

                feature.Geometry = geometry;
            }
        }
    }

    private Geometry ChangeCoordinate(Geometry geometry, int step, Polygon polygon, Envelope tileEnvelope,
                                  double extentDistance)
    {
        var coordinates = geometry.Coordinates;
        for (var j = 0; j < coordinates.Length; j++)
        {
            if (CountChanged >= CountToChange)
            {
                return geometry;
            }

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
