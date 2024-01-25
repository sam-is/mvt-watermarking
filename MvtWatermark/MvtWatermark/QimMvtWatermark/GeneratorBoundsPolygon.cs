using NetTopologySuite.Geometries;
using System.Dynamic;

namespace MvtWatermark.QimMvtWatermark;
public static class GeneratorBoundsPolygon
{
    public static Polygon Get(Envelope envelopeTile, double countMSquares, int i, int j)
    {
        var sizeMSquare = envelopeTile.Height / countMSquares;
        return new Polygon(
                    new LinearRing(
                        new Coordinate[]
                        {
                                    new(envelopeTile.MinX + sizeMSquare * i, envelopeTile.MinY + sizeMSquare * j),
                                    new(envelopeTile.MinX + sizeMSquare * i, envelopeTile.MinY + sizeMSquare * (j + 1)),
                                    new(envelopeTile.MinX + sizeMSquare * (i + 1), envelopeTile.MinY + sizeMSquare * (j + 1)),
                                    new(envelopeTile.MinX + sizeMSquare * (i + 1), envelopeTile.MinY + sizeMSquare * j),
                                    new(envelopeTile.MinX + sizeMSquare * i, envelopeTile.MinY + sizeMSquare * j)
                        }
                ));
    }
}
