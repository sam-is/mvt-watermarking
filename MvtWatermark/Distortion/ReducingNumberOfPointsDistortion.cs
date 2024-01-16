using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;

namespace Distortion;
public class ReducingNumberOfPointsDistortion(double relativeCount, bool isLttb) : IDistortion
{
    public static Coordinate[] LargestTriangleThreeBuckets(Coordinate[] data, int count)
    {
        var length = data.Length;
        if (count >= length || count == 0)
            return data;

        var res = new Coordinate[count];

        var every = (double)(length - 2) / (count - 2);

        var a = 0;
        var maxAreaPoint = new Coordinate(0, 0);
        var nextA = 0;

        res[0] = data[a];

        for (var i = 0; i < count - 2; i++)
        {
            double avgX = 0;
            double avgY = 0;
            var avgRangeStart = (int)(Math.Floor((i + 1) * every) + 1);
            var avgRangeEnd = (int)(Math.Floor((i + 2) * every) + 1);
            avgRangeEnd = avgRangeEnd < length ? avgRangeEnd : length;

            var avgRangeLength = avgRangeEnd - avgRangeStart;

            for (; avgRangeStart < avgRangeEnd; avgRangeStart++)
            {
                avgX += data[avgRangeStart].X;
                avgY += data[avgRangeStart].Y;
            }
            avgX /= avgRangeLength;

            avgY /= avgRangeLength;

            var rangeOffs = (int)(Math.Floor((i + 0) * every) + 1);
            var rangeTo = (int)(Math.Floor((i + 1) * every) + 1);

            double maxArea = -1;

            for (; rangeOffs < rangeTo; rangeOffs++)
            {
                var area = Math.Abs((data[a].X - avgX) * (data[rangeOffs].Y - data[a].Y) - (data[a].X - data[rangeOffs].X) * (avgY - data[a].Y)) * 0.5;
                if (area > maxArea)
                {
                    maxArea = area;
                    maxAreaPoint = data[rangeOffs];
                    nextA = rangeOffs;
                }
            }

            res[i + 1] = maxAreaPoint;
            a = nextA;
        }

        res[count - 1] = data[length - 1];

        return res;
    }

    private static double Distance(Coordinate point, Coordinate start, Coordinate end)
    {
        var a = start.Y - end.Y;
        var b = end.X - start.X;
        var c = (start.X * end.Y - end.X * start.Y);
        return Math.Abs(a * point.X + b * point.Y + c) / Math.Sqrt(a * a + b * b);
    }

    public static Coordinate[] RamerDouglasPecker(Coordinate[] data, double eps)
    {
        var max = 0.0;
        var index = 0;

        for (var i = 1; i < data.Length - 2; i++)
        {
            var distance = Distance(data[i], data[0], data[^1]);
            if (distance > max)
            {
                index = i;
                max = distance;
            }
        }

        Coordinate[] res;

        if (max > eps)
        {
            var leftCoordinates = new Coordinate[index + 1];
            var rightCoordinates = new Coordinate[data.Length - index];
            for (var i = 0; i < data.Length; i++)
            {
                if (i <= index)
                    leftCoordinates[i] = data[i];
                if (i >= index)
                    rightCoordinates[i - index] = data[i];
            }
            var left = RamerDouglasPecker(leftCoordinates, eps);
            var right = RamerDouglasPecker(rightCoordinates, eps);

            res = new Coordinate[left.Length + right.Length - 1];

            for (var i = 0; i < left.Length - 1; i++)
                res[i] = left[i];

            for (var i = 0; i < right.Length; i++)
                res[left.Length + i - 1] = right[i];
        }
        else
        {
            res = [data[0], data[^1]];
        }

        return res;
    }

    public VectorTileTree Distort(VectorTileTree tiles)
    {
        var copyTileTree = new VectorTileTree();

        foreach (var tileId in tiles)
        {
            var copyTile = new VectorTile { TileId = tileId };

            var t = new NetTopologySuite.IO.VectorTiles.Tiles.Tile(tileId);
            var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);

            foreach (var layer in tiles[tileId].Layers)
            {
                var l = new Layer { Name = layer.Name };
                foreach (var feature in layer.Features)
                {

                    switch (feature.Geometry.GeometryType)
                    {
                        case "Point":
                            l.Features.Add(new Feature(feature.Geometry.Copy(), feature.Attributes));
                            break;
                        case "LineString":
                            Coordinate[] coordinates;
                            if (isLttb)
                            {
                                var count = (int)Math.Ceiling(feature.Geometry.NumPoints * relativeCount);

                                if (count < 2)
                                    count = 2;

                                coordinates = LargestTriangleThreeBuckets(feature.Geometry.Coordinates, count);
                            }
                            else
                            {
                                var distance = envelopeTile.Height * relativeCount;
                                coordinates = RamerDouglasPecker(feature.Geometry.Coordinates, distance);
                            }

                            l.Features.Add(new Feature(new LineString(coordinates), feature.Attributes));
                            break;
                        case "Polygon":
                            if (isLttb)
                            {
                                var count = (int)Math.Ceiling(feature.Geometry.NumPoints * relativeCount);

                                if (count < 3)
                                    count = 3;

                                coordinates = LargestTriangleThreeBuckets(feature.Geometry.Coordinates, count);
                            }
                            else
                            {
                                var distance = envelopeTile.Height * relativeCount;
                                coordinates = RamerDouglasPecker(feature.Geometry.Coordinates, distance);
                                if (coordinates.Length < 3)
                                    coordinates = [coordinates[0], new((coordinates[0].X + coordinates[^1].X) / 2, (coordinates[0].Y + coordinates[^1].Y) / 2), coordinates[^1]];
                            }

                            coordinates[^1] = coordinates[0];
                            l.Features.Add(new Feature(new Polygon(new LinearRing(coordinates)), feature.Attributes));
                            break;
                        case "MultiPoint":
                            l.Features.Add(new Feature(feature.Geometry.Copy(), feature.Attributes));
                            break;
                        case "MultiLineString":
                            var multiLineString = feature.Geometry as MultiLineString;
                            var lineStrings = new LineString[multiLineString!.NumGeometries];
                            for (var i = 0; i < multiLineString!.NumGeometries; i++)
                            {
                                var lineString = multiLineString!.Geometries[i];
                                if (isLttb)
                                {
                                    var count = (int)Math.Ceiling(lineString.NumPoints * relativeCount);

                                    if (count < 2)
                                        count = 2;

                                    coordinates = LargestTriangleThreeBuckets(lineString.Coordinates, count);
                                }
                                else
                                {
                                    var distance = envelopeTile.Height * relativeCount;
                                    coordinates = RamerDouglasPecker(lineString.Coordinates, distance);
                                }
                                lineStrings[i] = new LineString(coordinates);
                            }
                            l.Features.Add(new Feature(new MultiLineString(lineStrings), feature.Attributes));
                            break;
                        case "MultiPolygon":
                            var multiPolygon = feature.Geometry as MultiPolygon;
                            var polygons = new Polygon[multiPolygon!.NumGeometries];
                            for (var i = 0; i < multiPolygon!.NumGeometries; i++)
                            {
                                var polygon = multiPolygon!.Geometries[i];
                                if (isLttb)
                                {
                                    var count = (int)Math.Ceiling(polygon.NumPoints * relativeCount);

                                    if (count < 3)
                                        count = 3;

                                    coordinates = LargestTriangleThreeBuckets(polygon.Coordinates, count);
                                }
                                else
                                {
                                    var distance = envelopeTile.Height * relativeCount;
                                    coordinates = RamerDouglasPecker(polygon.Coordinates, distance);

                                    if (coordinates.Length < 3)
                                        coordinates = [coordinates[0], new((coordinates[0].X + coordinates[^1].X) / 2, (coordinates[0].Y + coordinates[^1].Y) / 2), coordinates[^1]];
                                }

                                coordinates[^1] = coordinates[0];
                                polygons[i] = new Polygon(new LinearRing(coordinates));
                            }
                            l.Features.Add(new Feature(new MultiPolygon(polygons), feature.Attributes));
                            break;
                    }
                }

                copyTile.Layers.Add(l);
            }

            copyTileTree[tileId] = copyTile;
        }

        return copyTileTree;
    }
}
