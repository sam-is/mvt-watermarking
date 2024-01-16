using MvtWatermark.QimMvtWatermark;
using NetTopologySuite.IO.VectorTiles;

namespace Researches.Parameters;
public class ReasearchParameters
{
    public static void Start(VectorTileTree tileTree, string path)
    {
        var options = new QimMvtWatermarkOptions(0.6, 0.3, 20, 4096, 2, 5, 20, null, false);
        var checkParameters = new CheckParameters(options);

        checkParameters.Run(tileTree, path);
    }

    public static void StartMatrix(VectorTileTree tileTree, string path)
    {
        var options = new QimMvtWatermarkOptions(0.6, 0.3, 5, 4096, 2, 4, 16, null, false);
        var matrixParametersResearch = new MatrixParametersResearch(options);

        matrixParametersResearch.Run(tileTree, path);
    }
}
