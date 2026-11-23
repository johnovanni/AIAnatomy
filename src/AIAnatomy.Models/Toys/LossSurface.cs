namespace AIAnatomy.Models.Toys;

/// <summary>A bowl that is steep one way and shallow the other: f(w1, w2) = w1² + 10·w2².</summary>
public static class LossSurface
{
    public static double Loss(double w1, double w2) => w1 * w1 + 10 * w2 * w2;

    /// <summary>
    /// Plain gradient descent with gradient (2·w1, 20·w2). Entry 0 is the starting point, so the result has steps + 1 entries.
    /// </summary>
    public static IReadOnlyList<(double W1, double W2, double Loss)> Descend(double w1, double w2, double rate, int steps)
    {
        var path = new List<(double W1, double W2, double Loss)>(steps + 1) { (w1, w2, Loss(w1, w2)) };
        for (int i = 0; i < steps; i++)
        {
            w1 -= rate * 2 * w1;
            w2 -= rate * 20 * w2;
            path.Add((w1, w2, Loss(w1, w2)));
        }
        return path;
    }
}
