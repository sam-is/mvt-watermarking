using System;
using System.Collections.Generic;

namespace MvtWatermark.QimMvtWatermark;
public class Maps
{
    static private List<bool[,]?> _maps = new() { null, null, null, null, null, null, null, null, null, null };
    static private List<QimMvtWatermarkOptions?> _options = new() { null, null, null, null, null, null, null, null, null, null };

    static public bool[,] GetMap(QimMvtWatermarkOptions options, int key)
    {
        _options[key % 10] ??= options;

        if (_options[key % 10]!.R == options.R && _options[key % 10]!.Extent == options.Extent && _maps[key % 10] != null)
            return _maps[key % 10]!;

        _options[key % 10] = options;
        _maps[key % 10] = GenerateMap(key);
        return _maps[key % 10]!;
    }

    /// <summary>
    /// Generates re-quantization matrix
    /// </summary>
    /// <param name="key">Secret key</param>
    /// <returns>Re-quantization matrix</returns>
    static private bool[,] GenerateMap(int key)
    {
        var map = new bool[_options[key % 10]!.Extent, _options[key % 10]!.Extent];
        var random = new Random(key);
        for (var i = 0; i < _options[key % 10]!.Extent; i++)
            for (var j = 0; j < _options[key % 10]!.Extent; j++)
                map[i, j] = Convert.ToBoolean(random.Next() % 2);
        map = ChangeMap(map, key % 10);
        return map;
    }

    /// <summary>
    /// Modifies the re-quantization matrix so that each point has a point with the opposite value next to it
    /// </summary>
    /// <param name="map">Re-quantization matrix</param>
    /// <returns>Modified re-quantization matrix</returns>
    static private bool[,] ChangeMap(bool[,] map, int number)
    {
        for (var i = 0; i < _options[number]!.Extent; i++)
            for (var j = 0; j < _options[number]!.Extent; j++)
                if (!CheckMapPoint(map, i, j, number))
                    map[i, j] = !map[i, j];
        return map;
    }

    static private bool CheckMapPoint(bool[,] map, int x, int y, int number)
    {
        var value = map[x, y];

        if (CheckNearestPoints(map, x, y, value, number))
            return true;

        for (var i = 1; i < _options[number]!.Distance; ++i)
        {

            if (CheckNearestPoints(map, x + i, y, value, number))
                return true;
            if (CheckNearestPoints(map, x - i, y, value, number))
                return true;
            if (CheckNearestPoints(map, x, y + i, value, number))
                return true;
            if (CheckNearestPoints(map, x, y - i, value, number))
                return true;

        }
        return false;
    }

    /// <summary>
    /// Checks the nearest points for the opposite value
    /// </summary>
    /// <param name="map">Re-quantization matrix</param>
    /// <param name="x">X coordinate point in re-quantization matrix</param>
    /// <param name="y">Y coordinate point in re-quantization matrix</param>
    /// <param name="value">Point value in re-quantization matrix</param>
    /// <returns>True if found opposite value, false otherwise</returns>
    static private bool CheckNearestPoints(bool[,] map, int x, int y, bool value, int number)
    {
        if (x < 0 || x >= _options[number]!.Extent || y < 0 || y >= _options[number]!.Extent)
            return false;

        if (x + 1 < _options[number]!.Extent)
            if (map[x + 1, y] != value)
                return true;

        if (x - 1 >= 0)
            if (map[x - 1, y] != value)
                return true;

        if (y + 1 < _options[number]!.Extent)
            if (map[x, y + 1] != value)
                return true;

        if (y - 1 >= 0)
            if (map[x, y - 1] != value)
                return true;

        return false;
    }
}
