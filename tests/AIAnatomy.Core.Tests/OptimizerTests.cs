using AIAnatomy.Core;

namespace AIAnatomy.Core.Tests;

public class OptimizerTests
{
    private static Parameter Scalar(string name, double value) => new(name, Tensor.Scalar(value).AsParameter());

    [Fact]
    public void Sgd_Step_MovesAgainstGradient()
    {
        var w = Scalar("w", 3);
        var sgd = new Sgd(new[] { w }, 0.1);
        Ops.Mul(w.Value, w.Value).Backward(); // loss = w^2, gradient 6
        sgd.Step();
        Assert.Equal(2.4, w.Value.Item(), 12);
    }

    [Fact]
    public void Adam_FirstStep_IsLearningRateTimesSign()
    {
        var w = Scalar("w", 1);
        var adam = new Adam(new[] { w }, 0.01);
        Ops.Scale(w.Value, 5).Backward(); // gradient 5
        adam.Step();
        Assert.Equal(1 - 0.01 * 5 / (5 + 1e-8), w.Value.Item(), 12);
    }

    [Fact]
    public void Sgd_FitsLinearData()
    {
        var w = new Parameter("w", Tensor.Zeros(1, 1).AsParameter());
        var b = new Parameter("b", Tensor.Zeros(1).AsParameter());
        var xs = Enumerable.Range(0, 20).Select(i => -1.0 + 2.0 * i / 19).ToArray();
        var x = Tensor.FromArray(xs, 20, 1);
        var y = Tensor.FromArray(xs.Select(v => 2 * v + 1).ToArray(), 20, 1);
        var sgd = new Sgd(new[] { w, b }, 0.05);

        for (int step = 0; step < 500; step++)
        {
            sgd.ZeroGrad();
            var loss = Losses.MeanSquaredError(Ops.Add(Ops.MatMul(x, w.Value), b.Value), y);
            loss.Backward();
            sgd.Step();
        }

        Assert.InRange(w.Value.Item(), 1.99, 2.01);
        Assert.InRange(b.Value.Item(), 0.99, 1.01);
    }

    [Fact]
    public void Step_LeavesParameterIdentityUnchanged()
    {
        var w = Scalar("w", 3);
        var tensor = w.Value;
        var data = tensor.Data;
        Ops.Mul(w.Value, w.Value).Backward();
        new Sgd(new[] { w }, 0.1).Step();
        Assert.Same(tensor, w.Value);
        Assert.Same(data, tensor.Data);
        Assert.NotEqual(3.0, tensor.Item());
    }

    [Fact]
    public void Step_SkipsParametersWithoutGradient()
    {
        var w = Scalar("w", 3);
        new Sgd(new[] { w }, 0.1).Step();
        new Adam(new[] { w }, 0.1).Step();
        Assert.Equal(3.0, w.Value.Item());
    }

    [Fact]
    public void ZeroGrad_ClearsEveryParameterGradient()
    {
        var w = Scalar("w", 3);
        var sgd = new Sgd(new[] { w }, 0.1);
        Ops.Mul(w.Value, w.Value).Backward();
        Assert.NotNull(w.Value.Grad);
        sgd.ZeroGrad();
        Assert.Null(w.Value.Grad);
    }
}
