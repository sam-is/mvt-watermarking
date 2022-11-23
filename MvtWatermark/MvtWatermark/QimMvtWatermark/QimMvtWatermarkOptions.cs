using System;

namespace MvtWatermark.QimMvtWatermark;

public class QimMvtWatermarkOptions
{
    /// <summary>
    /// The value for the relative number of required points
    /// </summary>
    public double T2 { get; set; }
    /// <summary>
    /// Inaccuracy value for relative number of required points
    /// </summary>
    public double Delta2 { get; set; }
    /// <summary>
    /// The number of points in the each square from MxM, that is necessary for successful embedding
    /// </summary>
    public int T1 { get; set; }
    /// <summary>
    /// Inaccuracy value for number of required points
    /// </summary>
    public int Delta1 { get; set; }
    /// <summary>
    /// Describes the width and height of the tile in integer coordinates
    /// </summary>
    public int Extent { get; set; }
    /// <summary>
    /// The maximum four-connected distance for the opposite value in the re-quantization matrix
    /// </summary>
    public int Distance { get; set; }
    /// <summary>
    /// The length and width of the matrix containing the indexes of the embedded message
    /// </summary>
    public int M { get; set; }
    /// <summary>
    /// The number of embeddings per message bit
    /// </summary>
    public int R { get; set; }
    /// <summary>
    /// The number of embedded bits
    /// </summary>
    public int Nb { get; set; }
    /// <summary>
    /// Indicates that the extraction will take place over a series of squares
    /// </summary>
    public bool IsGeneralExtractionMethod { get; set; }

    public QimMvtWatermarkOptions(double k, double t2, int t1, int extent, int distance,
                                  int? nb, int? r, int? m, bool isGeneralExtractionMethod = false)
    {
        T2 = t2;
        Delta2 = k * T2;
        T1 = t1;
        Delta1 = (int)Math.Round(k * T1);
        Extent = extent;
        Distance = distance;

        if (nb == null && r != null && m != null)
        {
            R = (int)r!;
            M = (int)m!;
            Nb = (int)Math.Floor((double)M * M / R);
        }
        else if (r == null && nb != null && m != null)
        {
            M = (int)m!;
            Nb = (int)nb!;
            R = (int)Math.Floor((double)M * M / Nb);
        }
        else if (m == null && nb != null && r != null)
        {
            R = (int)r!;
            Nb = (int)nb!;
            M = (int)Math.Ceiling(Math.Sqrt(Nb * R));
        }
        else if (nb != null && r != null && m != null)
        {
            Nb = (int)nb!;
            R = (int)r!;
            M = (int)m!;
        }
        else
            throw new ArgumentNullException(nb==null? nameof(nb):nameof(r),"Only one of nb, r, m parameters can be null");

        IsGeneralExtractionMethod = isGeneralExtractionMethod;
    }

    public QimMvtWatermarkOptions() : this(0.5, 0.7, 15, 4096, 2, 5, 10, null) { }
}