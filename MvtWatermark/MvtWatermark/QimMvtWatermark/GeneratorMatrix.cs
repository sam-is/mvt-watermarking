using System;

namespace MvtWatermark.QimMvtWatermark;
public static class GeneratorMatrix
{
    /// <summary>
    /// Generates a matrix with embedded message indexes
    /// </summary>
    /// <param name="key">Secret key</param>
    /// <returns>Matrix with embedded message indexes</returns>
    public static int[,] GenerateRandomMatrixWithIndices(int key, int sizeMatrix, int sizeMessage, int repeatCoefficient)
    {
        if (sizeMatrix * sizeMatrix < sizeMessage * repeatCoefficient)
            throw new InvalidOperationException("sizeMessage * repeatCoefficient must be smaller than sizeMatrix^2");

        var random = new Random(key);
        var winx = new int[sizeMatrix, sizeMatrix];

        for (var i = 0; i < sizeMatrix; i++)
            for (var j = 0; j < sizeMatrix; j++)
                winx[i, j] = -1;


        for (var i = 0; i < sizeMessage; i++)
        {
            for (var j = 0; j < repeatCoefficient; j++)
            {
                int x;
                int y;
                do
                {
                    x = random.Next() % sizeMatrix;
                    y = random.Next() % sizeMatrix;
                } while (winx[x, y] != -1);

                winx[x, y] = i;
            }
        }

        return winx;
    }
}
