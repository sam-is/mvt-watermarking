﻿using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;

public class DeletingByAreaDistortion : IDistortion
{
    private readonly double _relativeArea;

    public DeletingByAreaDistortion(double relativeArea)
    {
        _relativeArea = relativeArea;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var tile = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var envelopeTile = CoordinateConverter.TileBounds(tile.X, tile.Y, tile.Zoom);
            var tileArea = envelopeTile.Area;
            var area = tileArea * _relativeArea;

            var copyTile = new VectorTile { TileId = tileId };

            foreach (var layer in tiles[tileId].Layers)
            {
                var l = new Layer { Name = layer.Name };
                foreach (var feature in layer.Features)
                {
                    if (feature.Geometry.Area > area || feature.Geometry is not IPolygonal) 
                        // моя поправочка, чтобы искажение не удаляло точки и лайнстринги, так как их площадь нулевая.
                        // хотя, может лучше проверять bounding box лайнстрингов?
                    {
                        var copyFeature = new Feature(feature.Geometry, feature.Attributes);
                        l.Features.Add(copyFeature);
                    }
                }
                copyTile.Layers.Add(l);
            }

            copyTileTree[tileId] = copyTile;
        }

        return copyTileTree;
    }
}