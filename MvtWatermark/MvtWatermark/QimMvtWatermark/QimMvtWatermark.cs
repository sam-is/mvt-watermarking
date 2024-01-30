using MvtWatermark.QimMvtWatermark.ExtractingMethods;
using MvtWatermark.QimMvtWatermark.MessagePreparing.Embed;
using MvtWatermark.QimMvtWatermark.MessagePreparing.Extract;
using MvtWatermark.QimMvtWatermark.Requantization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Tile = NetTopologySuite.IO.VectorTiles.Tiles.Tile;

namespace MvtWatermark.QimMvtWatermark;

public class QimMvtWatermark(QimMvtWatermarkOptions options) : IMvtWatermark
{
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

        var winx = GeneratorMatrix.GenerateRandomMatrixWithIndices(key, options.M, options.Nb, options.R);
        var map = options.Maps.GetMap(options, key);
        var requantizationMatrix = new RequantizationMatrix(map, options.Distance);
        var statisticsCollector = new StatisticsCollector(copyTile, requantizationMatrix, envelopeTile, options.T1);

        for (var i = 0; i < options.M; i++)
        {
            for (var j = 0; j < options.M; j++)
            {
                var index = winx[i, j];
                if (index == -1)
                    continue;
                var value = message[index];

                var polygon = GeneratorBoundsPolygon.Get(envelopeTile, options.M, i, j);

                var stat = statisticsCollector.Collect(polygon, out var s0, out var s1);
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
                coordinatesChanger.ChangeCoordinate(copyTile, polygon, envelopeTile);
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

        var winx = GeneratorMatrix.GenerateRandomMatrixWithIndices(key, options.M, options.Nb, options.R);
        var map = options.Maps.GetMap(options, key);
        var requantizationMatrix = new RequantizationMatrix(map, options.Extent);
        var statisticsCollector = new StatisticsCollector(tile, requantizationMatrix, envelopeTile, options.T1);

        IExtractingMethod extractorBits;
        if (options.IsGeneralExtractionMethod)
            extractorBits = new GeneralExtractionMethod(options.Nb, options.T2);
        else
            extractorBits = new MajorityExtractionMethod(options.Nb);

        for (var i = 0; i < options.M; i++)
        {
            for (var j = 0; j < options.M; j++)
            {

                var index = winx[i, j];
                if (index == -1)
                    continue;

                var polygon = GeneratorBoundsPolygon.Get(envelopeTile, options.M, i, j);

                var stat = statisticsCollector.Collect(polygon, out var s0, out var s1);
                if (Math.Abs(stat + 1) < 0.00001)
                    continue;

                embedded = true;
                if (stat >= options.T2)
                    extractorBits.AddStatistics(index, s0, s1);
            }
        }

        if (!embedded)
            return null;

        return extractorBits.GetBits();
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
            Mode.WithTilesMajorityVote => Embed(tileTree, key, new EmbedMajorityVoice(message, options.Nb)),
            Mode.WithCheck => Embed(tileTree, key, message.Length, new EmbedCheck(message, options.Nb)),
            Mode.Repeat => Embed(tileTree, key, new EmbedRepeat(message, tileTree.Select(id => id), options.Nb)),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Embeds a message into VectorTileTree
    /// </summary>
    /// <param name="tileTree">VectorTileTree for embedding message</param>
    /// <param name="key">Secret key</param>
    /// <param name="messageLength">Message length</param>
    /// <param name="messagePreparing">Message preparing object <see cref="IMessageForEmbed{int}>"/></param>
    /// <returns>VectorTileTree with an embedded message</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public VectorTileTree Embed(VectorTileTree tileTree, int key, int messageLength, IMessageForEmbed<int> messagePreparing)
    {
        var index = 0;
        var copyTileTree = new VectorTileTree();
        foreach (var tileId in tileTree)
        {
            var tile = tileTree[tileId];
            var bits = messagePreparing.GetPart(index);

            var copyTile = Embed(tile, Math.Abs(key + (int)tileId), bits);
            if (copyTile == null || !VectorTileUtils.IsValidForRead(copyTile))
            {
                copyTileTree[tileId] = tile;
                continue;
            }

            copyTileTree[tileId] = copyTile;
            index += options.Nb;
        }
        if (index < messageLength)
            throw new InvalidOperationException("Not all of the message was embedded, try reducing the message size or increasing the nb parameter.");
        return copyTileTree;
    }

    /// <summary>
    /// Embeds a message into VectorTileTree
    /// </summary>
    /// <param name="tileTree">VectorTileTree for embedding message</param>
    /// <param name="key">Secret key</param>
    /// <param name="messagePreparing">Message preparing object <see cref="IMessageForEmbed{ulong}>"/></param>
    /// <returns>VectorTileTree with an embedded message</returns>
    public VectorTileTree Embed(VectorTileTree tileTree, int key, IMessageForEmbed<ulong> messagePreparing)
    {
        var dictionaryTiles = new ConcurrentDictionary<ulong, VectorTile>();
        Parallel.ForEach(tileTree, tileId =>
        {
            var tile = tileTree[tileId];
            var bits = messagePreparing.GetPart(tileId);

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
            Mode.WithTilesMajorityVote => Extract(tileTree, key, new ExtractMajorityVoice(options.MessageLength, options.Nb)),
            Mode.WithCheck => Extract(tileTree, key, new ExtractCheck(tileTree.Select(id => id), options.Nb)),
            Mode.Repeat => Extract(tileTree, key, new ExtractRepeat(tileTree.Select(id => id), options.Nb)),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Extracts an embedded message from VectorTileTree
    /// </summary>
    /// <param name="tileTree">VectorTileTree to extract the message from</param>
    /// <param name="key">Secret key</param>
    /// <param name="messagePreparing">Message preparing object <see cref="IMessageFromExtract{int}"/></param>
    /// <returns>Extracted message</returns>
    public BitArray Extract(VectorTileTree tileTree, int key, IMessageFromExtract<int> messagePreparing)
    {
        var index = 0;
        foreach (var tileId in tileTree)
        {
            var tile = tileTree[tileId];
            var bits = Extract(tile, Math.Abs(key + (int)tileId));
            if (bits != null)
            {
                messagePreparing.SetPart(bits, index);
                index += bits.Count;
            }
        }
        return messagePreparing.Get();
    }

    /// <summary>
    /// Extracts an embedded message from VectorTileTree
    /// </summary>
    /// <param name="tileTree">VectorTileTree to extract the message from</param>
    /// <param name="key">Secret key</param>
    /// <param name="messagePreparing">Message preparing object <see cref="IMessageFromExtract{ulong}"/></param>
    /// <returns>Extracted message</returns>
    public BitArray Extract(VectorTileTree tileTree, int key, IMessageFromExtract<ulong> messagePreparing)
    {
        Parallel.ForEach(tileTree, tileId =>
        {
            if (tileTree[tileId] == null)
                return;

            var tile = tileTree[tileId];
            var bits = Extract(tile, Math.Abs(key + (int)tileId));
            messagePreparing.SetPart(bits, tileId);
        });

        return messagePreparing.Get();
    }
}
