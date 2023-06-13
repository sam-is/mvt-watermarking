using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class ObjectsMagnifier: IDistortion
{
    private readonly double _relativeNewDotsNumber;
    public ObjectsMagnifier(double relativeNewDotsNumber)
    {
        if (relativeNewDotsNumber < 0)
            throw new ArgumentException("RelativeNewDotsNumber must be bigger then 0", $"relativeNewDotsNumber = {relativeNewDotsNumber}");

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
                    if (feature.Geometry is LineString)
                    {
                        copyFeature.Geometry = MagnifyDotsNumberLineString(feature);
                    }
                    else if (feature.Geometry is MultiLineString)
                    {
                        copyFeature.Geometry = MagnifyDotsNumberMultiLineString(feature);
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

    private Geometry MagnifyDotsNumberLineString(IFeature feature)
    {
        Coordinate[] coordinates = feature.Geometry.Coordinates;
        //Envelope boundingBox = feature.BoundingBox;
        //boundingBox = (Envelope)feature.Geometry.Envelope;
        var boundingBox = new Envelope(feature.Geometry.Coordinates);

        var coordinatesToAddNum = (int)Math.Ceiling(_relativeNewDotsNumber * coordinates.Length);
        var rand = new Random(coordinatesToAddNum);

        var extraCoords = new Coordinate[coordinatesToAddNum];
        for (var i = 0; i < coordinatesToAddNum; i++)
        {
            var x = rand.Next((int)Math.Floor(boundingBox.MinX), (int)Math.Ceiling(boundingBox.MaxX)) + (double)rand.Next(-10000, 10000)/10000;
            var y = rand.Next((int)Math.Floor(boundingBox.MinY), (int)Math.Ceiling(boundingBox.MaxY)) + (double)rand.Next(-10000, 10000)/10000;
            extraCoords[i] = new Coordinate(x, y);
        }

        var resultCoords = new Coordinate[coordinates.Length + coordinatesToAddNum];

        for (var i = 0; i < coordinates.Length; i++)
        {
            resultCoords[i] = new Coordinate(coordinates[i]); // координата это же класс, то есть ссылочный тип.
                                                              // Поэтому наполняем результирующий массив новыми координатами.
                                                              // Но надо проверить, не создаёт ли фабрика лайнстринга новые координаты.
                                                              // В таком случае этот цикл уже не нужен, можно просто CopyTo
        }
        extraCoords.CopyTo(resultCoords, coordinates.Length);

        return new LineString(resultCoords);
    }

    private Geometry MagnifyDotsNumberMultiLineString(IFeature feature)
    {
        //Envelope boundingBox = feature.BoundingBox;
        //boundingBox = (Envelope)feature.Geometry.Envelope;
        var boundingBox = new Envelope(feature.Geometry.Coordinates);

        var coordinatesToAddNum = (int)Math.Ceiling(_relativeNewDotsNumber * feature.Geometry.Coordinates.Length);
        var rand = new Random(coordinatesToAddNum);

        var extraCoords = new Coordinate[coordinatesToAddNum];
        for (var i = 0; i < coordinatesToAddNum; i++)
        {
            var x = rand.Next((int)Math.Floor(boundingBox.MinX), (int)Math.Ceiling(boundingBox.MaxX)) + (double)rand.Next(-10000, 10000) / 10000;
            var y = rand.Next((int)Math.Floor(boundingBox.MinY), (int)Math.Ceiling(boundingBox.MaxY)) + (double)rand.Next(-10000, 10000) / 10000;
            extraCoords[i] = new Coordinate(x, y);
        }

        var lineStringList = ((MultiLineString)feature.Geometry).Cast<LineString>().ToList();
        var lastLineStringCoords = lineStringList[^1].Coordinates;
        var lastLineStringCoordsNew = new Coordinate[lastLineStringCoords.Length + extraCoords.Length];
        lastLineStringCoords.CopyTo(lastLineStringCoordsNew, 0);
        extraCoords.CopyTo(lastLineStringCoordsNew, lastLineStringCoords.Length);
        lineStringList[lineStringList.Count - 1] = new LineString(lastLineStringCoordsNew);

        return new MultiLineString(lineStringList.ToArray());
    }
}
