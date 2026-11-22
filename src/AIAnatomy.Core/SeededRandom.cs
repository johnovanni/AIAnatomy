namespace AIAnatomy.Core;

/// <summary>Deterministic random source. Every random choice in the app goes through this class.</summary>
public sealed class SeededRandom
{
    private readonly Random _random;

    public SeededRandom(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    public int Seed { get; }

    /// <summary>A value in [0, 1).</summary>
    public double NextDouble() => _random.NextDouble();

    /// <summary>An integer in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive) => _random.Next(maxExclusive);

    /// <summary>A standard normal value (mean 0, standard deviation 1) by the Box-Muller transform.</summary>
    public double NextGaussian()
    {
        double u1 = 1.0 - NextDouble(); // in (0, 1], so the logarithm is finite
        double u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
