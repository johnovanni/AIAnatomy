using AIAnatomy.Core;

namespace AIAnatomy.Core.Tests;

public class NeuralOpsTests
{
    private static Tensor Rand(int seed, params int[] shape) => Tensor.RandomNormal(new SeededRandom(seed), 1.0, shape);

    private static Func<Tensor[], Tensor> Weighted(Func<Tensor[], Tensor> op, Tensor[] probe)
    {
        var weights = Rand(99, op(probe).Shape);
        return inputs => Ops.Sum(Ops.Mul(op(inputs), weights));
    }

    public static IEnumerable<object[]> Cases() =>
        new[]
        {
            "Relu", "Gelu", "SoftmaxRows", "LogSoftmaxRows", "CausalMaskSoftmax",
            "LayerNormRows", "CrossEntropy", "MeanSquaredError",
        }.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Gradient_MatchesFiniteDifferences(string name)
    {
        var a = Rand(1, 3, 4);
        var away = Tensor.FromArray(new[] { -1.3, 0.7, 2.1, -0.4, 0.9, -2.2, 0.3, 1.6, -0.8, 0.5, -1.1, 1.9 }, 3, 4);
        var square = Rand(6, 4, 4);
        var gamma = Rand(7, 4);
        var beta = Rand(8, 4);
        var other = Rand(9, 3, 4);
        var targets = new[] { 2, 0, 3 };

        (Func<Tensor[], Tensor> Op, Tensor[] Inputs) c = name switch
        {
            "Relu" => (x => Ops.Relu(x[0]), new[] { away }),
            "Gelu" => (x => Ops.Gelu(x[0]), new[] { a }),
            "SoftmaxRows" => (x => Ops.SoftmaxRows(x[0]), new[] { a }),
            "LogSoftmaxRows" => (x => Ops.LogSoftmaxRows(x[0]), new[] { a }),
            "CausalMaskSoftmax" => (x => Ops.SoftmaxRows(Ops.CausalMask(x[0])), new[] { square }),
            "LayerNormRows" => (x => Ops.LayerNormRows(x[0], x[1], x[2]), new[] { a, gamma, beta }),
            "CrossEntropy" => (x => Losses.CrossEntropy(x[0], targets), new[] { a }),
            "MeanSquaredError" => (x => Losses.MeanSquaredError(x[0], x[1]), new[] { a, other }),
            _ => throw new ArgumentException(name),
        };

        var fn = c.Op(c.Inputs).Size == 1 ? c.Op : Weighted(c.Op, c.Inputs);
        Assert.True(GradCheck.MaxRelativeError(fn, c.Inputs) < 1e-6);
    }

    [Fact]
    public void Relu_KnownValues()
    {
        var y = Ops.Relu(Tensor.FromArray(new[] { -1.0, 0.0, 2.0 }, 3));
        Assert.Equal(new[] { 0.0, 0.0, 2.0 }, y.Data);
    }

    [Fact]
    public void Gelu_KnownValues()
    {
        var y = Ops.Gelu(Tensor.FromArray(new[] { 0.0, 1.0, -1.0 }, 3));
        Assert.Equal(0.0, y.Data[0], 12);
        Assert.Equal(0.8411919906082768, y.Data[1], 9);
        Assert.Equal(-0.15880800939172324, y.Data[2], 9);
    }

    [Fact]
    public void SoftmaxRows_RowsSumToOne()
    {
        var y = Ops.SoftmaxRows(Rand(1, 5, 7));
        for (int r = 0; r < 5; r++) Assert.Equal(1.0, y.Row(r).Sum(), 12);
    }

    [Fact]
    public void SoftmaxRows_LargeInputsStable()
    {
        var y = Ops.SoftmaxRows(Tensor.FromArray(new[] { 1000.0, 1001.0 }, 1, 2));
        Assert.Equal(0.2689414213699951, y.Data[0], 12);
        Assert.Equal(0.7310585786300049, y.Data[1], 12);
        Assert.All(y.Data, v => Assert.False(double.IsNaN(v)));
    }

    [Fact]
    public void LogSoftmaxRows_LargeInputsStable()
    {
        var y = Ops.LogSoftmaxRows(Tensor.FromArray(new[] { 1000.0, 1001.0 }, 1, 2));
        Assert.Equal(Math.Log(0.2689414213699951), y.Data[0], 9);
        Assert.Equal(Math.Log(0.7310585786300049), y.Data[1], 9);
    }

    [Fact]
    public void CausalMask_SoftmaxGivesZeroAboveDiagonal()
    {
        var y = Ops.SoftmaxRows(Ops.CausalMask(Rand(1, 4, 4)));
        for (int r = 0; r < 4; r++)
        {
            for (int c = r + 1; c < 4; c++) Assert.Equal(0.0, y[r, c]);
            Assert.Equal(1.0, y.Row(r).Sum(), 12);
        }
        Assert.Equal(1.0, y[0, 0], 12);
    }

    [Fact]
    public void CausalMask_MasksWithNegativeInfinity()
    {
        var m = Ops.CausalMask(Rand(1, 3, 3));
        Assert.True(double.IsNegativeInfinity(m[0, 1]));
        Assert.True(double.IsNegativeInfinity(m[1, 2]));
        Assert.False(double.IsInfinity(m[2, 0]));
        Assert.False(double.IsInfinity(m[1, 1]));
    }

    [Fact]
    public void CausalMask_NonSquare_Throws() =>
        Assert.Throws<ArgumentException>(() => Ops.CausalMask(Rand(1, 2, 3)));

    [Fact]
    public void LayerNorm_OutputMeanZeroVarianceOne()
    {
        var x = Rand(3, 4, 16);
        var y = Ops.LayerNormRows(x, Tensor.FromArray(Enumerable.Repeat(1.0, 16).ToArray(), 16), Tensor.Zeros(16), 0);
        for (int r = 0; r < 4; r++)
        {
            var row = y.Row(r);
            double mean = row.Average();
            double variance = row.Select(v => (v - mean) * (v - mean)).Average();
            Assert.Equal(0.0, mean, 6);
            Assert.Equal(1.0, variance, 6);
        }
    }

    [Fact]
    public void CrossEntropy_UniformLogits_IsLogV()
    {
        var loss = Losses.CrossEntropy(Tensor.Zeros(3, 8), new[] { 0, 5, 7 });
        Assert.Equal(Math.Log(8), loss.Item(), 12);
    }

    [Fact]
    public void CrossEntropy_BadTargets_Throw()
    {
        var logits = Tensor.Zeros(2, 4);
        Assert.Throws<ArgumentException>(() => Losses.CrossEntropy(logits, new[] { 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Losses.CrossEntropy(logits, new[] { 0, 4 }));
    }

    [Fact]
    public void MeanSquaredError_KnownValue()
    {
        var p = Tensor.FromArray(new[] { 1.0, 2.0 }, 2);
        var t = Tensor.FromArray(new[] { 0.0, 4.0 }, 2);
        Assert.Equal(2.5, Losses.MeanSquaredError(p, t).Item(), 12);
    }

    [Fact]
    public void MeanSquaredError_ShapeMismatch_Throws() =>
        Assert.Throws<ArgumentException>(() => Losses.MeanSquaredError(Tensor.Zeros(2), Tensor.Zeros(3)));
}
