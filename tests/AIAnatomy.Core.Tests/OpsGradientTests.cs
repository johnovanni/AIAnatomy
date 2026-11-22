using AIAnatomy.Core;

namespace AIAnatomy.Core.Tests;

public class OpsGradientTests
{
    private static Tensor Rand(int seed, params int[] shape) => Tensor.RandomNormal(new SeededRandom(seed), 1.0, shape);

    /// <summary>Reduces an op's output to a scalar with fixed random weights, so every output element gets a different gradient.</summary>
    private static Func<Tensor[], Tensor> Weighted(Func<Tensor[], Tensor> op, Tensor[] probe)
    {
        var weights = Rand(99, op(probe).Shape);
        return inputs => Ops.Sum(Ops.Mul(op(inputs), weights));
    }

    public static IEnumerable<object[]> Cases() =>
        new[]
        {
            "Add", "AddBroadcast", "Sub", "SubBroadcast", "Mul", "MulBroadcast", "Scale", "MatMul",
            "Transpose", "Sum", "Mean", "SliceColumns", "ConcatColumns", "Rows",
        }.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Gradient_MatchesFiniteDifferences(string name)
    {
        var a = Rand(1, 3, 4);
        var same = Rand(2, 3, 4);
        var bias = Rand(3, 4);
        var b = Rand(4, 4, 2);
        var table = Rand(5, 5, 4);
        var ids = new[] { 4, 0, 4 };

        (Func<Tensor[], Tensor> Op, Tensor[] Inputs) c = name switch
        {
            "Add" => (x => Ops.Add(x[0], x[1]), new[] { a, same }),
            "AddBroadcast" => (x => Ops.Add(x[0], x[1]), new[] { a, bias }),
            "Sub" => (x => Ops.Sub(x[0], x[1]), new[] { a, same }),
            "SubBroadcast" => (x => Ops.Sub(x[0], x[1]), new[] { a, bias }),
            "Mul" => (x => Ops.Mul(x[0], x[1]), new[] { a, same }),
            "MulBroadcast" => (x => Ops.Mul(x[0], x[1]), new[] { a, bias }),
            "Scale" => (x => Ops.Scale(x[0], -2.5), new[] { a }),
            "MatMul" => (x => Ops.MatMul(x[0], x[1]), new[] { a, b }),
            "Transpose" => (x => Ops.Transpose(x[0]), new[] { a }),
            "Sum" => (x => Ops.Sum(Ops.Mul(x[0], x[0])), new[] { a }),
            "Mean" => (x => Ops.Mean(Ops.Mul(x[0], x[0])), new[] { a }),
            "SliceColumns" => (x => Ops.SliceColumns(x[0], 1, 2), new[] { a }),
            "ConcatColumns" => (x => Ops.ConcatColumns(new[] { x[0], x[1] }), new[] { a, same }),
            "Rows" => (x => Ops.Rows(x[0], ids), new[] { table }),
            _ => throw new ArgumentException(name),
        };

        var fn = c.Op(c.Inputs).Size == 1 ? c.Op : Weighted(c.Op, c.Inputs);
        Assert.True(GradCheck.MaxRelativeError(fn, c.Inputs) < 1e-6);
    }

    [Fact]
    public void MatMul_KnownValues()
    {
        var a = Tensor.FromArray(new double[] { 1, 2, 3, 4 }, 2, 2);
        var b = Tensor.FromArray(new double[] { 5, 6 }, 2, 1);
        var c = Ops.MatMul(a, b);
        Assert.Equal(new[] { 2, 1 }, c.Shape);
        Assert.Equal(new double[] { 17, 39 }, c.Data);
    }

    [Fact]
    public void Rows_RepeatedId_AccumulatesGradient()
    {
        var table = Rand(1, 3, 4).AsParameter();
        Ops.Sum(Ops.Rows(table, new[] { 1, 1 })).Backward();
        Assert.All(table.Grad!.Skip(4).Take(4), g => Assert.Equal(2.0, g));
        Assert.All(table.Grad!.Take(4), g => Assert.Equal(0.0, g));
        Assert.All(table.Grad!.Skip(8), g => Assert.Equal(0.0, g));
    }

    [Fact]
    public void Add_BroadcastsBiasOverRows()
    {
        var a = Tensor.FromArray(new double[] { 1, 2, 3, 4, 5, 6 }, 2, 3);
        var bias = Tensor.FromArray(new double[] { 10, 20, 30 }, 3);
        var c = Ops.Add(a, bias);
        Assert.Equal(new double[] { 11, 22, 33, 14, 25, 36 }, c.Data);
        Assert.Equal(new[] { 2, 3 }, c.Shape);
    }

    [Fact]
    public void Backward_TwiceWithoutZeroGrad_Accumulates()
    {
        var x = Tensor.FromArray(new double[] { 1, 1, 1 }, 3).AsParameter();
        var y = Ops.Sum(x);
        y.Backward();
        y.Backward();
        Assert.All(x.Grad!, g => Assert.Equal(2.0, g));
    }

    [Fact]
    public void Backward_DiamondGraph_AccumulatesBothPaths()
    {
        var x = Tensor.FromArray(new double[] { 1, 2, 3 }, 3).AsParameter();
        var y = Ops.Sum(Ops.Add(Ops.Scale(x, 2), Ops.Scale(x, 3)));
        y.Backward();
        Assert.All(x.Grad!, g => Assert.Equal(5.0, g));
    }

    [Fact]
    public void Backward_SameTensorTwice_MulXX()
    {
        var x = Tensor.FromArray(new double[] { 1, 2, 3 }, 3).AsParameter();
        Ops.Sum(Ops.Mul(x, x)).Backward();
        Assert.Equal(new double[] { 2, 4, 6 }, x.Grad);
    }

    [Fact]
    public void Constants_GetNoGradientBuffer()
    {
        var x = Tensor.FromArray(new double[] { 1, 2 }, 2).AsParameter();
        var k = Tensor.FromArray(new double[] { 3, 4 }, 2);
        Ops.Sum(Ops.Mul(x, k)).Backward();
        Assert.Equal(new double[] { 3, 4 }, x.Grad);
        Assert.Null(k.Grad);
    }
}
