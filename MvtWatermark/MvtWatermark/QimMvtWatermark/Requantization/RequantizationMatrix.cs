using System;
using System.Collections.Generic;
using static MvtWatermark.QimMvtWatermark.CoordinateConverter;

namespace MvtWatermark.QimMvtWatermark.Requantization;
public class RequantizationMatrix(bool[,] map, int extent, int distance = 0)
{
    public bool[,] Map { get; } = map;
    public int Extent { get; } = extent;
    public int Distance { get; } = distance;

    public bool? this[int x, int y]
    {
        get
        {
            if (x >= Extent || y >= Extent || x < 0 || y < 0)
                return null;
            return Map[x, y];
        }
    }

    public bool? this[IntPoint point]
    {
        get
        {
            if (point.X >= Extent || point.Y >= Extent || point.X < 0 || point.Y < 0)
                return null;
            return Map[point.X, point.Y];
        }
    }

    ///// <summary>
    ///// Finds the nearest points for the opposite value
    ///// </summary>
    ///// <param name="x">X coordinate point in re-quantization matrix</param>
    ///// <param name="y">Y coordinate point in re-quantization matrix</param>
    ///// <param name="value">Point value in re-quantization matrix</param>
    ///// <returns>List of found points with opposite value</returns>
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
    /// <param name="value">The opposite value to which to look for</param>
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

    public List<IntPoint> FindOppositeIndices(IntPoint intPoint) => FindOppositeIndices(intPoint.X, intPoint.Y);
}
