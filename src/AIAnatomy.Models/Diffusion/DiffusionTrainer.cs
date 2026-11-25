using AIAnatomy.Core;
using AIAnatomy.Models.Training;

namespace AIAnatomy.Models.Diffusion;

/// <summary>
/// Trains a diffusion network in place to predict the noise (Ho et al. 2020, algorithm 1): each step draws a batch of
/// points of the shape (with replacement), a step i of the schedule for each (uniform over 1..Steps) and a pair of unit
/// normals e, makes x = sqrt(AlphaBar(i)) x_0 + sqrt(1 - AlphaBar(i)) e, and takes one Adam step on the mean squared
/// error between the network's predicted noise and e.
/// </summary>
public sealed class DiffusionTrainer : ISteppedTrainer
{
    private readonly DiffusionNet _net;
    private readonly NoiseSchedule _schedule;
    private readonly (double X, double Y)[] _shape;
    private readonly SeededRandom _rng;
    private readonly Adam _optimizer;
    private readonly int _batchSize;

    /// <param name="seed">Seeds the drawing of points, steps and noise only; the network's starting weights come from its own seed.</param>
    public DiffusionTrainer(DiffusionNet net, NoiseSchedule s, IReadOnlyList<(double X, double Y)> shape, int totalSteps, int seed,
        int batchSize = 128, double learningRate = 1e-3)
    {
        ArgumentNullException.ThrowIfNull(net);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalSteps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (!(learningRate > 0) || double.IsInfinity(learningRate))
            throw new ArgumentOutOfRangeException(nameof(learningRate), "The learning rate must be a positive number.");
        if (shape.Count == 0) throw new ArgumentException("The shape has no points.", nameof(shape));
        if (shape.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            throw new ArgumentException("The shape has a point that is not a number.", nameof(shape));

        _net = net;
        _schedule = s;
        _shape = shape.ToArray();
        _rng = new SeededRandom(seed);
        _batchSize = batchSize;
        _optimizer = new Adam(net.Parameters().ToList(), learningRate);
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
        var noisy = new double[_batchSize * 2];
        var noise = new double[_batchSize * 2];
        var t = new int[_batchSize];
        for (int b = 0; b < _batchSize; b++)
        {
            var point = _shape[_rng.NextInt(_shape.Length)];
            int step = 1 + _rng.NextInt(_schedule.Steps);
            t[b] = _schedule.Timesteps[step - 1];
            double alphaBar = _schedule.AlphaBar(step);
            double a = Math.Sqrt(alphaBar), c = Math.Sqrt(1 - alphaBar);
            noise[2 * b] = _rng.NextGaussian();
            noise[2 * b + 1] = _rng.NextGaussian();
            noisy[2 * b] = a * point.X + c * noise[2 * b];
            noisy[2 * b + 1] = a * point.Y + c * noise[2 * b + 1];
        }

        _optimizer.ZeroGrad();
        var predicted = _net.Forward(Tensor.FromArray(noisy, _batchSize, 2), t, _schedule.OriginalSteps);
        var loss = Losses.MeanSquaredError(predicted, Tensor.FromArray(noise, _batchSize, 2));
        double value = loss.Item();
        StepsTaken++;
        if (!double.IsFinite(value)) return double.NaN;
        loss.Backward();
        foreach (var p in _net.Parameters())
            if (p.Value.Grad is { } g && g.Any(v => !double.IsFinite(v))) return double.NaN;
        _optimizer.Step();
        return value;
    }
}
