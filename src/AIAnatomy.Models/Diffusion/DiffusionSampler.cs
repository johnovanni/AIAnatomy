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
    /// x_{i-1} = (x_i - beta_i / sqrt(1 - AlphaBar(i)) * e_hat) / sqrt(1 - beta_i) + sigma_i z, where e_hat is the network's
    /// predicted noise at the step's original t, and no noise is added at the last step. The noise uses the posterior
    /// variance sigma_i^2 = beta-tilde_i = beta_i (1 - AlphaBar(i-1)) / (1 - AlphaBar(i)) (Ho et al. 2020, section 3.2),
    /// from this schedule's own AlphaBar, so a respaced schedule uses AlphaBar at its own previous step t_{i-1}. A 2-D
    /// shape is a thin curve, close to the case of a fixed x_0 for which Ho et al. call beta-tilde the right choice;
    /// sigma^2 = beta would leave extra noise near t = 0 (docs/decisions.md, Task 10 review). So there are Steps + 1 frames
    /// and the last is the result. The same generator state gives the same frames.
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
            double sigma = Math.Sqrt(beta * (1 - s.AlphaBar(step - 1)) / (1 - s.AlphaBar(step)));
            for (int i = 0; i < x.Length; i++)
            {
                x[i] = (x[i] - noiseScale * predicted[i]) * inverseRootAlpha;
                if (step > 1) x[i] += sigma * rng.NextGaussian();   // the last step adds no noise (sigma_1 is 0 anyway)
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
