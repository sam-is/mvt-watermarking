using NetTopologySuite.Geometries;

namespace MvtWatermark.QimMvtWatermark;
public static class GeneratorBoundsPolygon
{
    /// <summary>
    /// Generate envelope for one of M^M squres. Needed for selection geometry that inside specific square
    /// </summary>
    /// <param name="envelopeTile">Envelope of current tile in meters</param>
    /// <param name="m">M parameter of algorithm</param>
    /// <param name="i">X index of square</param>
    /// <param name="j">Y index of square</param>
    /// <returns></returns>
    public static Polygon Get(Envelope envelopeTile, double m, int i, int j)
    {
        var sizeMSquare = envelopeTile.Height / m;
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
