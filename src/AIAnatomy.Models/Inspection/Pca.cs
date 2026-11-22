using AIAnatomy.Core;

namespace AIAnatomy.Models.Inspection;

/// <summary>Principal component analysis down to two dimensions, by power iteration with deflation.</summary>
public static class Pca
{
    private const int MaxIterations = 1000;
    private const double Tolerance = 1e-12;

    /// <summary>
    /// Centres the rows, finds the two directions of greatest variance and returns each row's coordinates along them.
    /// <paramref name="explainedFraction"/> is the share of the total variance those two directions hold.
    /// Each direction's sign is chosen so its largest-magnitude entry is positive, so the result never flips between runs.
    /// </summary>
    public static (double X, double Y)[] Project2D(double[][] rows, out double explainedFraction)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int n = rows.Length;
        if (n == 0) { explainedFraction = 0; return Array.Empty<(double, double)>(); }
        int d = rows[0].Length;
        foreach (var row in rows)
            if (row.Length != d) throw new ArgumentException("All rows must have the same length.", nameof(rows));

        var x = new double[n][];
        var mean = new double[d];
        foreach (var row in rows) for (int j = 0; j < d; j++) mean[j] += row[j] / n;
        double totalVariance = 0;
        for (int i = 0; i < n; i++)
        {
            x[i] = new double[d];
            for (int j = 0; j < d; j++)
            {
                x[i][j] = rows[i][j] - mean[j];
                totalVariance += x[i][j] * x[i][j];
            }
        }

        var scores1 = new double[n];
        var scores2 = new double[n];
        double captured = 0;
        for (int component = 0; component < 2; component++)
        {
            var scores = component == 0 ? scores1 : scores2;
            var (direction, strength) = Strongest(x, d, component);
            if (strength <= 0) continue;
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j < d; j++) s += x[i][j] * direction[j];
                scores[i] = s;
            }
            captured += strength;
            for (int i = 0; i < n; i++)   // deflate: remove what this component explains
                for (int j = 0; j < d; j++) x[i][j] -= scores[i] * direction[j];
        }

        explainedFraction = totalVariance > 0 ? Math.Min(1.0, captured / totalVariance) : 0;
        var result = new (double X, double Y)[n];
        for (int i = 0; i < n; i++) result[i] = (scores1[i], scores2[i]);
        return result;
    }

    /// <summary>The unit direction of greatest variance of the rows, and the variance sum along it (sum of squared scores).</summary>
    private static (double[] Direction, double Strength) Strongest(double[][] x, int d, int component)
    {
        int n = x.Length;
        var rng = new SeededRandom(1000 + component);   // fixed start, so results are repeatable
        var v = new double[d];
        for (int j = 0; j < d; j++) v[j] = rng.NextGaussian();
        if (!Normalize(v)) return (v, 0);

        var next = new double[d];
        double strength = 0;
        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            Array.Clear(next);
            strength = 0;
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j < d; j++) s += x[i][j] * v[j];
                strength += s * s;
                for (int j = 0; j < d; j++) next[j] += s * x[i][j];   // (X^T X) v
            }
            if (!Normalize(next)) return (v, 0);   // nothing left: the data has no more spread
            double change = 0;
            for (int j = 0; j < d; j++) change += (next[j] - v[j]) * (next[j] - v[j]);
            Array.Copy(next, v, d);
            if (change < Tolerance * Tolerance) break;
        }

        // The strength above was measured along the previous iterate; remeasure along the final direction.
        strength = 0;
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int j = 0; j < d; j++) s += x[i][j] * v[j];
            strength += s * s;
        }

        int biggest = 0;
        for (int j = 1; j < d; j++) if (Math.Abs(v[j]) > Math.Abs(v[biggest])) biggest = j;
        if (v[biggest] < 0) for (int j = 0; j < d; j++) v[j] = -v[j];
        return (v, strength);
    }

    private static bool Normalize(double[] v)
    {
        double norm = 0;
        foreach (double e in v) norm += e * e;
        norm = Math.Sqrt(norm);
        if (!(norm > 1e-300)) return false;
        for (int j = 0; j < v.Length; j++) v[j] /= norm;
        return true;
    }
}
