using System;
using System.Collections.Generic;

namespace MvtWatermark.QimMvtWatermark.Requantization;
public class GeneratorOfRequantizationMatrices
{
    private readonly List<bool[,]?> _maps;
    private readonly List<QimMvtWatermarkOptions?> _options;
    private readonly int _count;

    public GeneratorOfRequantizationMatrices(int count)
    {
        _count = count;
        _maps = new List<bool[,]?>(count);
        _options = new List<QimMvtWatermarkOptions?>(count);
        for (var i = 0; i < count; i++)
        {
            _maps.Add(null);
            _options.Add(null);
        }
    }

    public bool[,] GetMap(QimMvtWatermarkOptions options, int key)
    {
        if (_maps[key % _count] != null && _options[key % _count]!.Distance == options.Distance && _options[key % _count]!.Extent == options.Extent)
            return _maps[key % _count]!;

        _options[key % _count] = new QimMvtWatermarkOptions(options);
        _maps[key % _count] = GenerateMap(key);
        return _maps[key % _count]!;
    }

    /// <summary>
    /// Generates re-quantization matrix
    /// </summary>
    /// <param name="key">Secret key</param>
    /// <returns>Re-quantization matrix</returns>
    private bool[,] GenerateMap(int key)
    {
        var map = new bool[_options[key % _count]!.Extent, _options[key % _count]!.Extent];
        var random = new Random(key % _count);
        for (var i = 0; i < _options[key % _count]!.Extent; i++)
            for (var j = 0; j < _options[key % _count]!.Extent; j++)
                map[i, j] = Convert.ToBoolean(random.Next() % 2);
        map = ChangeMap(map, key % _count);
        return map;
    }

    /// <summary>
    /// Modifies the re-quantization matrix so that each point has a point with the opposite value next to it
    /// </summary>
    /// <param name="map">Re-quantization matrix</param>
    /// <returns>Modified re-quantization matrix</returns>
    private bool[,] ChangeMap(bool[,] map, int number)
    {
        for (var i = 0; i < _options[number]!.Extent; i++)
            for (var j = 0; j < _options[number]!.Extent; j++)
                if (!CheckMapPoint(map, i, j, number))
                    map[i, j] = !map[i, j];
        return map;
    }

    private bool CheckMapPoint(bool[,] map, int x, int y, int number)
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
    private bool CheckNearestPoints(bool[,] map, int x, int y, bool value, int number)
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
