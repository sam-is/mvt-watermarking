using System;
using System.Collections.Generic;
using static MvtWatermark.QimMvtWatermark.CoordinateConverter;

namespace MvtWatermark.QimMvtWatermark.Requantization;

/// <summary>
/// Helps work with re-quantization matrix.
/// </summary>
/// <param name="map">Re-quantization matrix</param>
/// <param name="extent">Extent of tile</param>
/// <param name="distance">Value that bigger than distance between opposite value in matrix</param>
public class RequantizationMatrix(bool[,] map, int extent, int distance = 0)
{
    /// <summary>
    /// Re-quantization matrix.
    /// </summary>
    public bool[,] Map { get; } = map;
    /// <summary>
    /// Extent of tile.
    /// </summary>
    public int Extent { get; } = extent;
    /// <summary>
    /// Value that bigger than distance between opposite value in matrix.
    /// </summary>
    public int Distance { get; } = distance;

    /// <summary>
    /// Returns value from re-quantization matrix by x, y.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>Value from re-quantization matrix</returns>
    public bool? this[int x, int y]
    {
        get
        {
            if (x >= Extent || y >= Extent || x < 0 || y < 0)
                return null;
            return Map[x, y];
        }
    }

    /// <summary>
    /// Returns value from re-quantization matrix by <see cref="IntPoint"/>
    /// </summary>
    /// <param name="point">Point from re-quantization matrix</param>
    /// <returns>Value from re-quantization matrix</returns>
    public bool? this[IntPoint point]
    {
        get
        {
            if (point.X >= Extent || point.Y >= Extent || point.X < 0 || point.Y < 0)
                return null;
            return Map[point.X, point.Y];
        }
    }

    /// <summary>
    /// Finds the nearest points for the opposite value
    /// </summary>
    /// <param name="x">X coordinate point in re-quantization matrix</param>
    /// <param name="y">Y coordinate point in re-quantization matrix</param>
    /// <param name="value">Point value in re-quantization matrix</param>
    /// <returns>List of found points with opposite value</returns>
    public List<IntPoint> GetOppositePoint(int x, int y, bool value)
    {
        var listPoints = new List<IntPoint>();

        if (this[x + 1, y] != null && this[x + 1, y] != value)
            listPoints.Add(new IntPoint(x + 1, y));

        if (this[x - 1, y] != null && this[x - 1, y] != value)
            listPoints.Add(new IntPoint(x - 1, y));

        if (this[x, y + 1] != null && this[x, y + 1] != value)
            listPoints.Add(new IntPoint(x, y + 1));

        if (this[x, y - 1] != null && this[x, y - 1] != value)
            listPoints.Add(new IntPoint(x, y - 1));

        return listPoints;
    }

    /// <summary>
    /// Searches for the nearest points with the opposite value in the re-quantization matrix
    /// </summary>
    /// <param name="x">X coordinate point in re-quantization matrix</param>
    /// <param name="y">Y coordinate point in re-quantization matrix</param>
    /// <returns>List of found points</returns>
    public List<IntPoint> FindOppositeIndices(int x, int y)
    {
        var shiftBaseValues = new List<Tuple<int, int>> { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        var listPoints = new List<IntPoint>();

        var value = Map[x, y];
        listPoints.AddRange(GetOppositePoint(x, y, value));

        for (var i = 1; i < Distance; i++)
        {
            foreach (var (baseX, baseY) in shiftBaseValues)
                listPoints.AddRange(GetOppositePoint(x + i * baseX, y + i * baseY, value));
        }

        return listPoints;
    }

    /// <summary>
    /// Searches for the nearest points with the opposite value in the re-quantization matrix
    /// </summary>
    /// <param name="intPoint">Coordinate point in re-quantization matrix</param>
    /// <returns></returns>
    public List<IntPoint> FindOppositeIndices(IntPoint intPoint) => FindOppositeIndices(intPoint.X, intPoint.Y);
}
