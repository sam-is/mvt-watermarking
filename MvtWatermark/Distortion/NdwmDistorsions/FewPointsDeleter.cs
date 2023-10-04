using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using System;

namespace Distortion.NdwmDistorsions;
public class FewPointsDeleter: IDistortion
{
    private int _numberOfPointsToDelete;
    public FewPointsDeleter(int numberOfPointsToDelete) // Пока что удаляется только одна точка
    {
        if (numberOfPointsToDelete < 0)
        {
            throw new ArgumentOutOfRangeException("numberOfPointsToDelete should not be less than 0");
        }
        _numberOfPointsToDelete = numberOfPointsToDelete;
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

                    if (feature.Geometry is LineString)
                    {
                        //copyFeature.Geometry = DeleteFewDotsLineString(feature.Geometry);
                        copyFeature.Geometry = NewDeleteFewDotsLineString(feature.Geometry, _numberOfPointsToDelete);
                    }
                    else if (feature.Geometry is MultiLineString)
                    {
                        //copyFeature.Geometry = DeleteFewDotsMultiLineString(feature.Geometry);
                        copyFeature.Geometry = NewDeleteFewDotsMultiLineString(feature.Geometry);
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

    private Geometry DeleteFewDotsLineString(Geometry geometry)
    {
        if (geometry.Coordinates.Length <= 2)
            return (LineString)geometry;

        //var rand = new Random(_numberOfPointsToDelete);
        var rand = new Random();
        var dotToDeleteIndex = rand.Next(geometry.Coordinates.Length);
        var coordinates = new Coordinate[geometry.Coordinates.Length - 1]; // Вместо 1 должно будет быть _numberOfPointsToDelete

        for (var i = 0; i < dotToDeleteIndex; i++)
        {
            coordinates[i] = geometry.Coordinates[i];
        }
        for (var i = dotToDeleteIndex; i < geometry.Coordinates.Length - 1; i++)
        {
            coordinates[i] = geometry.Coordinates[i + 1];
        }

        return new LineString(coordinates);
    }

    private Geometry NewDeleteFewDotsLineString(Geometry geometry, int numberOfPointsToDelete)
    {
        if (geometry.Coordinates.Length <= numberOfPointsToDelete + 2 || numberOfPointsToDelete < 1)
            return (LineString)geometry;

        //var occupiedIndices = new List<bool>(geometry.Coordinates.Length);

        var rand = new Random(25);

        var dotsToDeleteIndices = new List<int>(numberOfPointsToDelete);
        var occupiedIndices = new bool[geometry.Coordinates.Length];

        for (var i = 0; i < numberOfPointsToDelete; i++)
        {
            int dotToDeleteIndex;
            do
            {
                dotToDeleteIndex = rand.Next(geometry.Coordinates.Length);
            } while (occupiedIndices[dotToDeleteIndex] == true);
            dotsToDeleteIndices.Add(dotToDeleteIndex);
            occupiedIndices[dotToDeleteIndex] = true;
        }
        dotsToDeleteIndices.Sort();

        var coordinates = new Coordinate[geometry.Coordinates.Length - numberOfPointsToDelete];

        /*for (var i = 0; i < dotsToDeleteIndices[0]; i++)
        {
            coordinates[i] = geometry.Coordinates[i];
        }
        for (var i = dotsToDeleteIndices[0]; i < (dotsToDeleteIndices.Count > 1? dotsToDeleteIndices[1] geometry.Coordinates.Length - 1); i++)
        {
            coordinates[i] = geometry.Coordinates[i + 1];
        }*/

        for (var i = 0; i < dotsToDeleteIndices[0]; i++)
        {
            coordinates[i] = geometry.Coordinates[i];
        }

        for (var j = 1; j < dotsToDeleteIndices.Count; j++)
        {
            for (var i = dotsToDeleteIndices[j - 1] - j + 1; i < dotsToDeleteIndices[j] - j; i++) // dotsToDeleteIndices[j] - j ???
            {
                coordinates[i] = geometry.Coordinates[i + j]; // index out of bounds
                // ! если мы, например, хотим удалить точки 2, 5 и 12 из лайнстринга длиной в 13,
                // то удаляемые точки, учитывая, что удаляем мы их последовательно, в реальности будут такими: 2, 4, 10
            }
        }

        for (var i = dotsToDeleteIndices[numberOfPointsToDelete - 1] - numberOfPointsToDelete + 1; i < coordinates.Length; i++)
        {
            coordinates[i] = geometry.Coordinates[i + numberOfPointsToDelete];
        }

        return new LineString(coordinates);
    }

    private Geometry NewDeleteFewDotsMultiLineString(Geometry geometry)
    {
        if (_numberOfPointsToDelete < 1)
            return (MultiLineString)geometry;

        var lineStringList = ((MultiLineString)geometry).Cast<LineString>().ToList();

        var rand = new Random(25);
        var upperBorder = _numberOfPointsToDelete <= lineStringList.Count ? _numberOfPointsToDelete : lineStringList.Count;
        var chosenLineStringsCount = rand.Next(1, upperBorder + 1); // точек может быть больше, чем лайнстрингов
        var lineStringsToChangeIndices = new List<int>(chosenLineStringsCount);
        var occupiedLineStringsIndices = new bool[lineStringList.Count];

        for (var i = 0; i < chosenLineStringsCount; i++)
        {
            int lineStringToChangeIndex;
            do
            {
                lineStringToChangeIndex = rand.Next(chosenLineStringsCount);
            } while (occupiedLineStringsIndices[lineStringToChangeIndex] == true);
            lineStringsToChangeIndices.Add(lineStringToChangeIndex);
            occupiedLineStringsIndices[lineStringToChangeIndex] = true;
        }
        lineStringsToChangeIndices.Sort();

        //var numberOfPointsToDelete = _numberOfPointsToDelete;

        var eachLineStringDotsNumber = _numberOfPointsToDelete/chosenLineStringsCount;
        var dotsRemainder = _numberOfPointsToDelete%chosenLineStringsCount;
        var lineStringWithRemainderIndex = rand.Next(chosenLineStringsCount);

        foreach(var lineStringindex in lineStringsToChangeIndices)
        {
            if (lineStringindex == lineStringWithRemainderIndex)
            {
                lineStringList[lineStringindex] = (LineString)NewDeleteFewDotsLineString(lineStringList[lineStringindex], eachLineStringDotsNumber + dotsRemainder);
            }
            else
            {
                lineStringList[lineStringindex] = (LineString)NewDeleteFewDotsLineString(lineStringList[lineStringindex], eachLineStringDotsNumber);
            }
        }

        /*foreach(var lineStringindex in lineStringsToChangeIndices)
        {
            do
            {

            } while (numberOfPointsToDelete/);
        }*/

        //lineStringList[lineStringToChangeIndex] = (LineString)DeleteFewDotsLineString(lineStringList[lineStringToChangeIndex]);

        return new MultiLineString(lineStringList.ToArray());
    }

    private Geometry DeleteFewDotsMultiLineString(Geometry geometry)
    {
        var lineStringList = ((MultiLineString)geometry).Cast<LineString>().ToList();

        //var rand = new Random(lineStringList.Count + _numberOfPointsToDelete);
        var rand = new Random();
        var lineStringToChangeIndex = rand.Next(lineStringList.Count);

        lineStringList[lineStringToChangeIndex] = (LineString)DeleteFewDotsLineString(lineStringList[lineStringToChangeIndex]);

        return new MultiLineString(lineStringList.ToArray());
    }
}
