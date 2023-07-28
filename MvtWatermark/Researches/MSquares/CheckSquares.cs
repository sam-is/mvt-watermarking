using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Researches.MSquares;
public class CheckSquares
{
    static public void Start(VectorTileTree tileTree, string path)
    {
        var valuesT1 = new int[] { 1, 2, 3, 4, 5, 8, 10, 15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100, 120, 140, 160, 180, 200, 250, 300, 400, 500, 1000, 1500, 2000 };
        var maxM = 50;
        var mean = new int[valuesT1.Length, maxM];
        var meanRelative = new double[valuesT1.Length, maxM];

        if (!Directory.Exists(Path.Combine(path, "count")))
            Directory.CreateDirectory(Path.Combine(path, "count"));

        if (!Directory.Exists(Path.Combine(path, "relative count")))
            Directory.CreateDirectory(Path.Combine(path, "relative count"));

        var locker = new object();

        Parallel.ForEach(tileTree, tileId =>
        {
            var tile = tileTree[tileId];
            var t = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile.TileId);

            var emptySquares = new List<List<int>>(maxM);

            using var writer = new StreamWriter(Path.Combine(path, "count", $"x = {t.X}, y = {t.Y}, z = {t.Zoom}.txt"));
            using var writerRelative = new StreamWriter(Path.Combine(path, "relative count", $"x = {t.X}, y = {t.Y}, z = {t.Zoom}.txt"));
            writer.Write(" t1/m  ");
            writerRelative.Write(" t1/m  ");
            for (var i = 1; i <= maxM; i++)
            {
                writer.Write($"{i,-7: ###}");
                writerRelative.Write($"{i,-7: ###}");
            }


            for (var m = 1; m <= maxM; m++)
            {
                Console.WriteLine($"{t.X} {t.Y}    {m}");
                emptySquares.Add(GetEmptySquares(tile, valuesT1, m));
            }


            for (var t1 = 0; t1 < valuesT1.Length; t1++)
            {
                writer.Write($"\n{valuesT1[t1],-7: ###}");
                writerRelative.Write($"\n{valuesT1[t1],-7: ###}");
                for (var m = 1; m <= maxM; m++)
                {
                    lock (locker)
                    {
                        mean[t1, m - 1] += emptySquares[m - 1][t1];
                        meanRelative[t1, m - 1] += (double)emptySquares[m - 1][t1] / (m * m);
                    }
                    writer.Write($"{emptySquares[m - 1][t1],-7: ###0}");
                    writerRelative.Write($"{(double)emptySquares[m - 1][t1] / (m * m),-7: 0.###}");
                }
            }
        });

        using var writerMean = new StreamWriter(Path.Combine(path, "mean.txt"));
        using var writerMeanRelative = new StreamWriter(Path.Combine(path, "mean relative.txt"));

        writerMean.Write(" t1/m  ");
        writerMeanRelative.Write(" t1/m  ");
        for (var i = 1; i <= maxM; i++)
        {
            writerMean.Write($"{i,-7: ###}");
            writerMeanRelative.Write($"{i,-7: ###}");
        }

        for (var t1 = 0; t1 < valuesT1.Length; t1++)
        {
            writerMean.Write($"\n{valuesT1[t1],-7: ###}");
            writerMeanRelative.Write($"\n{valuesT1[t1],-7: ###}");
            for (var m = 1; m <= maxM; m++)
            {
                writerMean.Write($"{(double)mean[t1, m - 1] / tileTree.Count(),-7: ###0.#}");
                writerMeanRelative.Write($"{meanRelative[t1, m - 1] / tileTree.Count(),-7: 0.###}");
            }
        }
    }

    static public List<int> GetEmptySquares(VectorTile tile, IList<int> t1, int m)
    {
        var t = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tile.TileId);
        var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);
        envelopeTile = CoordinateConverter.DegreesToMeters(envelopeTile);

        var a = envelopeTile.Height / m;

        var result = new List<int>(new int[t1.Count]);

        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < m; j++)
            {

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

                var countPoints = GetCountPoints(tile, polygon);

                for (var k = 0; k < result.Count; k++)
                    if (countPoints < t1[k])
                        result[k]++;
            }
        }
        return result;
    }

    private static int GetCountPoints(VectorTile tile, Geometry geometry)
    {
        var count = 0;

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
                        count++;
                    }
                }
            }

        return count;
    }
}
