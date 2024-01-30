using MvtWatermark.QimMvtWatermark.Requantization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using System;

namespace MvtWatermark.QimMvtWatermark;

/// <summary>
/// Collects statistics from vector tile. Counts how many coordinate points have value 1 and 0 in relative requantizationMatrix.
/// </summary>
/// <param name="tile">Vector tile with geometry</param>
/// <param name="requantizationMatrix">Re-quantization matrix</param>
/// <param name="tileEnvelope">Envelope that bounding tile</param>
/// <param name="threshold">If in square count points will be smaller than <c>threshold</c> then this square not counted. (<see cref="QimMvtWatermarkOptions.T1"/>)</param>
public class StatisticsCollector(VectorTile tile, RequantizationMatrix requantizationMatrix, Envelope tileEnvelope, int threshold)
{
    /// <summary>
    /// Vector tile with geometry.
    /// </summary>
    public VectorTile Tile { get; } = tile;
    /// <summary>
    /// Re-quantization matrix.
    /// </summary>
    public RequantizationMatrix RequantizationMatrix { get; } = requantizationMatrix;
    /// <summary>
    /// Envelope that bounding tile.
    /// </summary>
    public Envelope TileEnvelope { get; } = tileEnvelope;
    /// <summary>
    /// Distances in meters for difference i and i+1 for extent.
    /// </summary>
    public double ExtentDistance { get; } = tileEnvelope.Height / requantizationMatrix.Extent;
    public int Threshold { get; } = threshold;

    /// <summary>
    /// Counts statistics in square from M*M matrix, on the basis of which the value of the message bit is taken.
    /// </summary>
    /// <param name="geometry">Geometry bounding the square</param>
    /// <param name="s0">The number of values is zero</param>
    /// <param name="s1">The number of values is one</param>
    /// <returns>Relative value indicating how much one number is greater than another</returns>
    public double Collect(Geometry geometry, out int s0, out int s1)
    {
        s0 = 0;
        s1 = 0;

        foreach (var layer in Tile.Layers)
        {
            foreach (var feature in layer.Features)
            {
                var featureGeometry = feature.Geometry;
                var coordinates = featureGeometry.Coordinates;
                foreach (var coordinate in coordinates)
                {
                    var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinate);
                    if (geometry.Contains(new Point(coordinateMeters)))
                    {
                        var intCoorinate = CoordinateConverter.MetersToInteger(coordinateMeters, TileEnvelope, ExtentDistance);
                        var mapValue = RequantizationMatrix[intCoorinate];

                        if (mapValue == null)
                            continue;

                        if ((bool)mapValue)
                            s1++;
                        else
                            s0++;
                    }
                }
            }
        }

        if ((s0 == 0 && s1 == 0) || s0 + s1 < Threshold)
            return -1;

        return (double)Math.Abs(s0 - s1) / (s1 + s0);
    }
}
