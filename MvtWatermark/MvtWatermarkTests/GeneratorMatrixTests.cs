using MvtWatermark.QimMvtWatermark;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MvtWatermarkTests;
public class GeneratorMatrixTests
{
    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 5, 5, 5)]
    [InlineData(2, 8, 16, 4)]
    [InlineData(3, 3, 2, 2)]
    [InlineData(4, 18, 2, 9)]
    [InlineData(5, 18, 9, 2)]
    [InlineData(6, 100, 100, 100)]
    [InlineData(7, 15, 5, 3)]
    public void GenerateRandomMatrixWithIndices(int key, int m, int nb, int r)
    {
        var matrix = GeneratorMatrix.GenerateRandomMatrixWithIndices(key, m, nb, r);

        Assert.Equal(matrix.Length, m * m);

        var dict = new Dictionary<int, int>();
        for (var i = 0; i < nb; i++)
            dict[i] = 0;
        for (var i = 0; i < m; i++)
            for (var j = 0; j < m; j++)
                if (matrix[i, j] != -1)
                    dict[matrix[i, j]] += 1;

        Assert.True(dict.Values.All(v => v == r));
    }
}
