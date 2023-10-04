using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distortion;

// НЕ ГОТОВО
public class NewDotsInjector: IDistortion
{
    private readonly double _relativeNewDotsNumber;
    public NewDotsInjector(double relativeNewDotsNumber)
    {
        _relativeNewDotsNumber = relativeNewDotsNumber;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var vectorTile = tiles[tileId];
            var copyTile = new VectorTile() { TileId = tileId };

            foreach (var layer in vectorTile.Layers)
            {
                var copyLayer = new Layer() { Name = layer.Name };

                foreach (var feature in layer.Features)
                {
                    var copyFeature = new Feature() { Attributes = feature.Attributes };
                    Feature.ComputeBoundingBoxWhenItIsMissing = false; // Мб можно как-то иначе?
                    //copyFeature.Geometry = MagnifyDotsNumber(feature.Geometry.Coordinates, feature.BoundingBox);
                    if (feature.Geometry is LineString)
                    {
                        copyFeature.Geometry = InjectDotsNumber(feature);
                    }
                    else
                    {
                        copyFeature.Geometry = feature.Geometry;
                    }

                    copyLayer.Features.Add(copyFeature);
                }
                copyTile.Layers.Add(copyLayer);
            }
            copyTileTree[tileId] = copyTile;
        }
        return copyTileTree;
    }

    private Geometry InjectDotsNumber(IFeature feature)
    {
        Coordinate[] coordinates = feature.Geometry.Coordinates;
        var coordinatesToAddNum = (int)Math.Ceiling(_relativeNewDotsNumber * coordinates.Length);

        var resultCoords = new Coordinate[coordinates.Length + coordinatesToAddNum];

        for (var i = 0; i < coordinates.Length; i++)
        {
            resultCoords[i] = new Coordinate(coordinates[i]); // координата это же класс, то есть ссылочный тип.
                                                              // Поэтому наполняем результирующий массив новыми координатами.
                                                              // Но надо проверить, не создаёт ли фабрика лайнстринга новые координаты.
                                                              // В таком случае этот цикл уже не нужен, можно просто CopyTo

        }
        //extraCoords.CopyTo(resultCoords, coordinates.Length);

        //Console.WriteLine($"Дорастили {coordinatesToAddNum} точек к объекту"); // ОТЛАДКА

        return new LineString(resultCoords);
    }

    //private ()
}
