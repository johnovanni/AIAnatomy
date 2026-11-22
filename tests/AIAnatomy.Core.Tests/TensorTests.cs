using AIAnatomy.Core;

namespace AIAnatomy.Core.Tests;

public class TensorTests
{
    [Fact]
    public void FromArray_RowMajorIndexing()
    {
        var t = Tensor.FromArray(new double[] { 1, 2, 3, 4, 5, 6 }, 2, 3);
        Assert.Equal(4, t[1, 0]);
        Assert.Equal(6, t[1, 2]);
    }

    [Fact]
    public void Zeros_HasSizeAndZeros()
    {
        var t = Tensor.Zeros(3, 4);
        Assert.Equal(12, t.Size);
        Assert.All(t.Data, v => Assert.Equal(0, v));
    }

    [Fact]
    public void FromArray_WrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => Tensor.FromArray(new double[] { 1, 2, 3 }, 2, 2));
    }

    [Fact]
    public void SeededRandom_SameSeedSameSequence()
    {
        var a = new SeededRandom(7);
        var b = new SeededRandom(7);
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextDouble(), b.NextDouble());
    }

    [Fact]
    public void RandomNormal_HasRoughStd()
    {
        var t = Tensor.RandomNormal(new SeededRandom(1), 0.02, 10000);
        double mean = t.Data.Average();
        double std = Math.Sqrt(t.Data.Sum(v => (v - mean) * (v - mean)) / t.Size);
        Assert.InRange(std, 0.0185, 0.0215);
    }

    [Fact]
    public void Backward_OnNonScalar_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Tensor.Zeros(2).Backward());
    }
}
