namespace AIAnatomy.Models.Diffusion;

/// <summary>
/// How much noise each step adds (Ho et al. 2020). The full schedule has linear betas from <c>betaStart</c> at t = 1 to
/// <c>betaEnd</c> at t = Steps; alpha_t = 1 - beta_t and AlphaBar(t) = alpha_1 * ... * alpha_t, so a point after t steps
/// of noise is x_t = sqrt(AlphaBar(t)) x_0 + sqrt(1 - AlphaBar(t)) e with e a pair of unit normals.
/// A respaced schedule (Nichol and Dhariwal 2021) keeps an evenly spaced subsequence t_1 &lt; ... &lt; t_S of the original
/// steps, with beta'_i = 1 - AlphaBar(t_i) / AlphaBar(t_{i-1}) (AlphaBar(t_0) = 1), so its AlphaBar at step i is the
/// original AlphaBar at t_i. Steps here are numbered 1..Steps; <see cref="Timesteps"/> gives each one's original t, which
/// is what the network is shown.
/// </summary>
public sealed class NoiseSchedule
{
    /// <summary>The most steps a schedule may have (the file check uses it too).</summary>
    public const int MaxSteps = 10_000;

    private readonly double[] _originalAlphaBar;   // index t = 0..OriginalSteps, [0] = 1
    private readonly double[] _beta;               // index i = 1..Steps, [0] unused
    private readonly double[] _alphaBar;           // index i = 0..Steps, [0] = 1
    private readonly int[] _timesteps;

    public NoiseSchedule(int steps = 100, double betaStart = 1e-4, double betaEnd = 0.1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(steps, MaxSteps);
        if (!(betaStart > 0) || !(betaStart < 1)) throw new ArgumentOutOfRangeException(nameof(betaStart), "The first beta must be between 0 and 1.");
        if (!(betaEnd >= betaStart) || !(betaEnd < 1)) throw new ArgumentOutOfRangeException(nameof(betaEnd), "The last beta must be at least the first and below 1.");

        Steps = OriginalSteps = steps;
        BetaStart = betaStart;
        BetaEnd = betaEnd;
        _beta = new double[steps + 1];
        _alphaBar = new double[steps + 1];
        _alphaBar[0] = 1;
        for (int t = 1; t <= steps; t++)
        {
            // Linear from betaStart to betaEnd, like numpy.linspace(betaStart, betaEnd, steps) in Ho et al.'s code.
            _beta[t] = steps == 1 ? betaStart : betaStart + (betaEnd - betaStart) * (t - 1) / (steps - 1);
            _alphaBar[t] = _alphaBar[t - 1] * (1 - _beta[t]);
        }
        _originalAlphaBar = _alphaBar;
        _timesteps = Enumerable.Range(1, steps).ToArray();
    }

    private NoiseSchedule(NoiseSchedule original, int[] timesteps)
    {
        Steps = timesteps.Length;
        OriginalSteps = original.OriginalSteps;
        BetaStart = original.BetaStart;
        BetaEnd = original.BetaEnd;
        _originalAlphaBar = original._originalAlphaBar;
        _timesteps = timesteps;
        _beta = new double[Steps + 1];
        _alphaBar = new double[Steps + 1];
        _alphaBar[0] = 1;
        for (int i = 1; i <= Steps; i++)
        {
            _alphaBar[i] = _originalAlphaBar[timesteps[i - 1]];
            _beta[i] = 1 - _alphaBar[i] / _alphaBar[i - 1];
        }
    }

    /// <summary>The number of steps in this schedule.</summary>
    public int Steps { get; }

    /// <summary>The number of steps of the full schedule this one was taken from (equal to <see cref="Steps"/> when not respaced).</summary>
    public int OriginalSteps { get; }

    public double BetaStart { get; }
    public double BetaEnd { get; }

    /// <summary>The original t of each step, in order; 1..Steps for a full schedule.</summary>
    public IReadOnlyList<int> Timesteps => _timesteps;

    /// <summary>beta at step i (1..Steps).</summary>
    public double Beta(int i)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(i, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(i, Steps);
        return _beta[i];
    }

    /// <summary>The product of (1 - beta) up to step i (0..Steps); 1 at step 0, before any noise.</summary>
    public double AlphaBar(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(i, Steps);
        return _alphaBar[i];
    }

    /// <summary>
    /// A schedule of <paramref name="steps"/> steps taken from the full schedule (also when this one is already respaced):
    /// t_i = round(i * OriginalSteps / steps) for i = 1..steps, so the last step is always the original last and, as the
    /// spacing is at least 1, no t repeats. Respaced(OriginalSteps) gives the full schedule's steps again.
    /// </summary>
    public NoiseSchedule Respaced(int steps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(steps, OriginalSteps);
        var timesteps = new int[steps];
        for (int i = 1; i <= steps; i++)
            timesteps[i - 1] = (int)Math.Round((double)i * OriginalSteps / steps, MidpointRounding.AwayFromZero);
        return new NoiseSchedule(this, timesteps);
    }
}
