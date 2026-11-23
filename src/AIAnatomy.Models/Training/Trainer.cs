using AIAnatomy.Core;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Training;

/// <summary>Trains a transformer in place on windows from a batch source: cross-entropy, clipped gradients, Adam.</summary>
public sealed class Trainer
{
    public const string DivergedMessage = "Training stopped: the loss became too large (try a smaller learning rate)";
    public const string AlreadyRunningMessage = "Training is already running";
    private const double DivergenceLimit = 1e4;
    private const double MaxGradientNorm = 1.0;
    private const int ValidationEvery = 25;

    private readonly Transformer _model;
    private readonly IBatchSource _source;
    private readonly TrainingSettings _settings;
    private readonly SeededRandom _rng;
    private readonly Adam _optimizer;
    private readonly IReadOnlyList<Parameter> _parameters;
    private readonly int[][] _validation;   // each window: inputs then targets shifted by one
    private readonly Transformer[] _slots;   // one model per batch sequence; slot 0 is the model itself
    private int _running;                    // 1 while RunAsync is running

    /// <summary>Trains on random windows of a text (see <see cref="CorpusBatchSource"/>).</summary>
    public Trainer(Transformer model, IReadOnlyList<int> corpusTokens, TrainingSettings settings)
        : this(model, corpusTokens, settings, new SeededRandom(settings?.Seed ?? 0))
    {
    }

    private Trainer(Transformer model, IReadOnlyList<int> corpusTokens, TrainingSettings settings, SeededRandom rng)
        : this(model, MakeCorpusSource(model, corpusTokens, settings, rng), settings, rng)
    {
    }

    /// <summary>Trains on windows from any source (the corpus, or generated sequences).</summary>
    public Trainer(Transformer model, IBatchSource source, TrainingSettings settings)
        : this(model, source, settings, new SeededRandom(settings?.Seed ?? 0))
    {
    }

    private Trainer(Transformer model, IBatchSource source, TrainingSettings settings, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);
        ValidateSettings(settings);
        _model = model;
        _source = source;
        _settings = settings;
        _rng = rng;
        _parameters = model.Parameters().ToList();
        _optimizer = new Adam(_parameters, settings.LearningRate);
        _validation = source.ValidationWindows.Select(w => w.ToArray()).ToArray();
        if (_validation.Length == 0) throw new ArgumentException("The batch source has no validation windows.", nameof(source));
        _slots = new Transformer[settings.BatchSize];
        _slots[0] = model;
        for (int i = 1; i < _slots.Length; i++) _slots[i] = model.Clone();
    }

    private static CorpusBatchSource MakeCorpusSource(Transformer model, IReadOnlyList<int> corpusTokens, TrainingSettings settings, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(corpusTokens);
        ArgumentNullException.ThrowIfNull(settings);
        ValidateSettings(settings);
        return new CorpusBatchSource(corpusTokens, model.Config.ContextLength, model.Config.VocabularySize, settings.ValidationFraction, rng);
    }

    private static void ValidateSettings(TrainingSettings settings)
    {
        if (settings.BatchSize < 1) throw new ArgumentException("The batch size must be at least 1.", nameof(settings));
        if (!(settings.LearningRate > 0) || double.IsInfinity(settings.LearningRate))
            throw new ArgumentException("The learning rate must be a positive number.", nameof(settings));
        if (!(settings.ValidationFraction >= 0 && settings.ValidationFraction < 1))
            throw new ArgumentException("The validation fraction must be from 0 up to (not including) 1.", nameof(settings));
    }

    public bool HasDiverged { get; private set; }
    public int StepsTaken { get; private set; }

    /// <summary>The most recent validation loss, or null before the first step.</summary>
    public double? LatestValidationLoss { get; private set; }

    /// <summary>One batch and one Adam step. Validation loss is measured at step 1 and every 25 steps.</summary>
    public TrainingStepReport Step()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) throw new InvalidOperationException(AlreadyRunningMessage);
        try { return StepCore(); }
        finally { Volatile.Write(ref _running, 0); }
    }

    private TrainingStepReport StepCore()
    {
        if (HasDiverged) throw new InvalidOperationException(DivergedMessage);

        _optimizer.ZeroGrad();
        int batch = _settings.BatchSize;
        // Windows are drawn in order from the seeded generator; the sequences then run on separate threads,
        // each on its own copy of the weights, and their gradients are added together in sequence order.
        var windows = new int[batch][];
        for (int b = 0; b < batch; b++) windows[b] = _source.NextTrainingWindow(_rng).ToArray();
        for (int b = 1; b < batch; b++)
        {
            CopyWeights(_model, _slots[b]);
            foreach (var p in _slots[b].Parameters()) p.Value.ZeroGrad();
        }

        var losses = new double[batch];
        Parallel.For(0, batch, b =>
        {
            var slot = _slots[b];
            var (inputs, targets) = Split(windows[b]);
            var loss = Losses.CrossEntropy(slot.Forward(inputs).Logits, targets);
            losses[b] = loss.Item();
            loss.Backward();
        });

        double total = 0;
        foreach (double l in losses) total += l;
        if (batch > 1) AddSlotGradients();
        double trainLoss = total / batch;
        StepsTaken++;

        if (double.IsNaN(trainLoss) || double.IsInfinity(trainLoss) || trainLoss > DivergenceLimit
            || !ScaleAndClipGradients(1.0 / batch))
        {
            HasDiverged = true;
            return new TrainingStepReport(StepsTaken, trainLoss, null, DivergedMessage);
        }
        _optimizer.Step();

        double? validation = null;
        if (StepsTaken == 1 || StepsTaken % ValidationEvery == 0)
        {
            validation = ValidationLoss();
            LatestValidationLoss = validation;
        }
        return new TrainingStepReport(StepsTaken, trainLoss, validation, null);
    }

    /// <summary>Runs steps on a background thread until <paramref name="steps"/> are done, training diverges, or it is cancelled.</summary>
    public Task RunAsync(int steps, IProgress<TrainingStepReport> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) throw new InvalidOperationException(AlreadyRunningMessage);
        return Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < steps && !ct.IsCancellationRequested; i++)
                {
                    var report = StepCore();
                    progress.Report(report);
                    if (report.StoppedReason is not null) break;
                }
            }
            finally { Volatile.Write(ref _running, 0); }
        }, CancellationToken.None);
    }

    /// <summary>A window holds n+1 tokens: the first n are the inputs, the last n are the targets (shifted by one).</summary>
    private static (int[] Inputs, int[] Targets) Split(int[] window)
    {
        int n = window.Length - 1;
        if (n < 1) throw new ArgumentException("A training window needs at least two tokens.");
        return (window[..n], window[1..]);
    }

    private static void CopyWeights(Transformer from, Transformer to)
    {
        foreach (var (source, target) in from.Parameters().Zip(to.Parameters()))
            Array.Copy(source.Value.Data, target.Value.Data, source.Value.Size);
    }

    /// <summary>Adds the gradients of slots 1.. into the model's own, in slot order, so the sum never depends on thread timing.</summary>
    private void AddSlotGradients()
    {
        for (int b = 1; b < _slots.Length; b++)
            foreach (var (main, other) in _parameters.Zip(_slots[b].Parameters()))
            {
                if (main.Value.Grad is not { } target || other.Value.Grad is not { } source) continue;
                for (int i = 0; i < target.Length; i++) target[i] += source[i];
            }
    }

    /// <summary>Scales the gradients, then clips them to the maximum norm. False when the norm is not a finite number.</summary>
    private bool ScaleAndClipGradients(double scale)
    {
        double sumSquares = 0;
        foreach (var p in _parameters)
        {
            if (p.Value.Grad is not { } g) continue;
            for (int i = 0; i < g.Length; i++)
            {
                g[i] *= scale;
                sumSquares += g[i] * g[i];
            }
        }
        double norm = Math.Sqrt(sumSquares);
        if (!double.IsFinite(norm)) return false;
        if (!(norm > MaxGradientNorm)) return true;
        double clip = MaxGradientNorm / norm;
        foreach (var p in _parameters)
        {
            if (p.Value.Grad is not { } g) continue;
            for (int i = 0; i < g.Length; i++) g[i] *= clip;
        }
        return true;
    }

    private double ValidationLoss()
    {
        // Forward passes only read the weights, so the windows can run side by side; the losses are summed in order.
        var losses = new double[_validation.Length];
        Parallel.For(0, losses.Length, w =>
        {
            var (inputs, targets) = Split(_validation[w]);
            losses[w] = Losses.CrossEntropy(_model.Forward(inputs).Logits, targets).Item();
        });
        double total = 0;
        foreach (double l in losses) total += l;
        return total / losses.Length;
    }
}
