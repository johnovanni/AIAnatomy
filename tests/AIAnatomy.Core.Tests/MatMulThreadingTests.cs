using AIAnatomy.Core;

namespace AIAnatomy.Core.Tests;

public class MatMulThreadingTests
{
    private static Tensor Rand(int seed, params int[] shape) => Tensor.RandomNormal(new SeededRandom(seed), 1.0, shape);

    /// <summary>The plain sequential triple loop, summing in the same order as the engine promises to.</summary>
    private static double[] Naive(double[] a, double[] b, int n, int k, int m)
    {
        var c = new double[n * m];
        for (int i = 0; i < n; i++)
            for (int p = 0; p < k; p++)
                for (int j = 0; j < m; j++) c[i * m + j] += a[i * k + p] * b[p * m + j];
        return c;
    }

    [Theory]
    [InlineData(3, 4, 5)]       // small: runs on one thread
    [InlineData(40, 64, 100)]   // 256,000 products: runs in parallel
    public void MatMul_ForwardAndBackward_AreBitIdenticalToSequentialSums(int n, int k, int m)
    {
        var a = Rand(1, n, k).AsParameter();
        var b = Rand(2, k, m).AsParameter();
        var c = Ops.MatMul(a, b);
        Assert.Equal(Naive(a.Data, b.Data, n, k, m), c.Data);

        var weights = Rand(3, n, m);
        Ops.Sum(Ops.Mul(c, weights)).Backward();

        var expectedA = new double[n * k];
        var expectedB = new double[k * m];
        for (int i = 0; i < n; i++)
            for (int p = 0; p < k; p++)
            {
                double sum = 0;
                for (int j = 0; j < m; j++) sum += weights.Data[i * m + j] * b.Data[p * m + j];
                expectedA[i * k + p] = sum;
            }
        for (int p = 0; p < k; p++)
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++) expectedB[p * m + j] += a.Data[i * k + p] * weights.Data[i * m + j];
        Assert.Equal(expectedA, a.Grad);
        Assert.Equal(expectedB, b.Grad);
    }
}
