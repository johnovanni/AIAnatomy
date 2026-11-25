using AIAnatomy.Core;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Recurrent;

/// <summary>
/// "How far back it remembers", measured: the mean cross-entropy at each position of a set of windows. Position t predicts
/// token t+1 having read tokens 0..t, so a curve that keeps falling with t means the model is using older characters.
/// Both models take the same windows (n+1 tokens each: inputs, then targets shifted by one), so the curves compare.
/// </summary>
public static class ContextLoss
{
    /// <summary>The transformer's loss at positions 0..n-1. Windows must have n+1 tokens with n at most its context length.</summary>
    public static double[] LossByPosition(Transformer model, IReadOnlyList<IReadOnlyList<int>> windows)
    {
        ArgumentNullException.ThrowIfNull(model);
        int n = CheckWindows(windows);
        if (n > model.Config.ContextLength)
            throw new ArgumentException($"The windows are longer than the model's context of {model.Config.ContextLength}.", nameof(windows));

        // Forward passes only read the weights, so the windows run side by side; the sums are added in window order.
        var perWindow = new double[windows.Count][];
        Parallel.For(0, windows.Count, w =>
        {
            var window = windows[w];
            var logits = model.Forward(window.Take(n).ToArray()).Logits;
            perWindow[w] = PositionLosses(Ops.LogSoftmaxRows(logits).Data, logits.Shape[1], t => t, t => window[t + 1], n);
        });
        var mean = new double[n];
        foreach (var losses in perWindow)
            for (int t = 0; t < n; t++) mean[t] += losses[t];
        for (int t = 0; t < n; t++) mean[t] /= windows.Count;
        return mean;
    }

    /// <summary>The memory network's loss at positions 0..n-1, every window starting from an empty memory.</summary>
    public static double[] LossByPosition(ElmanRnn rnn, IReadOnlyList<IReadOnlyList<int>> windows)
    {
        ArgumentNullException.ThrowIfNull(rnn);
        int n = CheckWindows(windows);
        var (inputs, targets) = RnnTrainer.Split(windows);
        var logits = rnn.Forward(inputs).Logits;
        var logProbs = Ops.LogSoftmaxRows(logits).Data;
        int v = logits.Shape[1], batch = windows.Count;
        var mean = new double[n];
        for (int b = 0; b < batch; b++)
        {
            int window = b;
            var losses = PositionLosses(logProbs, v, t => t * batch + window, t => targets[t * batch + window], n);
            for (int t = 0; t < n; t++) mean[t] += losses[t];
        }
        for (int t = 0; t < n; t++) mean[t] /= batch;
        return mean;
    }

    private static double[] PositionLosses(double[] logProbs, int v, Func<int, int> row, Func<int, int> target, int n)
    {
        var losses = new double[n];
        for (int t = 0; t < n; t++)
        {
            int id = target(t);
            if (id < 0 || id >= v) throw new ArgumentOutOfRangeException(nameof(target), $"Token {id} is outside the vocabulary of {v}.");
            losses[t] = -logProbs[row(t) * v + id];
        }
        return losses;
    }

    /// <summary>Returns n, the number of positions: every window must hold the same n+1 tokens, n at least 1.</summary>
    private static int CheckWindows(IReadOnlyList<IReadOnlyList<int>> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        if (windows.Count == 0) throw new ArgumentException("Give at least one window.", nameof(windows));
        int length = windows[0]?.Count ?? 0;
        if (length < 2) throw new ArgumentException("A window needs at least two tokens.", nameof(windows));
        if (windows.Any(w => w is null || w.Count != length)) throw new ArgumentException("All the windows must have the same length.", nameof(windows));
        return length - 1;
    }
}
