using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Tiles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark;

public class QimMvtWatermark : IMvtWatermark
{
    private readonly QimMvtWatermarkOptions _options;

    /// <summary>
    /// Generates a matrix with embedded message indexes
    /// </summary>
    /// <param name="key">Secret key</param>
    /// <returns>matrix with embedded message indexes</returns>
    private int[,] GenerateWinx(int key)
    {
        var random = new Random(key);
        var winx = new int[_options.M, _options.M];

        for (var i = 0; i < _options.M; i++)
            for (var j = 0; j < _options.M; j++)
                winx[i, j] = -1;


        for (var i = 0; i < _options.Nb; i++)
        {
            for (var j = 0; j < _options.R; j++)
            {
                int x;
                int y;
                do
                {
                    x = random.Next() % _options.M;
                    y = random.Next() % _options.M;
                } while (winx[x, y] != -1);

                winx[x, y] = i;
            }
        }

        return winx;
    }

    /// <summary>
    /// Generates re-quantization matrix
    /// </summary>
    /// <param name="key">Secret key</param>
    /// <returns>Re-quantization matrix</returns>
    private bool[,] GenerateMap(int key)
    {
        var map = new bool[_options.Extent, _options.Extent];
        var random = new Random(key);
        for (var i = 0; i < _options.Extent; i++)
            for (var j = 0; j < _options.Extent; j++)
                map[i, j] = Convert.ToBoolean(random.Next() % 2);
        map = ChangeMap(map);
        return map;
    }

    /// <summary>
    /// Modifies the re-quantization matrix so that each point has a point with the opposite value next to it
    /// </summary>
    /// <param name="map">Re-quantization matrix</param>
    /// <returns>Modified re-quantization matrix</returns>
    private bool[,] ChangeMap(bool[,] map)
    {
        for (var i = 0; i < _options.Extent; i++)
            for (var j = 0; j < _options.Extent; j++)
                if (!CheckMapPoint(map, i, j))
                    map[i, j] = !map[i, j];
        return map;
    }

    private bool CheckMapPoint(bool[,] map, int x, int y)
    {
        var value = map[x, y];

        if (CheckNearestPoints(map, x, y, value))
            return true;

        for (var i = 1; i < _options.Distance; ++i)
        {

            if (CheckNearestPoints(map, x + i, y, value))
                return true;
            if (CheckNearestPoints(map, x - i, y, value))
                return true;
            if (CheckNearestPoints(map, x, y + i, value))
                return true;
            if (CheckNearestPoints(map, x, y - i, value))
                return true;

        }
        return false;
    }

    /// <summary>
    /// Сhecks the nearest points for the opposite value
    /// </summary>
    /// <param name="map">Re-quantization matrix</param>
    /// <param name="x">X coordinate point in re-quantization matrix</param>
    /// <param name="y">Y coordinate point in re-quantization matrix</param>
    /// <param name="value">Point value in re-quantization matrix</param>
    /// <returns></returns>
    private bool CheckNearestPoints(bool[,] map, int x, int y, bool value)
    {
        if (x < 0 || x >= _options.Extent || y < 0 || y >= _options.Extent)
            return false;

        if (x + 1 < _options.Extent)
            if (map[x + 1, y] != value)
                return true;

        if (x - 1 >= 0)
            if (map[x - 1, y] != value)
                return true;

        if (y + 1 < _options.Extent)
            if (map[x, y + 1] != value)
                return true;

        if (y - 1 >= 0)
            if (map[x, y - 1] != value)
                return true;

        return false;
    }

    /// <summary>
    /// Counts statistics in square from M*M matrix, on the basis of which the value of the message bit is taken
    /// </summary>
    /// <param name="tile">Vector tile with geometry</param>
    /// <param name="geometry">Geometry bounding the square</param>
    /// <param name="map">Re-quantization matrix</param>
    /// <param name="tileEnvelope">Envelope that bounding tile</param>
    /// <param name="extentDist">Distances in meters for difference i and i+1 for extent</param>
    /// <param name="s0">The number of values is zero</param>
    /// <param name="s1">The number of values is one</param>
    /// <returns>Relative value indicating how much one number is greater than another</returns>
    private double Statistics(VectorTile tile, Geometry geometry, bool[,] map, Envelope tileEnvelope, double extentDist, out int s0, out int s1)
    {
        s0 = 0;
        s1 = 0;

        foreach (var layer in tile.Layers)
            foreach (var feature in layer.Features)
            {
                var featureGeometry = feature.Geometry;
                var coordinates = featureGeometry.Coordinates;
                foreach (var coordinate in coordinates)
                {
                    var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinate);
                    if (geometry.Contains(new Point(coordinateMeters)))
                    {
                        var x = Convert.ToInt32((coordinateMeters.X - tileEnvelope.MinX) / extentDist);
                        var y = Convert.ToInt32((coordinateMeters.Y - tileEnvelope.MinY) / extentDist);
                        if (x == _options.Extent || y == _options.Extent)
                            continue;
                        var mapValue = Convert.ToInt32(map[x, y]);

                        if (mapValue == 1)
                            s1++;
                        else
                            s0++;
                    }
                }
            }

        if ((s0 == 0 && s1 == 0) || s0 + s1 < _options.CountPoints)
            return -1;

        return (double)Math.Abs(s0 - s1) / (s1 + s0);
    }

    private record IntPoint(int X, int Y);

    private IEnumerable<IntPoint> GetOppositePoint(bool[,] map, int x, int y, bool value)
    {
        var listPoints = new List<IntPoint>();

        if (x + 1 < _options.Extent)
            if (map[x + 1, y] != value)
                listPoints.Add(new IntPoint(x + 1, y));

        if (x - 1 >= 0)
            if (map[x - 1, y] != value)
                listPoints.Add(new IntPoint(x - 1, y));

        if (y + 1 < _options.Extent)
            if (map[x, y + 1] != value)
                listPoints.Add(new IntPoint(x, y + 1));

        if (y - 1 >= 0)
            if (map[x, y - 1] != value)
                listPoints.Add(new IntPoint(x, y - 1));

        return listPoints;
    }

    /// <summary>
    /// Searches for the nearest points with the opposite value in the re-quantization matrix
    /// </summary>
    /// <param name="map">Re-quantization matrix</param>
    /// <param name="value">The opposite value to which to look for</param>
    /// <param name="x">X coordinate point in re-quantization matrix</param>
    /// <param name="y">Y coordinate point in re-quantization matrix</param>
    /// <returns>List of found points</returns>
    private List<IntPoint> FindOppositeIndexes(bool[,] map, bool value, int x, int y)
    {
        var listPoints = new List<IntPoint>();

        if (CheckNearestPoints(map, x, y, value))
        {
            var l = GetOppositePoint(map, x, y, value);
            listPoints.AddRange(l);
        }

        for (var i = 1; i < _options.Distance; ++i)
        {

            if (CheckNearestPoints(map, x + i, y, value))
            {
                var l = GetOppositePoint(map, x + i, y, value);
                listPoints.AddRange(l);
            }

            if (CheckNearestPoints(map, x - i, y, value))
            {
                var l = GetOppositePoint(map, x - i, y, value);
                listPoints.AddRange(l);
            }

            if (CheckNearestPoints(map, x, y + i, value))
            {
                var l = GetOppositePoint(map, x, y + 1, value);
                listPoints.AddRange(l);
            }

            if (CheckNearestPoints(map, x, y - i, value))
            {
                var l = GetOppositePoint(map, x, y - 1, value);
                listPoints.AddRange(l);
            };
        }
        return listPoints;
    }

    /// <summary>
    /// Changes coordinates of geometry points in a certain area
    /// </summary>
    /// <param name="tile">Vector tile with geometry where needed to change coordinates</param>
    /// <param name="polygon">The polygon where the coordinates will change</param>
    /// <param name="tileEnvelope">Envelope that bounding tile</param>
    /// <param name="extentDist">Distances in meters for difference i and i+1 for extent</param>
    /// <param name="map">Matrix re-quantization</param>
    /// <param name="value">The value that corresponds to the value in the re-quantization matrix to which the coordinates will need to be shifted</param>
    /// <param name="countToChange">The number of points that need to change coordinates</param>
    /// <param name="count">Total number of points</param>
    private void ChangeCoordinate(VectorTile tile, Polygon polygon, Envelope tileEnvelope, double extentDist, bool[,] map, bool value, int countToChange, int count)
    {
        var step = (int)Math.Floor((double)count / countToChange);
        var countChanged = 0;
        var countSuited = 0;
        foreach (var layer in tile.Layers)
        {
            foreach (var feature in layer.Features)
            {
                var geometry = feature.Geometry;
                var coordinates = geometry.Coordinates;
                for (var j = 0; j < coordinates.Length; j++)
                {
                    if (countChanged >= countToChange)
                    {
                        return;
                    }

                    var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinates[j]);
                    if (polygon.Contains(new Point(coordinateMeters)))
                    {
                        var x = Convert.ToInt32((coordinateMeters.X - tileEnvelope.MinX) / extentDist);
                        var y = Convert.ToInt32((coordinateMeters.Y - tileEnvelope.MinY) / extentDist);

                        if (x == _options.Extent || y == _options.Extent)
                            continue;

                        var mapValue = map[x, y];
                        if (mapValue == value)
                            continue;

                        countSuited++;

                        if (countSuited % step == 0)
                        {
                            var listPoints = FindOppositeIndexes(map, mapValue, x, y);

                            foreach (var point in listPoints)
                            {
                                var geometryCopy = geometry.Copy();
                                var xMeters = tileEnvelope.MinX + point.X * extentDist;
                                var yMeters = tileEnvelope.MinY + point.Y * extentDist;
                                var coord = CoordinateConverter.MetersToDegrees(new Coordinate(xMeters, yMeters));
                                var countChangedForPoint = 0;
                                if (x != point.X)
                                    geometryCopy.Coordinates[j].X = coord.X;
                                if (y != point.Y)
                                    geometryCopy.Coordinates[j].Y = coord.Y;
                                countChangedForPoint++;

                                if (!geometryCopy.IsValid)
                                {
                                    if (geometryCopy.GeometryType == "Polygon")
                                    {
                                        geometryCopy.Coordinates[^1].X = geometryCopy.Coordinates[0].X;
                                        geometryCopy.Coordinates[^1].Y = geometryCopy.Coordinates[0].Y;
                                    }
                                    if (!geometryCopy.IsValid)
                                        continue;
                                }

                                for (var k = j + 1; k < geometryCopy.Coordinates.Length; k++)
                                {
                                    var coordinate = CoordinateConverter.DegreesToMeters(geometryCopy.Coordinates[k]);
                                    if (Math.Abs(coordinate.X - coordinateMeters.X) < extentDist * 0.0001 && Math.Abs(coordinate.Y - coordinateMeters.Y) < extentDist * 0.0001)
                                    {
                                        if (x != point.X)
                                            geometryCopy.Coordinates[k].X = coord.X;
                                        if (y != point.Y)
                                            geometryCopy.Coordinates[k].Y = coord.Y;
                                        countChangedForPoint++;
                                    }
                                }

                                if (!geometryCopy.IsValid)
                                    continue;

                                countChanged += countChangedForPoint;
                                geometry = geometryCopy;
                                if (!feature.Geometry.IsValid)
                                    continue;
                                break;
                            }
                        }
                    }
                }
                feature.Geometry = geometry;
            }
        }
    }

    /// <summary>
    /// Embeds a message into one vector tile
    /// </summary>
    /// <param name="tiles">Vector tile for embedding message</param>
    /// <param name="key">Secret key</param>
    /// <param name="message">The message to embed</param>
    /// <returns>Vector tile with an embedded message</returns>
    public VectorTile Embed(VectorTile tile, int key, BitArray message)
    {
        var embeded = false;
        var t = new Tile(tile.TileId);
        var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);
        envelopeTile = CoordinateConverter.DegreesToMeters(envelopeTile);
        var a = envelopeTile.Height / _options.M;
        var extentDist = envelopeTile.Height / _options.Extent;

        var winx = GenerateWinx(key);
        var map = GenerateMap(key);

        for (var i = 0; i < _options.M; i++)
            for (var j = 0; j < _options.M; j++)
            {
                var index = winx[i, j];
                if (index == -1)
                    continue;
                var value = Convert.ToInt32(message[index]);

                var polygon = new Polygon(
                    new LinearRing(
                        new Coordinate[]
                        {
                                    new(envelopeTile.MinX + a * i, envelopeTile.MinY + a * j),
                                    new(envelopeTile.MinX + a * i, envelopeTile.MinY + a * (j + 1)),
                                    new(envelopeTile.MinX + a * (i + 1), envelopeTile.MinY + a * (j + 1)),
                                    new(envelopeTile.MinX + a * (i + 1), envelopeTile.MinY + a * j),
                                    new(envelopeTile.MinX + a * i, envelopeTile.MinY + a * j)
                        }
                ));

                var stat = Statistics(tile, polygon, map, envelopeTile, extentDist, out var s0, out var s1);
                if (Math.Abs(stat + 1) < 0.00001)
                    continue;

                embeded = true;
                if (stat >= _options.T2 + _options.Delta2)
                {
                    if (s1 - s0 > 0 && value == 1)
                        continue;

                    if (s0 - s1 > 0 && value == 0)
                        continue;
                }

                if (value == 1)
                {
                    var countAdded = (int)Math.Ceiling(((s0 + s1) * (_options.T2 + _options.Delta2) + s0 - s1) / 2);
                    ChangeCoordinate(tile, polygon, envelopeTile, extentDist, map, true, countAdded, s0);
                }

                if (value == 0)
                {
                    var countAdded = (int)Math.Ceiling(((s0 + s1) * (_options.T2 + _options.Delta2) + s1 - s0) / 2);
                    ChangeCoordinate(tile, polygon, envelopeTile, extentDist, map, false, countAdded, s1);
                }
            }
        if (!embeded)
            return null!;
        return tile;
    }

    /// <summary>
    /// Extracts an embedded message from one vector tile
    /// </summary>
    /// <param name="tiles">Vector tile to extract the message from</param>
    /// <param name="key">Secret key</param>
    /// <returns>Extracted message</returns>
    public BitArray Extract(VectorTile tile, int key)
    {
        var embeded = false;
        var t = new Tile(tile.TileId);
        var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);
        envelopeTile = CoordinateConverter.DegreesToMeters(envelopeTile);

        var a = envelopeTile.Height / _options.M;
        var extentDist = envelopeTile.Height / _options.Extent;

        var winx = GenerateWinx(key);
        var map = GenerateMap(key);

        var bits = new BitArray(_options.Nb, false);

        var dictGeneralExtractionMethod = new Dictionary<int, Tuple<int, int>>(_options.Nb);
        var dict = new Dictionary<int, int>(_options.Nb);

        for (var i = 0; i < _options.Nb; i++)
        {
            dictGeneralExtractionMethod.Add(i, new(0, 0));
            dict.Add(i, 0);
        }

        for (var i = 0; i < _options.M; i++)
            for (var j = 0; j < _options.M; j++)
            {

                var index = winx[i, j];
                if (index == -1)
                    continue;

                var polygon = new Polygon(
                    new LinearRing(
                        new Coordinate[]
                        {
                                    new(envelopeTile.MinX + a * i, envelopeTile.MinY + a * j),
                                    new(envelopeTile.MinX + a * i, envelopeTile.MinY + a * (j + 1)),
                                    new(envelopeTile.MinX + a * (i + 1), envelopeTile.MinY + a * (j + 1)),
                                    new(envelopeTile.MinX + a * (i + 1), envelopeTile.MinY + a * j),
                                    new(envelopeTile.MinX + a * i, envelopeTile.MinY + a * j)
                        }
                ));

                var stat = Statistics(tile, polygon, map, envelopeTile, extentDist, out var s0, out var s1);
                if (Math.Abs(stat + 1) < 0.00001)
                    continue;

                dictGeneralExtractionMethod[index] = new(dictGeneralExtractionMethod[index].Item1 + s0, dictGeneralExtractionMethod[index].Item2 + s1);

                embeded = true;
                if (stat >= _options.T2)
                {
                    if (s0 > s1)
                        dict[index] -= 1;

                    if (s1 > s0)
                        dict[index] += 1;
                }
            }

        if (_options.IsGeneralExtractionMethod)
        {
            for (var i = 0; i < _options.Nb; i++)
            {
                var s0 = dictGeneralExtractionMethod[i].Item1;
                var s1 = dictGeneralExtractionMethod[i].Item2;
                if ((double)Math.Abs(s0 - s1) / (s1 + s0) > _options.T2)
                {
                    if (s1 > s0)
                        bits[i] = true;
                    if (s0 > s1)
                        bits[i] = false;
                }
            }
        }
        else
        {
            for (var i = 0; i < _options.Nb; i++)
            {
                if (dict[i] > 0)
                    bits[i] = true;
                if (dict[i] < 0)
                    bits[i] = false;
            }
        }

        if (!embeded)
            return null!;

        return bits;
    }
    public QimMvtWatermark(QimMvtWatermarkOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Embeds a message into VectorTileTree
    /// </summary>
    /// <param name="tiles">VectorTileTree for embedding message</param>
    /// <param name="key">Secret key</param>
    /// <param name="message">The message to embed</param>
    /// <returns>VectorTileTree with an embedded message</returns>
    public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
    {
        var current = 0;
        foreach (var tileId in tiles)
        {
            if (tiles.TryGet(tileId, out var tile))
            {
                if (current >= message.Count)
                    break;
                var bits = new BitArray(_options.Nb);
                for (var i = 0; i < _options.Nb && i < message.Count; i++)
                    bits[i] = message[i + current];

                tile = Embed(tile, key+(int)tileId, bits);
                if (tile == null)
                    continue;

                tiles[tileId] = tile;
                current += _options.Nb;
            }
        }
        return tiles;
    }

    /// <summary>
    /// Extracts an embedded message from vectortiletree
    /// </summary>
    /// <param name="tiles">VectorTileTree to extract the message from</param>
    /// <param name="key">Secret key</param>
    /// <returns>Extracted message</returns>
    public BitArray Extract(VectorTileTree tiles, int key)
    {
        var message = new bool[_options.Nb * tiles.Count()];
        var index = 0;
        foreach (var tileId in tiles)
        {
            if (tiles.TryGet(tileId, out var tile))
            {
                var bits = Extract(tile, key + (int)tileId);
                if (bits != null)
                {
                    bits.CopyTo(message, index);
                    index += bits.Count;
                }
            }
        }
        return new BitArray(message);
    }
}
