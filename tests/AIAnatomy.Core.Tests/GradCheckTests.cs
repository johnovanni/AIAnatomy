using AIAnatomy.Core;

namespace AIAnatomy.Core.Tests;

public class GradCheckTests
{
    /// <summary>Scale whose backward is deliberately wrong: it multiplies the gradient by 2*s instead of s.</summary>
    private static Tensor WrongScale(Tensor a, double s)
    {
        var data = a.Data.Select(v => v * s).ToArray();
        return new Tensor(data, (int[])a.Shape.Clone(), "wrong-scale", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int i = 0; i < g.Length; i++) ga[i] += g[i] * 2 * s;
        });
    }

    [Fact]
    public void GradCheck_DetectsWrongBackward()
    {
        var x = Tensor.FromArray(new[] { 1.0, 2.0, 3.0 }, 3);
        double error = GradCheck.MaxRelativeError(p => Ops.Sum(WrongScale(p[0], 3.0)), x);
        Assert.True(error > 1e-3);
    }

    [Fact]
    public void Backward_TwiceWithoutZeroGrad_ChainClearsIntermediates()
    {
        var x = Tensor.FromArray(new[] { 1.0, 1.0 }, 2).AsParameter();
        var y = Ops.Sum(Ops.Scale(x, 2));
        y.Backward();
        y.Backward();
        Assert.All(x.Grad!, g => Assert.Equal(4.0, g));
    }

    [Fact]
    public void Add_ShapeMismatch_Throws() =>
        Assert.Throws<ArgumentException>(() => Ops.Add(Tensor.Zeros(2, 3), Tensor.Zeros(3, 2)));

    [Fact]
    public void Rows_IdOutOfRange_Throws()
    {
        var table = Tensor.Zeros(3, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ops.Rows(table, new[] { 3 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ops.Rows(table, new[] { -1 }));
    }
}
