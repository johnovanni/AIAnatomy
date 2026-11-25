namespace AIAnatomy.Core;

public static partial class Ops
{
    private static readonly double GeluScale = Math.Sqrt(2.0 / Math.PI);
    private const double GeluCubic = 0.044715;

    public static Tensor Relu(Tensor a)
    {
        var data = new double[a.Size];
        for (int i = 0; i < data.Length; i++) data[i] = a.Data[i] > 0 ? a.Data[i] : 0;
        return new Tensor(data, (int[])a.Shape.Clone(), "relu", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int i = 0; i < g.Length; i++) if (a.Data[i] > 0) ga[i] += g[i];
        });
    }

    public static Tensor Tanh(Tensor a)
    {
        var data = new double[a.Size];
        for (int i = 0; i < data.Length; i++) data[i] = Math.Tanh(a.Data[i]);
        return new Tensor(data, (int[])a.Shape.Clone(), "tanh", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int i = 0; i < g.Length; i++) ga[i] += (1 - data[i] * data[i]) * g[i];
        });
    }

    /// <summary>The tanh approximation of GELU used by GPT-2: 0.5x(1 + tanh(sqrt(2/pi)(x + 0.044715x^3))).</summary>
    public static Tensor Gelu(Tensor a)
    {
        var data = new double[a.Size];
        for (int i = 0; i < data.Length; i++)
        {
            double x = a.Data[i];
            data[i] = 0.5 * x * (1 + Math.Tanh(GeluScale * (x + GeluCubic * x * x * x)));
        }
        return new Tensor(data, (int[])a.Shape.Clone(), "gelu", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int i = 0; i < g.Length; i++)
            {
                double x = a.Data[i];
                double t = Math.Tanh(GeluScale * (x + GeluCubic * x * x * x));
                double slope = 0.5 * (1 + t) + 0.5 * x * (1 - t * t) * GeluScale * (1 + 3 * GeluCubic * x * x);
                ga[i] += g[i] * slope;
            }
        });
    }

    /// <summary>Softmax over each row of an [N,M] matrix. The row maximum is subtracted first so large inputs do not overflow.</summary>
    public static Tensor SoftmaxRows(Tensor a)
    {
        Require2D(a, "softmax");
        int n = a.Shape[0], m = a.Shape[1];
        var data = new double[a.Size];
        for (int r = 0; r < n; r++)
        {
            double max = double.NegativeInfinity;
            for (int j = 0; j < m; j++) max = Math.Max(max, a.Data[r * m + j]);
            double total = 0;
            for (int j = 0; j < m; j++) total += data[r * m + j] = Math.Exp(a.Data[r * m + j] - max);
            for (int j = 0; j < m; j++) data[r * m + j] /= total;
        }
        return new Tensor(data, (int[])a.Shape.Clone(), "softmax", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int r = 0; r < n; r++)
            {
                double dot = 0;
                for (int j = 0; j < m; j++) dot += g[r * m + j] * data[r * m + j];
                for (int j = 0; j < m; j++) ga[r * m + j] += data[r * m + j] * (g[r * m + j] - dot);
            }
        });
    }

    /// <summary>log(softmax) over each row, computed as x - max - log(sum(exp(x - max))).</summary>
    public static Tensor LogSoftmaxRows(Tensor a)
    {
        Require2D(a, "log-softmax");
        int n = a.Shape[0], m = a.Shape[1];
        var data = new double[a.Size];
        for (int r = 0; r < n; r++)
        {
            double max = double.NegativeInfinity;
            for (int j = 0; j < m; j++) max = Math.Max(max, a.Data[r * m + j]);
            double total = 0;
            for (int j = 0; j < m; j++) total += Math.Exp(a.Data[r * m + j] - max);
            double logTotal = Math.Log(total);
            for (int j = 0; j < m; j++) data[r * m + j] = a.Data[r * m + j] - max - logTotal;
        }
        return new Tensor(data, (int[])a.Shape.Clone(), "log-softmax", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int r = 0; r < n; r++)
            {
                double sum = 0;
                for (int j = 0; j < m; j++) sum += g[r * m + j];
                for (int j = 0; j < m; j++) ga[r * m + j] += g[r * m + j] - Math.Exp(data[r * m + j]) * sum;
            }
        });
    }

    /// <summary>
    /// For a [T,T] score matrix, sets every entry above the diagonal (column greater than row) to -infinity, so a
    /// following softmax gives it weight exactly 0. Gradient flows only through the unmasked entries.
    /// </summary>
    public static Tensor CausalMask(Tensor scores)
    {
        Require2D(scores, "causal-mask");
        int t = scores.Shape[0];
        if (scores.Shape[1] != t) throw new ArgumentException($"causal-mask: needs a square matrix but got shape [{string.Join(", ", scores.Shape)}].");
        var data = (double[])scores.Data.Clone();
        for (int r = 0; r < t; r++)
            for (int c = r + 1; c < t; c++) data[r * t + c] = double.NegativeInfinity;
        return new Tensor(data, (int[])scores.Shape.Clone(), "causal-mask", new[] { scores }, self =>
        {
            if (self.Grad is not { } g || !scores.RequiresGrad) return;
            var gs = scores.EnsureGrad();
            for (int r = 0; r < t; r++)
                for (int c = 0; c <= r; c++) gs[r * t + c] += g[r * t + c];
        });
    }

    /// <summary>Normalises each row of [N,D] to mean 0 and (biased) variance 1, then applies gamma * x + beta (both [D]).</summary>
    public static Tensor LayerNormRows(Tensor x, Tensor gamma, Tensor beta, double eps = 1e-5)
    {
        Require2D(x, "layer-norm");
        int n = x.Shape[0], d = x.Shape[1];
        if (gamma.Shape.Length != 1 || gamma.Shape[0] != d || beta.Shape.Length != 1 || beta.Shape[0] != d)
            throw new ArgumentException($"layer-norm: gamma and beta must be [{d}] to match x [{n}, {d}].");

        var normalised = new double[x.Size];
        var inverseStd = new double[n];
        var data = new double[x.Size];
        for (int r = 0; r < n; r++)
        {
            double mean = 0;
            for (int j = 0; j < d; j++) mean += x.Data[r * d + j];
            mean /= d;
            double variance = 0;
            for (int j = 0; j < d; j++) { double c = x.Data[r * d + j] - mean; variance += c * c; }
            variance /= d;
            inverseStd[r] = 1.0 / Math.Sqrt(variance + eps);
            for (int j = 0; j < d; j++)
            {
                normalised[r * d + j] = (x.Data[r * d + j] - mean) * inverseStd[r];
                data[r * d + j] = normalised[r * d + j] * gamma.Data[j] + beta.Data[j];
            }
        }

        return new Tensor(data, (int[])x.Shape.Clone(), "layer-norm", new[] { x, gamma, beta }, self =>
        {
            if (self.Grad is not { } g) return;
            if (gamma.RequiresGrad)
            {
                var gg = gamma.EnsureGrad();
                for (int i = 0; i < g.Length; i++) gg[i % d] += g[i] * normalised[i];
            }
            if (beta.RequiresGrad)
            {
                var gb = beta.EnsureGrad();
                for (int i = 0; i < g.Length; i++) gb[i % d] += g[i];
            }
            if (!x.RequiresGrad) return;
            var gx = x.EnsureGrad();
            for (int r = 0; r < n; r++)
            {
                double meanDx = 0, meanDxNorm = 0;
                for (int j = 0; j < d; j++)
                {
                    double dxhat = g[r * d + j] * gamma.Data[j];
                    meanDx += dxhat;
                    meanDxNorm += dxhat * normalised[r * d + j];
                }
                meanDx /= d;
                meanDxNorm /= d;
                for (int j = 0; j < d; j++)
                {
                    double dxhat = g[r * d + j] * gamma.Data[j];
                    gx[r * d + j] += inverseStd[r] * (dxhat - meanDx - normalised[r * d + j] * meanDxNorm);
                }
            }
        });
    }
}
