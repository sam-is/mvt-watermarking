using System;

namespace MvtWatermark.QimMvtWatermark;

/// <summary>
/// Mode for embed and extracting methods
/// </summary>
public enum Mode
{
    /// <summary>
    /// Each tile contains a message part and different tiles may contain the same. 
    /// When extracting part of the message is restored by majority voting on all tiles, that will contain the same part of the message.
    /// </summary>
    WithTilesMajorityVote = 0,
    /// <summary>
    /// If during embedding it was not possible to embed part of the message in a tile, then this part will be embedded in the next tile
    /// </summary>
    WithCheck,
    /// <summary>
    /// The original message is embedded in a row in the tiles. 
    /// If it was not possible to embed a part of the message in the tile, then it will not be embedded. 
    /// If the message ends before the tiles, it is repeated again.
    /// </summary>
    Repeat
}


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
    /// <summary>
    /// Mode for embed and extracting methods
    /// </summary>
    public Mode Mode { get; set; }
    /// <summary>
    /// If select <see cref="Mode.WithTilesMajorityVote"/> in <see cref="Mode"/> this parametrs needed for correctly extraction
    /// </summary>
    public int? MessageLength { get; set; }
    /// <summary>
    /// Optimizes genereting quantization matrix.
    /// Parameter <c>countMaps</c> in constructor setup maximum quantization matrices that will be created.
    /// </summary>
    public Maps Maps { get; set; }

    public QimMvtWatermarkOptions(double k, double t2, int t1, int extent, int distance,
                                  int? nb, int? r, int? m, int countMaps = 10, bool isGeneralExtractionMethod = false,
                                  Mode mode = Mode.WithTilesMajorityVote, int? messageLength = null)
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
            throw new ArgumentNullException(nb == null ? nameof(nb) : nameof(r), "Only one of nb, r, m parameters can be null");

        IsGeneralExtractionMethod = isGeneralExtractionMethod;
        Mode = mode;
        MessageLength = messageLength;
        Maps = new Maps(countMaps);
    }

    public QimMvtWatermarkOptions() : this(0.9, 0.2, 5, 2048, 2, 8, 8, null) { }

    public QimMvtWatermarkOptions(QimMvtWatermarkOptions options) : this(options.T2, options.Delta2, options.T1, options.Delta1, options.Extent, options.Distance, options.M,
                                                                         options.R, options.Nb, options.Maps, options.IsGeneralExtractionMethod, options.Mode, options.MessageLength)
    { }

    public QimMvtWatermarkOptions(double t2, double delta2, int t1, int delta1, int extent, int distance, int m, int r, int nb, Maps maps, bool isGeneralExtractionMethod, Mode mode, int? messageLength)
    {
        T2 = t2;
        Delta2 = delta2;
        T1 = t1;
        Delta1 = delta1;
        Extent = extent;
        Distance = distance;
        M = m;
        R = r;
        Nb = nb;
        IsGeneralExtractionMethod = isGeneralExtractionMethod;
        Mode = mode;
        MessageLength = messageLength;
        Maps = maps;
    }
}