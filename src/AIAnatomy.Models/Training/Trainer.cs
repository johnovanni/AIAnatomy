using AIAnatomy.Core;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Training;

/// <summary>Trains a transformer in place on a token list: random windows, cross-entropy, clipped gradients, Adam.</summary>
public sealed class Trainer
{
    public const string DivergedMessage = "Training stopped: the loss became too large (try a smaller learning rate)";
    private const double DivergenceLimit = 1e4;
    private const double MaxGradientNorm = 1.0;
    private const int ValidationEvery = 25;
    private const int MaxValidationWindows = 32;

    private readonly Transformer _model;
    private readonly int[] _tokens;
    private readonly TrainingSettings _settings;
    private readonly SeededRandom _rng;
    private readonly Adam _optimizer;
    private readonly IReadOnlyList<Parameter> _parameters;
    private readonly int _context;
    private readonly int _trainWindowStarts;   // training windows start in [0, _trainWindowStarts)
    private readonly int[] _validationStarts;
    private readonly Transformer[] _slots;   // one model per batch sequence; slot 0 is the model itself

    public Trainer(Transformer model, IReadOnlyList<int> corpusTokens, TrainingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(corpusTokens);
        ArgumentNullException.ThrowIfNull(settings);
        _context = model.Config.ContextLength;
        if (CorpusValidator.ProblemForLength(corpusTokens.Count, _context) is { } problem)
            throw new ArgumentException(problem, nameof(corpusTokens));
        if (settings.BatchSize < 1) throw new ArgumentException("The batch size must be at least 1.", nameof(settings));
        if (!(settings.LearningRate > 0) || double.IsInfinity(settings.LearningRate))
            throw new ArgumentException("The learning rate must be a positive number.", nameof(settings));
        if (!(settings.ValidationFraction >= 0 && settings.ValidationFraction < 1))
            throw new ArgumentException("The validation fraction must be from 0 up to (not including) 1.", nameof(settings));
        foreach (int id in corpusTokens)
            if (id < 0 || id >= model.Config.VocabularySize)
                throw new ArgumentException($"The text has a token ({id}) outside the model's vocabulary of {model.Config.VocabularySize}.", nameof(corpusTokens));

        _model = model;
        _tokens = corpusTokens.ToArray();
        _settings = settings;
        _rng = new SeededRandom(settings.Seed);
        _parameters = model.Parameters().ToList();
        _optimizer = new Adam(_parameters, settings.LearningRate);
        _slots = new Transformer[settings.BatchSize];
        _slots[0] = model;
        for (int i = 1; i < _slots.Length; i++) _slots[i] = model.Clone();

        int window = _context + 1;
        int n = _tokens.Length;
        int trainEnd = Math.Max(window, (int)Math.Floor(n * (1 - settings.ValidationFraction)));
        _trainWindowStarts = trainEnd - window + 1;
        // The last 10%; if that is shorter than one window it grows backwards just enough to hold one.
        int validationStart = Math.Min(trainEnd, n - window);
        _validationStarts = ChooseValidationStarts(validationStart, n - window - validationStart + 1);
    }

    public bool HasDiverged { get; private set; }
    public int StepsTaken { get; private set; }

    /// <summary>The most recent validation loss, or null before the first step.</summary>
    public double? LatestValidationLoss { get; private set; }

    /// <summary>One batch and one Adam step. Validation loss is measured at step 1 and every 25 steps.</summary>
    public TrainingStepReport Step()
    {
        if (HasDiverged) throw new InvalidOperationException(DivergedMessage);

        _optimizer.ZeroGrad();
        int batch = _settings.BatchSize;
        // Windows are drawn in order from the seeded generator; the sequences then run on separate threads,
        // each on its own copy of the weights, and their gradients are added together in sequence order.
        var starts = new int[batch];
        for (int b = 0; b < batch; b++) starts[b] = _rng.NextInt(_trainWindowStarts);
        for (int b = 1; b < batch; b++)
        {
            CopyWeights(_model, _slots[b]);
            foreach (var p in _slots[b].Parameters()) p.Value.ZeroGrad();
        }

        var losses = new double[batch];
        Parallel.For(0, batch, b =>
        {
            var slot = _slots[b];
            var inputs = new ArraySegment<int>(_tokens, starts[b], _context);
            var targets = new ArraySegment<int>(_tokens, starts[b] + 1, _context);
            var loss = Losses.CrossEntropy(slot.Forward(inputs).Logits, targets);
            losses[b] = loss.Item();
            loss.Backward();
        });

        double total = 0;
        foreach (double l in losses) total += l;
        if (batch > 1) AddSlotGradients();
        double trainLoss = total / batch;
        StepsTaken++;

        if (double.IsNaN(trainLoss) || double.IsInfinity(trainLoss) || trainLoss > DivergenceLimit)
        {
            HasDiverged = true;
            return new TrainingStepReport(StepsTaken, trainLoss, null, DivergedMessage);
        }

        ScaleAndClipGradients(1.0 / batch);
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
        return Task.Run(() =>
        {
            for (int i = 0; i < steps && !ct.IsCancellationRequested; i++)
            {
                var report = Step();
                progress.Report(report);
                if (report.StoppedReason is not null) break;
            }
        }, CancellationToken.None);
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

    private void ScaleAndClipGradients(double scale)
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
        if (!(norm > MaxGradientNorm)) return;
        double clip = MaxGradientNorm / norm;
        foreach (var p in _parameters)
        {
            if (p.Value.Grad is not { } g) continue;
            for (int i = 0; i < g.Length; i++) g[i] *= clip;
        }
    }

    private double ValidationLoss()
    {
        // Forward passes only read the weights, so the windows can run side by side; the losses are summed in order.
        var losses = new double[_validationStarts.Length];
        Parallel.For(0, losses.Length, w =>
        {
            var inputs = new ArraySegment<int>(_tokens, _validationStarts[w], _context);
            var targets = new ArraySegment<int>(_tokens, _validationStarts[w] + 1, _context);
            losses[w] = Losses.CrossEntropy(_model.Forward(inputs).Logits, targets).Item();
        });
        double total = 0;
        foreach (double l in losses) total += l;
        return total / losses.Length;
    }

    private int[] ChooseValidationStarts(int first, int possible)
    {
        if (possible <= MaxValidationWindows) return Enumerable.Range(first, possible).ToArray();
        var chosen = new SortedSet<int>();
        while (chosen.Count < MaxValidationWindows) chosen.Add(first + _rng.NextInt(possible));
        return chosen.ToArray();
    }
}
