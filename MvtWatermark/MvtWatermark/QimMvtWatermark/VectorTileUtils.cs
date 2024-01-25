using NetTopologySuite.Features;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.IO;
using Tile = NetTopologySuite.IO.VectorTiles.Tiles.Tile;

namespace MvtWatermark.QimMvtWatermark;
public static class VectorTileUtils
{
    public static bool IsValidForRead(VectorTile tile)
    {
        var reader = new MapboxTileReader();
        using var memoryStream = new MemoryStream();
        tile.Write(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        try
        {
            var readTile = reader.Read(memoryStream, new Tile(tile.TileId));
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }

    public static VectorTile Copy(VectorTile tile)
    {
        var copyTile = new VectorTile { TileId = tile.TileId };
        foreach (var layer in tile.Layers)
        {
            var newLayer = new Layer { Name = layer.Name };
            foreach (var feature in layer.Features)
                newLayer.Features.Add(new Feature(feature.Geometry.Copy(), feature.Attributes));

            copyTile.Layers.Add(newLayer);
        }

        return copyTile;
    }
}
