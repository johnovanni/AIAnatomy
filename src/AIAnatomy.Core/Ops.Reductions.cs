namespace AIAnatomy.Core;

public static partial class Ops
{
    /// <summary>The sum of every element, as a 1-element tensor.</summary>
    public static Tensor Sum(Tensor a) => Reduce("sum", a, 1.0);

    /// <summary>The average of every element, as a 1-element tensor.</summary>
    public static Tensor Mean(Tensor a) => Reduce("mean", a, a.Size == 0 ? 1.0 : 1.0 / a.Size);

    private static Tensor Reduce(string op, Tensor a, double factor)
    {
        double total = 0;
        foreach (double v in a.Data) total += v;
        return new Tensor(new[] { total * factor }, new[] { 1 }, op, new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            double each = g[0] * factor;
            for (int i = 0; i < ga.Length; i++) ga[i] += each;
        });
    }
}
