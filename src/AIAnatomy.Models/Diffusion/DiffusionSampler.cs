using AIAnatomy.Core;

namespace AIAnatomy.Models.Diffusion;

/// <summary>The forward process (adding noise to a shape) and the reverse process (turning noise into points) of DDPM (Ho et al. 2020).</summary>
public static class DiffusionSampler
{
    /// <summary>
    /// The shape's points after <paramref name="t"/> steps of noise (0..s.Steps of schedule <paramref name="s"/>), in one
    /// jump: x_t = sqrt(AlphaBar(t)) x_0 + sqrt(1 - AlphaBar(t)) e. Returns the noisy points and the noise e, both [N,2];
    /// e is drawn point by point, x then y.
    /// </summary>
    public static (double[,] Noisy, double[,] Noise) AddNoise(IReadOnlyList<(double X, double Y)> shape, int t, NoiseSchedule s, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(rng);
        if (shape.Count == 0) throw new ArgumentException("The shape has no points.", nameof(shape));
        double alphaBar = s.AlphaBar(t);
        double a = Math.Sqrt(alphaBar), b = Math.Sqrt(1 - alphaBar);
        var noisy = new double[shape.Count, 2];
        var noise = new double[shape.Count, 2];
        for (int i = 0; i < shape.Count; i++)
        {
            noise[i, 0] = rng.NextGaussian();
            noise[i, 1] = rng.NextGaussian();
            noisy[i, 0] = a * shape[i].X + b * noise[i, 0];
            noisy[i, 1] = a * shape[i].Y + b * noise[i, 1];
        }
        return (noisy, noise);
    }

    /// <summary>
    /// DDPM ancestral sampling of <paramref name="count"/> points. Frame 0 is pure noise x_T (unit normals, drawn point by
    /// point, x then y); each step i = Steps..1 then does
    /// x_{i-1} = (x_i - beta_i / sqrt(1 - AlphaBar(i)) * e_hat) / sqrt(1 - beta_i) + sigma_i z, with sigma_i^2 = beta_i and
    /// no noise at the last step, where e_hat is the network's predicted noise at the step's original t. So there are
    /// Steps + 1 frames and the last is the result. The same generator state gives the same frames.
    /// </summary>
    public static IReadOnlyList<double[,]> Sample(DiffusionNet net, NoiseSchedule s, int count, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(net);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var x = new double[count * 2];
        for (int i = 0; i < x.Length; i++) x[i] = rng.NextGaussian();
        var frames = new List<double[,]>(s.Steps + 1) { ToFrame(x, count) };
        var t = new int[count];
        for (int step = s.Steps; step >= 1; step--)
        {
            Array.Fill(t, s.Timesteps[step - 1]);
            var predicted = net.Forward(Tensor.FromArray(x, count, 2), t, s.OriginalSteps).Data;
            double beta = s.Beta(step);
            double noiseScale = beta / Math.Sqrt(1 - s.AlphaBar(step));
            double inverseRootAlpha = 1 / Math.Sqrt(1 - beta);
            double sigma = Math.Sqrt(beta);
            for (int i = 0; i < x.Length; i++)
            {
                x[i] = (x[i] - noiseScale * predicted[i]) * inverseRootAlpha;
                if (step > 1) x[i] += sigma * rng.NextGaussian();
            }
            frames.Add(ToFrame(x, count));
        }
        return frames;
    }

    private static double[,] ToFrame(double[] x, int count)
    {
        var frame = new double[count, 2];
        for (int i = 0; i < count; i++)
        {
            frame[i, 0] = x[2 * i];
            frame[i, 1] = x[2 * i + 1];
        }
        return frame;
    }
}
