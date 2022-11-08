using MvtWatermark;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.IO.VectorTiles.Tiles;
using System;
using System.Collections;

namespace MvtWatermark.QimMvtWatermark
{
    public class QimMvtWatermark : IMvtWatermark
    {
        private readonly QimMvtWatermarkOptions _options;


        private int[,] GenerateWinx(int key, int sizeMessage)
        {
            var r = (int)Math.Floor((double)_options.M * _options.M / sizeMessage);
            Console.WriteLine($"r = {r}");
            var random = new Random(key);
            var winx = new int[_options.M, _options.M];

            for (int i = 0; i < _options.M; i++)
                for (int j = 0; j < _options.M; j++)
                    winx[i, j] = -1;


            for (var i = 0; i < sizeMessage; i++)
            {
                for (var j = 0; j < r; j++)
                {
                    int x;
                    int y;
                    do
                    {
                        x = random.Next() % _options.M;
                        y = random.Next() % _options.M;
                    } while (winx[x, y] != -1);

                    winx[x, y] = i;
                }
            }

            return winx;
        }
        private VectorTile Embed(VectorTile tile, ulong id, int key, BitArray message)
        {
            var t = new Tile(id);
            var envelopeTile = CoordinateConverter.TileBounds(t.X, t.Y, t.Zoom);
            var a = envelopeTile.Height / _options.M;

            var winx = GenerateWinx(key);
            for (var i = 0; i < _options.M; i++)
                for (var j = 0; j < _options.M; j++)
                {

                    var index = _winx[i, j];
                    if (index == -1)
                        continue;
                    var value = Convert.ToInt32(bites[index]);

                    var polygon = new Polygon(
                        new LinearRing(
                            new Coordinate[]
                            {
                                    new(envelopeTile.MinX + _a * i, envelopeTile.MinY + _a * j),
                                    new(envelopeTile.MinX + _a * i, envelopeTile.MinY + _a * (j + 1)),
                                    new(envelopeTile.MinX + _a * (i + 1), envelopeTile.MinY + _a * (j + 1)),
                                    new(envelopeTile.MinX + _a * (i + 1), envelopeTile.MinY + _a * j),
                                    new(envelopeTile.MinX + _a * i, envelopeTile.MinY + _a * j)
                            }
                    )
                    );



                    var stat = Statistics(polygon, out int s0, out int s1);
                    if (stat == -1)
                    {
                        Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, index: {index}, not embeded");
                        continue;
                    }

                    Console.WriteLine($"i: {i}, j:{j}, s0: {s0}, s1: {s1}, stat: {stat}, value: {value}, index: {index}");
                    if (stat >= _options.T2 + _options.Delta2)
                    {
                        if (s1 - s0 > 0 && value == 1)
                        {
                            Console.WriteLine($"yes");
                            continue;
                        }
                        if (s0 - s1 > 0 && value == 0)
                        {
                            Console.WriteLine($"yes");
                            continue;
                        }
                        Console.WriteLine($"no");
                    }

                    if (value == 1)
                    {
                        var countAdded = (int)Math.Ceiling(((s0 + s1) * (_options.T2 + _options.Delta2) + s0 - s1) / 2);
                        Console.WriteLine($"needed to 1: {countAdded}");
                        ChangeCoordinate(1, countAdded, s0, polygon);
                    }

                    if (value == 0)
                    {
                        var countAdded = (int)Math.Ceiling(((s0 + s1) * (_options.T2 + _options.Delta2) + s1 - s0) / 2);
                        Console.WriteLine($"needed to 0: {countAdded}");
                        ChangeCoordinate(0, countAdded, s1, polygon);
                    }
                }
            return tile;
        }
        public QimMvtWatermark(QimMvtWatermarkOptions options)
        {
            _options = options;
        }

        public VectorTileTree Embed(VectorTileTree tiles, int key, BitArray message)
        {
            throw new NotImplementedException();
        }

        public BitArray Extract(VectorTileTree tiles, int key)
        {
            throw new NotImplementedException();
        }
    }
}