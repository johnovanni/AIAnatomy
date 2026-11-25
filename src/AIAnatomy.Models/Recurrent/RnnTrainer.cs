using AIAnatomy.Core;
using AIAnatomy.Models.Training;

namespace AIAnatomy.Models.Recurrent;

/// <summary>
/// Trains a memory network in place on windows from a batch source, the way <see cref="Trainer"/> trains a transformer:
/// cross-entropy on every position, the gradient norm clipped to 1.0, then Adam. The batch's windows run side by side in
/// one forward pass, so the loss is already the mean over the batch (Trainer's sum of per-window means times 1/batch).
/// </summary>
public sealed class RnnTrainer : ISteppedTrainer
{
    private const double MaxGradientNorm = 1.0;

    private readonly ElmanRnn _rnn;
    private readonly IBatchSource _source;
    private readonly SeededRandom _rng;
    private readonly Adam _optimizer;
    private readonly IReadOnlyList<Parameter> _parameters;
    private readonly int _batchSize;

    /// <param name="seed">Seeds the drawing of training windows only; the network's starting weights come from its own seed.</param>
    public RnnTrainer(ElmanRnn rnn, IBatchSource source, int totalSteps, int seed, int batchSize = 8, double learningRate = 3e-3)
    {
        ArgumentNullException.ThrowIfNull(rnn);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (!(learningRate > 0) || double.IsInfinity(learningRate))
            throw new ArgumentOutOfRangeException(nameof(learningRate), "The learning rate must be a positive number.");
        if (source.ValidationWindows.Count == 0) throw new ArgumentException("The batch source has no validation windows.", nameof(source));
        int length = source.ValidationWindows[0].Count;
        if (length < 2 || source.ValidationWindows.Any(w => w.Count != length))
            throw new ArgumentException("The validation windows must all have the same length of at least two tokens.", nameof(source));
        if (source.ValidationWindows.Any(w => w.Any(id => id < 0 || id >= rnn.Config.VocabularySize)))
            throw new ArgumentException($"The windows hold tokens outside the network's vocabulary of {rnn.Config.VocabularySize}.", nameof(source));

        _rnn = rnn;
        _source = source;
        _rng = new SeededRandom(seed);
        _batchSize = batchSize;
        _parameters = rnn.Parameters().ToList();
        _optimizer = new Adam(_parameters, learningRate);
        TotalSteps = totalSteps;
    }

    public int TotalSteps { get; }
    public int StepsTaken { get; private set; }

    /// <summary>The source's fixed validation windows (n+1 tokens each).</summary>
    public IReadOnlyList<IReadOnlyList<int>> ValidationWindows => _source.ValidationWindows;

    /// <summary>
    /// One batch and one Adam step; returns the training loss. When the loss or the gradient is not a finite number the
    /// weights are left alone and NaN is returned, which <see cref="SteppedTraining.RunAsync"/> reports as diverged.
    /// </summary>
    public double Step()
    {
        if (StepsTaken >= TotalSteps) throw new InvalidOperationException("The network has already taken all its training steps.");
        var windows = new IReadOnlyList<int>[_batchSize];
        for (int b = 0; b < _batchSize; b++) windows[b] = _source.NextTrainingWindow(_rng);

        _optimizer.ZeroGrad();
        var loss = Loss(_rnn, windows);
        double value = loss.Item();
        StepsTaken++;
        if (!double.IsFinite(value)) return double.NaN;
        loss.Backward();
        if (!ClipGradients()) return double.NaN;
        _optimizer.Step();
        return value;
    }

    /// <summary>The mean cross-entropy over every position of the validation windows (all run in one pass).</summary>
    public double ValidationLoss() => Loss(_rnn, _source.ValidationWindows).Item();

    /// <summary>Mean cross-entropy of predicting each window's tokens 1..n from tokens 0..n-1.</summary>
    internal static Tensor Loss(ElmanRnn rnn, IReadOnlyList<IReadOnlyList<int>> windows)
    {
        var (inputs, targets) = Split(windows);
        return Losses.CrossEntropy(rnn.Forward(inputs).Logits, targets);
    }

    /// <summary>Inputs are each window's first n tokens; targets are tokens 1..n in the logits' position-major order.</summary>
    internal static (IReadOnlyList<int>[] Inputs, int[] Targets) Split(IReadOnlyList<IReadOnlyList<int>> windows)
    {
        if (windows.Count == 0) throw new ArgumentException("Give at least one window.", nameof(windows));
        int n = windows[0].Count - 1;
        if (n < 1) throw new ArgumentException("A window needs at least two tokens.", nameof(windows));
        if (windows.Any(w => w is null || w.Count != n + 1)) throw new ArgumentException("All the windows must have the same length.", nameof(windows));
        int batch = windows.Count;
        var inputs = new IReadOnlyList<int>[batch];
        var targets = new int[n * batch];
        for (int b = 0; b < batch; b++)
        {
            var w = windows[b];
            var input = new int[n];
            for (int t = 0; t < n; t++)
            {
                input[t] = w[t];
                targets[t * batch + b] = w[t + 1];
            }
            inputs[b] = input;
        }
        return (inputs, targets);
    }

    /// <summary>Clips the gradient to the maximum norm. False when the norm is not a finite number.</summary>
    private bool ClipGradients()
    {
        double sumSquares = 0;
        foreach (var p in _parameters)
            if (p.Value.Grad is { } g)
                foreach (double x in g) sumSquares += x * x;
        double norm = Math.Sqrt(sumSquares);
        if (!double.IsFinite(norm)) return false;
        if (!(norm > MaxGradientNorm)) return true;
        double clip = MaxGradientNorm / norm;
        foreach (var p in _parameters)
            if (p.Value.Grad is { } g)
                for (int i = 0; i < g.Length; i++) g[i] *= clip;
        return true;
    }
}
