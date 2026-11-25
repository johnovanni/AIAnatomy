using AIAnatomy.Core;
using AIAnatomy.Models.Training;

namespace AIAnatomy.Models.Vision;

/// <summary>
/// Trains an image network in place on a digits set: each step takes the next batch of a seeded shuffle of the set (a new
/// shuffle every time the set is used up, so every image is seen once per pass), the mean cross-entropy over the batch,
/// the gradient norm clipped to 1.0, then Adam. The shuffle matters: the bundled files are grouped by digit.
/// </summary>
public sealed class DigitTrainer : ISteppedTrainer
{
    public const int DefaultBatchSize = 32;
    private const double MaxGradientNorm = 1.0;
    private const int AccuracyBatch = 256;

    private readonly DigitNet _net;
    private readonly DigitSet _train;
    private readonly SeededRandom _rng;
    private readonly Adam _optimizer;
    private readonly IReadOnlyList<Parameter> _parameters;
    private readonly int _batchSize;
    private readonly int[] _order;
    private int _next;

    /// <param name="seed">Seeds the shuffling of the images only; the network's starting weights come from its own seed.</param>
    public DigitTrainer(DigitNet net, DigitSet train, int totalSteps, int seed, int batchSize = DefaultBatchSize, double learningRate = 1e-3)
    {
        ArgumentNullException.ThrowIfNull(net);
        ArgumentNullException.ThrowIfNull(train);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (!(learningRate > 0) || double.IsInfinity(learningRate))
            throw new ArgumentOutOfRangeException(nameof(learningRate), "The learning rate must be a positive number.");
        CheckSet(net, train);

        _net = net;
        _train = train;
        _rng = new SeededRandom(seed);
        _batchSize = batchSize;
        _parameters = net.Parameters().ToList();
        _optimizer = new Adam(_parameters, learningRate);
        _order = Enumerable.Range(0, train.Count).ToArray();
        _next = _order.Length;   // shuffle before the first batch
        TotalSteps = totalSteps;
    }

    public int TotalSteps { get; }
    public int StepsTaken { get; private set; }

    /// <summary>
    /// One batch and one Adam step; returns the training loss. When the loss or the gradient is not a finite number the
    /// weights are left alone and NaN is returned, which <see cref="SteppedTraining.RunAsync"/> reports as diverged.
    /// </summary>
    public double Step()
    {
        if (StepsTaken >= TotalSteps) throw new InvalidOperationException("The network has already taken all its training steps.");
        var indices = new int[_batchSize];
        for (int b = 0; b < _batchSize; b++)
        {
            if (_next == _order.Length) Shuffle();
            indices[b] = _order[_next++];
        }
        var targets = indices.Select(_train.Label).ToArray();

        _optimizer.ZeroGrad();
        var loss = Losses.CrossEntropy(_net.Forward(_train.Batch(indices)).Logits, targets);
        double value = loss.Item();
        StepsTaken++;
        if (!double.IsFinite(value)) return double.NaN;
        loss.Backward();
        if (!ClipGradients()) return double.NaN;
        _optimizer.Step();
        return value;
    }

    /// <summary>The fraction of <paramref name="test"/>'s images whose most probable digit is the label, run in batches.</summary>
    public static double Accuracy(DigitNet net, DigitSet test)
    {
        ArgumentNullException.ThrowIfNull(net);
        ArgumentNullException.ThrowIfNull(test);
        CheckSet(net, test);
        int right = 0;
        for (int start = 0; start < test.Count; start += AccuracyBatch)
        {
            var indices = Enumerable.Range(start, Math.Min(AccuracyBatch, test.Count - start)).ToArray();
            var logits = net.Forward(test.Batch(indices)).Logits;
            int classes = logits.Shape[1];
            for (int r = 0; r < indices.Length; r++)
            {
                int best = 0;
                for (int j = 1; j < classes; j++)
                    if (logits.Data[r * classes + j] > logits.Data[r * classes + best]) best = j;
                if (best == test.Label(indices[r])) right++;
            }
        }
        return (double)right / test.Count;
    }

    private static void CheckSet(DigitNet net, DigitSet set)
    {
        if (set.Count == 0) throw new ArgumentException("The digits set has no images.", nameof(set));
        if (set.ImageSize != net.Config.ImageSize)
            throw new ArgumentException($"The images are {set.ImageSize}x{set.ImageSize} but the network reads {net.Config.ImageSize}x{net.Config.ImageSize}.", nameof(set));
    }

    /// <summary>Fisher-Yates shuffle of the image order with the trainer's generator.</summary>
    private void Shuffle()
    {
        for (int i = _order.Length - 1; i > 0; i--)
        {
            int j = _rng.NextInt(i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
        _next = 0;
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
