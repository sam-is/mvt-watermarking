using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvtWatermark.QimMvtWatermark;

public class QimMvtWatermark(QimMvtWatermarkOptions options) : IMvtWatermark
{

    /// <summary>
    /// Generates a matrix with embedded message indexes
    /// </summary>
    /// <param name="key">Secret key</param>
    /// <returns>Matrix with embedded message indexes</returns>
    private int[,] GenerateWinx(int key)
    {
        var random = new Random(key);
        var winx = new int[options.M, options.M];

        for (var i = 0; i < options.M; i++)
            for (var j = 0; j < options.M; j++)
                winx[i, j] = -1;


        for (var i = 0; i < options.Nb; i++)
        {
            for (var j = 0; j < options.R; j++)
            {
                int x;
                int y;
                do
                {
                    x = random.Next() % options.M;
                    y = random.Next() % options.M;
                } while (winx[x, y] != -1);

                winx[x, y] = i;
            }
        }

        return winx;
    }

    /// <summary>
    /// Checks the nearest points for the opposite value
    /// </summary>
    /// <param name="map">Re-quantization matrix</param>
    /// <param name="x">X coordinate point in re-quantization matrix</param>
    /// <param name="y">Y coordinate point in re-quantization matrix</param>
    /// <param name="value">Point value in re-quantization matrix</param>
    /// <returns>True if found opposite value, false otherwise</returns>
    private bool CheckNearestPoints(bool[,] map, int x, int y, bool value)
    {
        if (x < 0 || x >= options.Extent || y < 0 || y >= options.Extent)
            return false;

        if (x + 1 < options.Extent)
            if (map[x + 1, y] != value)
                return true;

        if (x - 1 >= 0)
            if (map[x - 1, y] != value)
                return true;

        if (y + 1 < options.Extent)
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
                        if (x == options.Extent || y == options.Extent)
                            continue;
                        var mapValue = Convert.ToInt32(map[x, y]);

                        if (mapValue == 1)
                            s1++;
                        else
                            s0++;
                    }
                }
            }

        if ((s0 == 0 && s1 == 0) || s0 + s1 < options.T1)
            return -1;

        return (double)Math.Abs(s0 - s1) / (s1 + s0);
    }

    private record IntPoint(int X, int Y);

    private List<IntPoint> GetOppositePoint(bool[,] map, int x, int y, bool value)
    {
        var listPoints = new List<IntPoint>();

        if (x + 1 < options.Extent)
            if (map[x + 1, y] != value)
                listPoints.Add(new IntPoint(x + 1, y));

        if (x - 1 >= 0)
            if (map[x - 1, y] != value)
                listPoints.Add(new IntPoint(x - 1, y));

        if (y + 1 < options.Extent)
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

        for (var i = 1; i < options.Distance; ++i)
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
            }
        }
        return listPoints;
    }

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
    private void ChangeCoordinate(VectorTile tile, Polygon polygon, Envelope tileEnvelope,
                                  double extentDist, bool[,] map, bool value, int countToChange, int count)
    {
        var step = (int)Math.Floor((double)count / countToChange);
        if (step == 0)
            step = 1;
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

                        if (x == options.Extent || y == options.Extent)
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
                                var areas = new List<double>();

                                if (geometryCopy.GeometryType == "MultiPolygon")
                                {
                                    var multipolygon = geometryCopy as MultiPolygon;
                                    foreach (Polygon p in multipolygon!.Cast<Polygon>())
                                        areas.Add(p.Area);
                                }
                                else
                                {
                                    areas.Add(geometryCopy.Area);
                                }

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

                                    if (geometryCopy.GeometryType == "MultiPolygon")
                                    {
                                        var multipolygon = geometryCopy as MultiPolygon;
                                        foreach (Polygon p in multipolygon!.Cast<Polygon>())
                                        {
                                            p.Coordinates[^1].X = p.Coordinates[0].X;
                                            p.Coordinates[^1].Y = p.Coordinates[0].Y;
                                        }
                                    }

                                    countChangedForPoint++;

                                    if (!geometryCopy.IsValid)
                                        continue;
                                }

                                var isBroken = false;
                                if (geometryCopy.GeometryType == "MultiPolygon")
                                {
                                    var multipolygon = geometryCopy as MultiPolygon;
                                    for (var i = 0; i < multipolygon!.Geometries.Length; i++)
                                        if (Math.Max(multipolygon[i].Area, areas[i]) / Math.Min(multipolygon[i].Area, areas[i]) > 3)
                                            isBroken = true;
                                }
                                else
                                {
                                    if (Math.Max(geometryCopy.Area, areas[0]) / Math.Min(geometryCopy.Area, areas[0]) > 3)
                                        isBroken = true;
                                }

                                if (isBroken)
                                    continue;

                                countChanged += countChangedForPoint;
                                geometry = geometryCopy;
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
    /// <param name="tile">Vector tile for embedding message</param>
    /// <param name="key">Secret key</param>
    /// <param name="message">The message to embed</param>
    /// <returns>Vector tile with an embedded message</returns>
    public VectorTile? Embed(VectorTile tile, int key, BitArray message)
    {
        var copyTile = new VectorTile { TileId = tile.TileId };
        foreach (var layer in tile.Layers)
        {
            var l = new Layer { Name = layer.Name };
            foreach (var feature in layer.Features)
                l.Features.Add(new Feature(feature.Geometry.Copy(), feature.Attributes));

            copyTile.Layers.Add(l);
        }

        var embedded = false;
        var t = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(copyTile.TileId);
        var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);
        envelopeTile = CoordinateConverter.DegreesToMeters(envelopeTile);
        var a = envelopeTile.Height / options.M;
        var extentDist = envelopeTile.Height / options.Extent;

        var winx = GenerateWinx(key);
        var map = options.Maps.GetMap(options, key);

        for (var i = 0; i < options.M; i++)
        {
            for (var j = 0; j < options.M; j++)
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

                var stat = Statistics(copyTile, polygon, map, envelopeTile, extentDist, out var s0, out var s1);
                if (Math.Abs(stat + 1) < 0.00001)
                    continue;

                embedded = true;
                if (stat >= options.T2 + options.Delta2)
                {
                    if (s1 - s0 > 0 && value == 1)
                        continue;

                    if (s0 - s1 > 0 && value == 0)
                        continue;
                }

                if (value == 1)
                {
                    var countAdded = (int)Math.Ceiling(((s0 + s1) * (options.T2 + options.Delta2) + s0 - s1) / 2);
                    ChangeCoordinate(copyTile, polygon, envelopeTile, extentDist, map, true, countAdded, s0);
                }

                if (value == 0)
                {
                    var countAdded = (int)Math.Ceiling(((s0 + s1) * (options.T2 + options.Delta2) + s1 - s0) / 2);
                    ChangeCoordinate(copyTile, polygon, envelopeTile, extentDist, map, false, countAdded, s1);
                }
            }
        }

        if (!embedded)
            return null;
        return copyTile;
    }

    /// <summary>
    /// Extracts an embedded message from one vector tile
    /// </summary>
    /// <param name="tile">Vector tile to extract the message from</param>
    /// <param name="key">Secret key</param>
    /// <returns>Extracted message</returns>
    public BitArray? Extract(VectorTile tile, int key)
    {
        var embedded = false;
        var t = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile.TileId);
        var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);
        envelopeTile = CoordinateConverter.DegreesToMeters(envelopeTile);

        var a = envelopeTile.Height / options.M;
        var extentDist = envelopeTile.Height / options.Extent;

        var winx = GenerateWinx(key);
        var map = options.Maps.GetMap(options, key);

        var bits = new BitArray(options.Nb, false);

        var dictGeneralExtractionMethod = new Dictionary<int, Tuple<int, int>>(options.Nb);
        var dict = new Dictionary<int, int>(options.Nb);

        for (var i = 0; i < options.Nb; i++)
        {
            dictGeneralExtractionMethod.Add(i, new(0, 0));
            dict.Add(i, 0);
        }

        for (var i = 0; i < options.M; i++)
        {
            for (var j = 0; j < options.M; j++)
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

                embedded = true;
                if (stat >= options.T2)
                {
                    if (s0 > s1)
                        dict[index] -= 1;

                    if (s1 > s0)
                        dict[index] += 1;
                }
            }
        }

        if (options.IsGeneralExtractionMethod)
        {
            for (var i = 0; i < options.Nb; i++)
            {
                var s0 = dictGeneralExtractionMethod[i].Item1;
                var s1 = dictGeneralExtractionMethod[i].Item2;
                if ((double)Math.Abs(s0 - s1) / (s1 + s0) > options.T2)
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
            for (var i = 0; i < options.Nb; i++)
            {
                if (dict[i] > 0)
                    bits[i] = true;
                if (dict[i] < 0)
                    bits[i] = false;
            }
        }

        if (!embedded)
            return null;

        return bits;
    }

    /// <summary>
    /// Embeds a message into VectorTileTree
    /// </summary>
    /// <param name="tileTree">VectorTileTree for embedding message</param>
    /// <param name="key">Secret key</param>
    /// <param name="message">The message to embed</param>
    /// <returns>VectorTileTree with an embedded message</returns>
    public VectorTileTree Embed(VectorTileTree tileTree, int key, BitArray message)
    {
        return options.Mode switch
        {
            Mode.WithTilesMajorityVote => EmbedWithMajorityVote(tileTree, key, message),
            Mode.WithCheck => EmbedWithCheck(tileTree, key, message),
            Mode.Repeat => EmbedRepeat(tileTree, key, message),
            _ => throw new NotImplementedException(),
        };
    }

    public VectorTileTree EmbedWithCheck(VectorTileTree tileTree, int key, BitArray message)
    {
        var current = 0;
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tileTree)
        {
            var tile = tileTree[tileId];

            var bits = new BitArray(options.Nb);
            for (var i = 0; i < options.Nb; i++)
                bits[i] = message[(i + current) % message.Count];

            var copyTile = Embed(tile, Math.Abs(key + (int)tileId), bits);
            if (copyTile == null || !IsValidForRead(copyTile))
            {
                copyTileTree[tileId] = tile;
                continue;
            }

            copyTileTree[tileId] = copyTile;
            current += options.Nb;
        }
        if (current < message.Count)
            throw new ArgumentException("Not all of the message was embedded, try reducing the message size or increasing the nb parameter.", nameof(message));
        return copyTileTree;
    }

    public VectorTileTree EmbedRepeat(VectorTileTree tileTree, int key, BitArray message)
    {
        var concurrentDictionary = new ConcurrentDictionary<ulong, VectorTile>();

        var messages = new bool[options.Nb * tileTree.Count()];

        for (var i = 0; i < messages.Length; i++)
            messages[i] = message[i % message.Count];

        var dict = new ConcurrentDictionary<ulong, bool[]>();
        var iter = 0;
        foreach (var tileId in tileTree)
        {
            dict[tileId] = messages.Take(new Range(iter, iter + options.Nb)).ToArray();
            iter++;
        }

        Parallel.ForEach(tileTree, tileId =>
        {
            var tile = tileTree[tileId];
            var bits = new BitArray(dict[tileId]);

            var copyTile = Embed(tile, Math.Abs(key + (int)tileId), bits);
            if (copyTile == null || !IsValidForRead(copyTile))
                concurrentDictionary[tileId] = tile;
            else
                concurrentDictionary[tileId] = copyTile;
        });

        var copyTileTree = new VectorTileTree();

        foreach (var tileId in concurrentDictionary.Keys)
            copyTileTree[tileId] = concurrentDictionary[tileId];

        return copyTileTree;
    }

    public VectorTileTree EmbedWithMajorityVote(VectorTileTree tileTree, int key, BitArray message)
    {
        var dictionaryTiles = new ConcurrentDictionary<ulong, VectorTile>();
        var dictionaryMessage = GetMessageDictonary(message, options.Nb);

        Parallel.ForEach(tileTree, tileId =>
        {
            var tile = Embed(tileTree[tileId], key, dictionaryMessage, message.Length);
            if (tile == null)
                dictionaryTiles[tileId] = tileTree[tileId];
            else
                dictionaryTiles[tileId] = tile;
        });

        var copyTileTree = new VectorTileTree();

        foreach (var tileId in dictionaryTiles.Keys)
            copyTileTree[tileId] = dictionaryTiles[tileId];

        return copyTileTree;
    }

    /// <summary>
    /// Extracts an embedded message from VectorTileTree
    /// </summary>
    /// <param name="tileTree">VectorTileTree to extract the message from</param>
    /// <param name="key">Secret key</param>
    /// <returns>Extracted message</returns>
    public BitArray Extract(VectorTileTree tileTree, int key)
    {
        return options.Mode switch
        {
            Mode.WithTilesMajorityVote => ExtractWithMajorityVote(tileTree, key, options.MessageLength),
            Mode.WithCheck => ExtractWithCheck(tileTree, key),
            Mode.Repeat => ExtractRepeat(tileTree, key),
            _ => throw new NotImplementedException(),
        };
    }

    public BitArray ExtractWithCheck(VectorTileTree tileTree, int key)
    {
        var message = new bool[options.Nb * tileTree.Count()];
        var index = 0;
        foreach (var tileId in tileTree)
        {
            var tile = tileTree[tileId];
            var bits = Extract(tile, Math.Abs(key + (int)tileId));
            if (bits != null)
            {
                bits.CopyTo(message, index);
                index += bits.Count;
            }
        }
        return new BitArray(message.Take(index).ToArray());
    }

    public BitArray ExtractRepeat(VectorTileTree tileTree, int key)
    {
        var message = new bool[options.Nb * tileTree.Count()];

        var dict = new Dictionary<ulong, int>();
        var iter = 0;
        foreach (var tileId in tileTree)
        {
            dict.Add(tileId, iter);
            iter++;
        }

        Parallel.ForEach(tileTree, tileId =>
        {
            var tile = tileTree[tileId];
            var bits = Extract(tile, Math.Abs(key + (int)tileId));
            bits?.CopyTo(message, dict[tileId] * options.Nb);
        });

        return new BitArray(message);
    }

    public BitArray ExtractWithMajorityVote(VectorTileTree tileTree, int key, int? sizeMessage)
    {
        if (sizeMessage == null)
            throw new ArgumentNullException(nameof(sizeMessage));

        var dict = new ConcurrentDictionary<int, int[]>();
        var indices = (int)Math.Floor((double)sizeMessage / options.Nb);
        for (var i = 0; i < indices; i++)
            dict[i] = new int[options.Nb];

        Parallel.ForEach(tileTree, tileId =>
        {
            var tile = tileTree[tileId];

            var index = Convert.ToInt32(tileId % (ulong)Math.Floor((double)sizeMessage / options.Nb));

            var bitArray = Extract(tile, Math.Abs(key + (int)tileId));
            if (bitArray == null)
                return;

            for (var i = 0; i < bitArray.Length; i++)
                dict[index][i] += bitArray[i] == true ? 1 : -1;
        });

        var result = new bool[options.Nb * indices];

        for (var i = 0; i < indices; i++)
        {
            for (var j = 0; j < dict[i].Length; j++)
            {
                if (dict[i][j] > 0)
                    result[i * options.Nb + j] = true;
                else
                    result[i * options.Nb + j] = false;
            }
        }

        return new BitArray(result);
    }

    public VectorTile? Embed(VectorTile tile, int key, IDictionary<int, bool[]> dictionaryMessage, int messageLength)
    {
        var tileId = tile.TileId;
        var bits = new BitArray(dictionaryMessage[Convert.ToInt32(tileId % (ulong)Math.Floor((double)messageLength / options.Nb))]);

        var copyTile = Embed(tile, Math.Abs(key + (int)tileId), bits);
        if (copyTile == null || !IsValidForRead(copyTile))
            return null;
        else
            return copyTile;
    }

    public static ConcurrentDictionary<int, bool[]> GetMessageDictonary(BitArray message, int nb)
    {
        var dictionaryMessage = new ConcurrentDictionary<int, bool[]>();
        var step = (double)message.Length / nb;
        var tmparray = new bool[message.Length];
        message.CopyTo(tmparray, 0);
        var iter = 0;
        for (var i = 0; i < step; i++)
        {
            dictionaryMessage[i] = tmparray.Take(new Range(iter, iter + nb)).ToArray();
            iter += nb;
        }

        return dictionaryMessage;
    }

    public static bool IsValidForRead(VectorTile tile)
    {
        var reader = new MapboxTileReader();
        using var memoryStream = new MemoryStream();
        tile.Write(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        try
        {
            var readTile = reader.Read(memoryStream, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile.TileId));
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }
}
