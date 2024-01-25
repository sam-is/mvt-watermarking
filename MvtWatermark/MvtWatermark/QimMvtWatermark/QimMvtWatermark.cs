using MvtWatermark.QimMvtWatermark.ExtractingMethods;
using MvtWatermark.QimMvtWatermark.Requantization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tile = NetTopologySuite.IO.VectorTiles.Tiles.Tile;

namespace MvtWatermark.QimMvtWatermark;

public class QimMvtWatermark(QimMvtWatermarkOptions options) : IMvtWatermark
{
    /// <summary>
    /// Counts statistics in square from M*M matrix, on the basis of which the value of the message bit is taken
    /// </summary>
    /// <param name="tile">Vector tile with geometry</param>
    /// <param name="geometry">Geometry bounding the square</param>
    /// <param name="map">Re-quantization matrix</param>
    /// <param name="tileEnvelope">Envelope that bounding tile</param>
    /// <param name="extentDistance">Distances in meters for difference i and i+1 for extent</param>
    /// <param name="s0">The number of values is zero</param>
    /// <param name="s1">The number of values is one</param>
    /// <returns>Relative value indicating how much one number is greater than another</returns>
    private double Statistics(VectorTile tile, Geometry geometry, RequantizationMatrix requantizationMatrix, Envelope tileEnvelope, double extentDistance, out int s0, out int s1)
    {
        s0 = 0;
        s1 = 0;

        foreach (var layer in tile.Layers)
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
                        var intCoorinate = CoordinateConverter.MetersToInteger(coordinateMeters, tileEnvelope, extentDistance);
                        var mapValue = requantizationMatrix[intCoorinate];

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

        if ((s0 == 0 && s1 == 0) || s0 + s1 < options.T1)
            return -1;

        return (double)Math.Abs(s0 - s1) / (s1 + s0);
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

        var copyTile = VectorTileUtils.Copy(tile);
        var embedded = false;
        var tileInforamtion = new Tile(copyTile.TileId);
        var envelopeTile = CoordinateConverter.DegreesToMeters(CoordinateConverter.TileBounds(tileInforamtion.X, tileInforamtion.Y, tileInforamtion.Zoom));
        var extentDistance = envelopeTile.Height / options.Extent;

        var winx = GeneratorMatrix.GenerateRandomMatrixWithIndices(key, options.M, options.Nb, options.R);
        var map = options.Maps.GetMap(options, key);
        var requantizationMatrix = new RequantizationMatrix(map, options.Extent, options.Distance);

        for (var i = 0; i < options.M; i++)
        {
            for (var j = 0; j < options.M; j++)
            {
                var index = winx[i, j];
                if (index == -1)
                    continue;
                var value = message[index];

                var polygon = GeneratorBoundsPolygon.Get(envelopeTile, options.M, i, j);

                var stat = Statistics(copyTile, polygon, requantizationMatrix, envelopeTile, extentDistance, out var s0, out var s1);
                if (Math.Abs(stat + 1) < 0.00001)
                    continue;

                embedded = true;
                if (stat >= options.T2 + options.Delta2)
                    if ((s1 - s0 > 0 && value) || (s0 - s1 > 0 && !value))
                        continue;

                var countAdded = (int)Math.Ceiling(((s0 + s1) * (options.T2 + options.Delta2) + (value ? s0 : -s0) + (value ? -s1 : s1)) / 2);
                if (countAdded <= 0)
                    continue;

                var coordinatesChanger = new CoordinatesChanger(countAdded, value, value ? s0 : s1, requantizationMatrix);
                coordinatesChanger.ChangeCoordinate(copyTile, polygon, envelopeTile, extentDistance);
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
        var tileInforamtion = new Tile(tile.TileId);
        var envelopeTile = CoordinateConverter.DegreesToMeters(CoordinateConverter.TileBounds(tileInforamtion.X, tileInforamtion.Y, tileInforamtion.Zoom));
        var extentDistance = envelopeTile.Height / options.Extent;

        var winx = GeneratorMatrix.GenerateRandomMatrixWithIndices(key, options.M, options.Nb, options.R);
        var map = options.Maps.GetMap(options, key);
        var requantizationMatrix = new RequantizationMatrix(map, options.Extent);

        IExtractingMethod extractorOfBits;
        if (options.IsGeneralExtractionMethod)
            extractorOfBits = new GeneralExtractionMethod(options.Nb, options.T2);
        else
            extractorOfBits = new MajorityExtractionMethod(options.Nb);

        for (var i = 0; i < options.M; i++)
        {
            for (var j = 0; j < options.M; j++)
            {

                var index = winx[i, j];
                if (index == -1)
                    continue;

                var polygon = GeneratorBoundsPolygon.Get(envelopeTile, options.M, i, j);

                var stat = Statistics(tile, polygon, requantizationMatrix, envelopeTile, extentDistance, out var s0, out var s1);
                if (Math.Abs(stat + 1) < 0.00001)
                    continue;

                embedded = true;
                if (stat >= options.T2)
                    extractorOfBits.AddStatistics(index, s0, s1);
            }
        }

        if (!embedded)
            return null;

        return extractorOfBits.GetBits();
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
            if (copyTile == null || !VectorTileUtils.IsValidForRead(copyTile))
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
        var dictionaryTiles = new ConcurrentDictionary<ulong, VectorTile>();

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
            if (copyTile == null || !VectorTileUtils.IsValidForRead(copyTile))
                dictionaryTiles[tileId] = tile;
            else
                dictionaryTiles[tileId] = copyTile;
        });

        var copyTileTree = new VectorTileTree();

        foreach (var tileId in dictionaryTiles.Keys)
            copyTileTree[tileId] = dictionaryTiles[tileId];

        return copyTileTree;
    }

    public VectorTileTree EmbedWithMajorityVote(VectorTileTree tileTree, int key, BitArray message)
    {
        var dictionaryTiles = new ConcurrentDictionary<ulong, VectorTile>();
        var dictionaryMessage = GetMessageDictonary(message, options.Nb);

        Parallel.ForEach(tileTree, tileId =>
        {
            if (tileTree[tileId] == null)
                return;

            var tile = tileTree[tileId];
            var bits = new BitArray(dictionaryMessage[Convert.ToInt32(tileId % (ulong)Math.Floor((double)message.Length / options.Nb))]);

            var copyTile = Embed(tile, Math.Abs(key + (int)tileId), bits);
            if (copyTile == null || !VectorTileUtils.IsValidForRead(copyTile))
                dictionaryTiles[tileId] = tile;
            else
                dictionaryTiles[tileId] = copyTile;
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
            if (tileTree[tileId] == null)
                return;

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
        if (copyTile == null || !VectorTileUtils.IsValidForRead(copyTile))
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
}
