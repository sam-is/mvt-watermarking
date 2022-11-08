using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Tiles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MvtWatermark.QimMvtWatermark
{
    public class QimMvtWatermark : IMvtWatermark
    {
        private readonly QimMvtWatermarkOptions _options;

        private int[,] GenerateWinx(int key)
        {
            //var r = (int)Math.Floor((double)_options.M * _options.M / sizeMessage);
            var sizeMessage = (int)Math.Floor((double)_options.M * _options.M / _options.R);
            var random = new Random(key);
            var winx = new int[_options.M, _options.M];

            for (int i = 0; i < _options.M; i++)
                for (int j = 0; j < _options.M; j++)
                    winx[i, j] = -1;


            for (var i = 0; i < sizeMessage; i++)
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

        private bool[,] ChangeMap(bool[,] map)
        {
            var count = 0;
            for (var i = 0; i < _options.Extent; i++)
                for (var j = 0; j < _options.Extent; j++)
                    if (!CheckMapPoint(map, i, j))
                    {
                        ++count;
                        if (Convert.ToInt32(map[i, j]) == 0)
                            map[i, j] = Convert.ToBoolean(1);
                        else
                            map[i, j] = Convert.ToBoolean(0);
                    }
            return map;
        }

        private bool CheckMapPoint(bool[,] map, int x, int y)
        {
            var value = Convert.ToInt32(map[x, y]);

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

        private bool CheckNearestPoints(bool[,] map, int x, int y, int value)
        {
            if (x < 0 || x >= _options.Extent || y < 0 || y >= _options.Extent)
                return false;

            if (x + 1 < _options.Extent)
                if (Convert.ToInt32(map[x + 1, y]) != value)
                    return true;

            if (x - 1 >= 0)
                if (Convert.ToInt32(map[x - 1, y]) != value)
                    return true;

            if (y + 1 < _options.Extent)
                if (Convert.ToInt32(map[x, y + 1]) != value)
                    return true;

            if (y - 1 >= 0)
                if (Convert.ToInt32(map[x, y - 1]) != value)
                    return true;

            return false;
        }

        private double Statistics(VectorTile tile, Polygon polygon, bool[,] map, Envelope envelopeTile, double extentDist, out int s0, out int s1)
        {
            s0 = 0;
            s1 = 0;

            foreach (var layer in tile.Layers)
                foreach (var feature in layer.Features)
                {
                    var geometry = feature.Geometry;
                    var coordinates = geometry.Coordinates;
                    foreach (var coordinate in coordinates)
                    {
                        var coordinateMeters = CoordinateConverter.DegreesToMeters(coordinate);
                        if (polygon.Contains(new Point(coordinateMeters)))
                        {
                            var x = Convert.ToInt32((coordinateMeters.X - envelopeTile.MinX) / extentDist);
                            var y = Convert.ToInt32((coordinateMeters.Y - envelopeTile.MinY) / extentDist);
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

        private struct IntPoint
        {
            public int x;
            public int y;
            public IntPoint(int x, int y) { this.x = x; this.y = y; }
        }

        private List<IntPoint> GetOppositePoint(bool[,] map, int x, int y, int value)
        {
            int xRes;
            int yRes;

            var listPoints = new List<IntPoint>();

            if (x + 1 < _options.Extent)
                if (Convert.ToInt32(map[x + 1, y]) != value)
                {
                    xRes = x + 1;
                    yRes = y;
                    listPoints.Add(new IntPoint(xRes, yRes));
                }

            if (x - 1 >= 0)
                if (Convert.ToInt32(map[x - 1, y]) != value)
                {
                    xRes = x - 1;
                    yRes = y;
                    listPoints.Add(new IntPoint(xRes, yRes));
                }

            if (y + 1 < _options.Extent)
                if (Convert.ToInt32(map[x, y + 1]) != value)
                {
                    xRes = x;
                    yRes = y + 1;
                    listPoints.Add(new IntPoint(xRes, yRes));
                }

            if (y - 1 >= 0)
                if (Convert.ToInt32(map[x, y - 1]) != value)
                {
                    xRes = x;
                    yRes = y - 1;
                    listPoints.Add(new IntPoint(xRes, yRes));
                }
            return listPoints;
        }
        private List<IntPoint> FindOppositeIndexes(bool[,] map, int value, int x, int y)
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

        private void ChangeCoordinate(VectorTile tile, Polygon polygon, Envelope envelopeTile, double extentDist, bool[,] map, int value, int countToChange, int count)
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
                            var x = Convert.ToInt32((coordinateMeters.X - envelopeTile.MinX) / extentDist);
                            var y = Convert.ToInt32((coordinateMeters.Y - envelopeTile.MinY) / extentDist);

                            if (x == _options.Extent || y == _options.Extent)
                                continue;

                            var mapValue = Convert.ToInt32(map[x, y]);
                            if (mapValue == value)
                                continue;

                            countSuited++;

                            if (countSuited % step == 0)
                            {
                                var listPoints = FindOppositeIndexes(map, mapValue, x, y);

                                foreach (var point in listPoints)
                                {
                                    var geometryCopy = geometry.Copy();
                                    double xMeteres = envelopeTile.MinX + point.x * extentDist;
                                    double yMeteres = envelopeTile.MinY + point.y * extentDist;
                                    var coor = CoordinateConverter.MetersToDegrees(new Coordinate(xMeteres, yMeteres));
                                    var tmp = 0;
                                    if (x != point.x)
                                        geometryCopy.Coordinates[j].X = coor.X;
                                    if (y != point.y)
                                        geometryCopy.Coordinates[j].Y = coor.Y;
                                    tmp++;

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
                                        if (coordinate.X == coordinateMeters.X && coordinate.Y == coordinateMeters.Y)
                                        {
                                            if (x != point.x)
                                                geometryCopy.Coordinates[k].X = coor.X;
                                            if (y != point.y)
                                                geometryCopy.Coordinates[k].Y = coor.Y;
                                            tmp++;
                                        }
                                    }

                                    if (!geometryCopy.IsValid)
                                        continue;

                                    countChanged += tmp;
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

        public VectorTile Embed(VectorTile tile, ulong id, int key, BitArray message)
        {
            var t = new Tile(id);
            var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);
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

                    var stat = Statistics(tile, polygon, map, envelopeTile, extentDist, out int s0, out int s1);
                    if (stat == -1)
                        continue;

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
                        ChangeCoordinate(tile, polygon, envelopeTile, extentDist, map, 1, countAdded, s0);
                    }

                    if (value == 0)
                    {
                        var countAdded = (int)Math.Ceiling(((s0 + s1) * (_options.T2 + _options.Delta2) + s1 - s0) / 2);
                        ChangeCoordinate(tile, polygon, envelopeTile, extentDist, map, 0, countAdded, s1);
                    }
                }
            return tile;
        }

        public BitArray Extract(VectorTile tile, ulong id, int key)
        {
            var t = new Tile(id);
            var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);

            var a = envelopeTile.Height / _options.M;
            var extentDist = envelopeTile.Height / _options.Extent;

            var winx = GenerateWinx(key);
            var map = GenerateMap(key);


            var sizeMessage = (int)Math.Floor((double)_options.M * _options.M / _options.R);
            var bits = new BitArray(sizeMessage, false);
            var dict = new Dictionary<int, int>(sizeMessage);

            for (var i = 0; i < sizeMessage; i++)
                dict.Add(i, 0);

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

                    var stat = Statistics(tile, polygon, map, envelopeTile, extentDist, out int s0, out int s1);
                    if (stat == -1)
                        continue;

                    if (stat >= _options.T2)
                    {
                        if (s0 > s1)
                            dict[index] -= 1;

                        if (s1 > s0)
                            dict[index] += 1;
                    }
                }

            for (var i = 0; i < sizeMessage; i++)
            {
                if (dict[i] > 0)
                    bits[i] = true;
                if (dict[i] < 0)
                    bits[i] = false;
            }

            return bits;
        }
        public QimMvtWatermark(QimMvtWatermarkOptions options)
        {
            _options = options;
        }

        public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
        {
            var countTiles = tiles.Count();

            var countBit = (int)Math.Floor((double)_options.M * _options.M / _options.R);
            if (countBit > (int)Math.Floor((double)message.Count / countTiles))
                throw new ArgumentOutOfRangeException();

            int current = 0;
            foreach (var tileId in tiles)
            {
                if (tiles.TryGet(tileId, out VectorTile tile))
                {
                    var bits = new BitArray(countBit);
                    for (var i = 0; i < countBit; i++)
                        bits[i] = message[i + current];
                    current += countBit;

                    tiles[tileId] = Embed(tile, tileId, key, bits);
                }
            }
            return tiles;
        }

        public BitArray Extract(VectorTileTree tiles, int key)
        {
            var countBit = (int)Math.Floor((double)_options.M * _options.M / _options.R);

            var message = new bool[countBit * tiles.Count()];
            var index = 0;
            foreach (var tileId in tiles)
            {
                if (tiles.TryGet(tileId, out VectorTile tile))
                {
                    var bits = Extract(tile, tileId, key);
                    bits.CopyTo(message, index);
                    index += bits.Count;
                }
            }
            return new BitArray(message);
        }
    }
}