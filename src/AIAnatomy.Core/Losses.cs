namespace AIAnatomy.Core;

public static class Losses
{
    /// <summary>Mean over the N rows of -log(softmax(logits)[target]). Logits are [N,V]; one target id per row.</summary>
    public static Tensor CrossEntropy(Tensor logits, IReadOnlyList<int> targets)
    {
        if (logits.Shape.Length != 2) throw new ArgumentException($"cross-entropy: logits must be 2-D but got shape [{string.Join(", ", logits.Shape)}].", nameof(logits));
        int n = logits.Shape[0], v = logits.Shape[1];
        if (targets.Count != n) throw new ArgumentException($"cross-entropy: {targets.Count} targets for {n} rows of logits.", nameof(targets));
        var ids = targets.ToArray();
        foreach (int id in ids)
            if (id < 0 || id >= v) throw new ArgumentOutOfRangeException(nameof(targets), $"cross-entropy: target {id} is outside a vocabulary of {v}.");

        var logProbs = Ops.LogSoftmaxRows(logits).Data;
        double total = 0;
        for (int r = 0; r < n; r++) total -= logProbs[r * v + ids[r]];

        return new Tensor(new[] { n == 0 ? 0.0 : total / n }, new[] { 1 }, "cross-entropy", new[] { logits }, self =>
        {
            if (self.Grad is not { } g || !logits.RequiresGrad) return;
            var gl = logits.EnsureGrad();
            double scale = g[0] / n;
            for (int r = 0; r < n; r++)
                for (int j = 0; j < v; j++)
                    gl[r * v + j] += scale * (Math.Exp(logProbs[r * v + j]) - (j == ids[r] ? 1.0 : 0.0));
        });
    }

    /// <summary>The mean of (prediction - target)^2 over every element.</summary>
    public static Tensor MeanSquaredError(Tensor prediction, Tensor target)
    {
        if (!prediction.Shape.SequenceEqual(target.Shape))
            throw new ArgumentException($"mean-squared-error: shapes [{string.Join(", ", prediction.Shape)}] and [{string.Join(", ", target.Shape)}] do not match.");
        int n = prediction.Size;
        double total = 0;
        for (int i = 0; i < n; i++) { double d = prediction.Data[i] - target.Data[i]; total += d * d; }

        return new Tensor(new[] { n == 0 ? 0.0 : total / n }, new[] { 1 }, "mse", new[] { prediction, target }, self =>
        {
            if (self.Grad is not { } g) return;
            double scale = 2.0 * g[0] / n;
            if (prediction.RequiresGrad)
            {
                var gp = prediction.EnsureGrad();
                for (int i = 0; i < n; i++) gp[i] += scale * (prediction.Data[i] - target.Data[i]);
            }
            if (target.RequiresGrad)
            {
                var gt = target.EnsureGrad();
                for (int i = 0; i < n; i++) gt[i] -= scale * (prediction.Data[i] - target.Data[i]);
            }
        });
    }
}
