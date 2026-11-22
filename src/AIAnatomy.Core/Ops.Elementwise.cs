namespace AIAnatomy.Core;

/// <summary>The tensor operations. Each computes its result and a backward step that adds into its parents' gradients.</summary>
public static partial class Ops
{
    /// <summary>a + b. b may have the same shape as a, or be a [D] vector added to every row of a [N,D] matrix.</summary>
    public static Tensor Add(Tensor a, Tensor b) => Binary("add", a, b, (x, y) => x + y, (g, x, y) => g, (g, x, y) => g);

    /// <summary>a - b, with the same broadcasting as <see cref="Add"/>.</summary>
    public static Tensor Sub(Tensor a, Tensor b) => Binary("sub", a, b, (x, y) => x - y, (g, x, y) => g, (g, x, y) => -g);

    /// <summary>Elementwise a * b, with the same broadcasting as <see cref="Add"/>.</summary>
    public static Tensor Mul(Tensor a, Tensor b) => Binary("mul", a, b, (x, y) => x * y, (g, x, y) => g * y, (g, x, y) => g * x);

    public static Tensor Scale(Tensor a, double s)
    {
        var data = new double[a.Size];
        for (int i = 0; i < data.Length; i++) data[i] = a.Data[i] * s;
        return new Tensor(data, (int[])a.Shape.Clone(), "scale", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int i = 0; i < g.Length; i++) ga[i] += g[i] * s;
        });
    }

    private static Tensor Binary(
        string op, Tensor a, Tensor b,
        Func<double, double, double> forward,
        Func<double, double, double, double> gradA,
        Func<double, double, double, double> gradB)
    {
        bool broadcast = a.Shape.Length == 2 && b.Shape.Length == 1 && a.Shape[1] == b.Shape[0];
        if (!broadcast && !a.Shape.SequenceEqual(b.Shape))
            throw new ArgumentException($"{op}: shapes [{string.Join(", ", a.Shape)}] and [{string.Join(", ", b.Shape)}] do not match.");

        int d = b.Size;
        var data = new double[a.Size];
        for (int i = 0; i < data.Length; i++)
            data[i] = forward(a.Data[i], b.Data[broadcast ? i % d : i]);

        return new Tensor(data, (int[])a.Shape.Clone(), op, new[] { a, b }, self =>
        {
            if (self.Grad is not { } g) return;
            if (a.RequiresGrad)
            {
                var ga = a.EnsureGrad();
                for (int i = 0; i < g.Length; i++) ga[i] += gradA(g[i], a.Data[i], b.Data[broadcast ? i % d : i]);
            }
            if (b.RequiresGrad)
            {
                var gb = b.EnsureGrad();
                for (int i = 0; i < g.Length; i++) gb[broadcast ? i % d : i] += gradB(g[i], a.Data[i], b.Data[broadcast ? i % d : i]);
            }
        });
    }
}
