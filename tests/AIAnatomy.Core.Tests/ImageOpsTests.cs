using AIAnatomy.Core;

namespace AIAnatomy.Core.Tests;

public class ImageOpsTests
{
    private static Tensor Rand(int seed, params int[] shape) => Tensor.RandomNormal(new SeededRandom(seed), 1.0, shape);

    private static Func<Tensor[], Tensor> Weighted(Func<Tensor[], Tensor> op, Tensor[] probe)
    {
        var weights = Rand(99, op(probe).Shape);
        return inputs => Ops.Sum(Ops.Mul(op(inputs), weights));
    }

    private static void Check(Func<Tensor[], Tensor> op, Tensor[] inputs) =>
        Assert.True(GradCheck.MaxRelativeError(Weighted(op, inputs), inputs) < 1e-6);

    [Fact] public void Tanh_Gradient() => Check(x => Ops.Tanh(x[0]), new[] { Rand(1, 3, 4) });

    [Fact] public void Reshape_Gradient() => Check(x => Ops.Reshape(x[0], 3, 4), new[] { Rand(2, 2, 6) });

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void Conv2d_Gradient(int padding) =>
        Check(x => Ops.Conv2d(x[0], x[1], x[2], 3, padding), new[] { Rand(3, 2, 2, 5, 5), Rand(4, 3, 18), Rand(5, 3) });

    [Fact]
    public void MaxPool2d_Gradient()
    {
        var data = Enumerable.Range(0, 32).Select(i => (double)((i * 7) % 32) * 0.37 - 5).ToArray();
        Check(x => Ops.MaxPool2d(x[0], 2), new[] { Tensor.FromArray(data, 1, 2, 4, 4) });
    }

    [Fact]
    public void Tanh_KnownValues()
    {
        var y = Ops.Tanh(Tensor.FromArray(new[] { 0.0, 1.0, -1.0 }, 3));
        Assert.Equal(0.0, y.Data[0], 12);
        Assert.Equal(Math.Tanh(1.0), y.Data[1], 12);
        Assert.Equal(-Math.Tanh(1.0), y.Data[2], 12);
    }

    [Fact]
    public void Reshape_KeepsDataOrder()
    {
        var y = Ops.Reshape(Tensor.FromArray(new[] { 1.0, 2, 3, 4, 5, 6 }, 2, 3), 3, 2);
        Assert.Equal(new[] { 3, 2 }, y.Shape);
        Assert.Equal(new[] { 1.0, 2, 3, 4, 5, 6 }, y.Data);
    }

    private static readonly double[] Image = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

    private static Tensor ConvOnes(int padding, double bias) =>
        Ops.Conv2d(Tensor.FromArray(Image, 1, 1, 3, 3), Tensor.FromArray(Enumerable.Repeat(1.0, 9).ToArray(), 1, 9),
            Tensor.FromArray(new[] { bias }, 1), 3, padding);

    [Fact]
    public void Conv2d_Padding1_HandWorked()
    {
        var y = ConvOnes(1, 0);
        Assert.Equal(new[] { 1, 1, 3, 3 }, y.Shape);
        Assert.Equal(new double[] { 12, 21, 16, 27, 45, 33, 24, 39, 28 }, y.Data);
    }

    [Fact]
    public void Conv2d_Padding0_HandWorked()
    {
        var y = ConvOnes(0, 0);
        Assert.Equal(new[] { 1, 1, 1, 1 }, y.Shape);
        Assert.Equal(new double[] { 45 }, y.Data);
    }

    [Fact]
    public void Conv2d_BiasAddsEverywhere() =>
        Assert.Equal(new double[] { 13, 22, 17, 28, 46, 34, 25, 40, 29 }, ConvOnes(1, 1).Data);

    [Fact]
    public void MaxPool2d_HandWorked()
    {
        var y = Ops.MaxPool2d(Tensor.FromArray(new double[] { 1, 3, 2, 0, 4, 2, 1, 5, 0, 1, 7, 6, 2, 3, 8, 1 }, 1, 1, 4, 4), 2);
        Assert.Equal(new[] { 1, 1, 2, 2 }, y.Shape);
        Assert.Equal(new double[] { 4, 5, 3, 8 }, y.Data);
    }

    [Fact]
    public void MaxPool2d_TieGoesToFirstInRowMajorOrder()
    {
        var x = Tensor.FromArray(new double[] { 5, 5, 5, 5 }, 1, 1, 2, 2).AsParameter();
        Ops.MaxPool2d(x, 2).Backward();
        Assert.Equal(new double[] { 1, 0, 0, 0 }, x.Grad);
    }

    [Fact]
    public void Conv2d_WrongWeightShape_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Ops.Conv2d(Tensor.Zeros(1, 1, 3, 3), Tensor.Zeros(8, 4), Tensor.Zeros(8), 3, 1));
        Assert.Equal("conv2d: weights must be [F, C*k*k] = [?, 9] but got [8, 4].", ex.Message);
    }

    [Fact]
    public void Conv2d_BiasMismatch_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Ops.Conv2d(Tensor.Zeros(1, 1, 3, 3), Tensor.Zeros(2, 9), Tensor.Zeros(3), 3, 1));
        Assert.StartsWith("conv2d: bias", ex.Message);
    }

    [Fact]
    public void MaxPool2d_OddSize_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Ops.MaxPool2d(Tensor.Zeros(1, 1, 3, 4), 2));
        Assert.StartsWith("maxpool2d:", ex.Message);
    }

    [Fact]
    public void Reshape_ChangingSize_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Ops.Reshape(Tensor.Zeros(2, 3), 4, 2));
        Assert.StartsWith("reshape:", ex.Message);
    }

    [Fact]
    public void Conv2d_IsBitIdenticalRunToRun()
    {
        var x = Rand(1, 16, 8, 14, 14);
        var w = Rand(2, 6, 8 * 9);
        var b = Rand(3, 6);
        var first = Ops.Conv2d(x, w, b, 3, 1).Data;
        var second = Ops.Conv2d(x, w, b, 3, 1).Data;
        Assert.True(first.AsSpan().SequenceEqual(second));
    }
}
