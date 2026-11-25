using System.Text.Json;
using AIAnatomy.Core;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Diffusion;

/// <summary>
/// The shape of the noise-predicting network: its hidden width, and how many time features it is shown
/// (sin and cos at TimeFeatures / 2 frequencies, so an even number).
/// </summary>
public sealed record DiffusionConfig(int Hidden = 128, int TimeFeatures = 16)
{
    /// <summary>The most time features allowed: 32 frequencies, the highest 2^31 times the lowest.</summary>
    public const int MaxTimeFeatures = 64;

    /// <summary>The network's input width: x, y and the time features.</summary>
    public int InputWidth => 2 + TimeFeatures;

    /// <summary>Total number of learnable numbers; equals the summed size of <c>DiffusionNet.Parameters()</c>.</summary>
    public long CountParameters()
    {
        long i = InputWidth, h = Hidden;
        return i * h + h + h * h + h + h * 2 + 2;
    }
}

/// <summary>
/// A small fully connected network that predicts the noise in a noisy point (the epsilon-prediction of Ho et al. 2020):
/// [x, y, time features] -> Hidden -> Hidden -> 2, with ReLU after both hidden layers and nothing after the output.
/// The time features of step t are sin and cos of (t / Steps) * pi * 2^k for k = 0 .. TimeFeatures/2 - 1, where Steps is
/// the length of the schedule the network was trained on; a respaced schedule passes its original t values.
/// </summary>
public sealed class DiffusionNet : Module
{
    public const string Layer1Name = "layer1.weight", Layer1BiasName = "layer1.bias", Layer2Name = "layer2.weight",
        Layer2BiasName = "layer2.bias", OutputName = "output.weight", OutputBiasName = "output.bias";

    private static readonly string[] Names = { Layer1Name, Layer1BiasName, Layer2Name, Layer2BiasName, OutputName, OutputBiasName };

    private readonly Parameter[] _parameters;

    /// <summary>
    /// Starting weights: He initialisation (std sqrt(2 / fan-in)) for the two ReLU layers, std 1/sqrt(Hidden) for the
    /// output layer, biases 0.
    /// </summary>
    public DiffusionNet(DiffusionConfig config, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rng);
        CheckConfig(config);
        Config = config;
        int input = config.InputWidth, h = config.Hidden;
        _parameters = new[]
        {
            new Parameter(Layer1Name, Tensor.RandomNormal(rng, Math.Sqrt(2.0 / input), input, h).AsParameter()),
            new Parameter(Layer1BiasName, Tensor.Zeros(h).AsParameter()),
            new Parameter(Layer2Name, Tensor.RandomNormal(rng, Math.Sqrt(2.0 / h), h, h).AsParameter()),
            new Parameter(Layer2BiasName, Tensor.Zeros(h).AsParameter()),
            new Parameter(OutputName, Tensor.RandomNormal(rng, 1 / Math.Sqrt(h), h, 2).AsParameter()),
            new Parameter(OutputBiasName, Tensor.Zeros(2).AsParameter()),
        };
    }

    private DiffusionNet(DiffusionConfig config, Parameter[] parameters)
    {
        Config = config;
        _parameters = parameters;
    }

    /// <summary>
    /// A network that uses the given tensors as its parameters (not copies), in <see cref="Parameters"/> order. Used for
    /// gradient checks and when loading a file. Shapes must match the config.
    /// </summary>
    public static DiffusionNet FromWeights(DiffusionConfig config, IReadOnlyList<Tensor> weights)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(weights);
        CheckConfig(config);
        var shapes = ParameterShapes(config);
        if (weights.Count != Names.Length)
            throw new ArgumentException($"A diffusion network has {Names.Length} weight tensors, not {weights.Count}.", nameof(weights));
        for (int i = 0; i < Names.Length; i++)
            if (weights[i] is null || !weights[i].Shape.SequenceEqual(shapes[i]))
                throw new ArgumentException($"The tensor for '{Names[i]}' needs shape [{string.Join(", ", shapes[i])}].", nameof(weights));
        return new DiffusionNet(config, Names.Select((n, i) => new Parameter(n, weights[i])).ToArray());
    }

    public DiffusionConfig Config { get; }

    public override IEnumerable<Parameter> Parameters() => _parameters;

    /// <summary>sin then cos of (t / scheduleSteps) * pi * 2^k for k = 0 .. count/2 - 1.</summary>
    public static double[] TimeFeatures(int t, int scheduleSteps, int count)
    {
        int frequencies = count / 2;
        var features = new double[count];
        double phase = (double)t / scheduleSteps * Math.PI;
        for (int k = 0; k < frequencies; k++)
        {
            double angle = phase * Math.Pow(2, k);
            features[k] = Math.Sin(angle);
            features[frequencies + k] = Math.Cos(angle);
        }
        return features;
    }

    /// <summary>
    /// The predicted noise [N,2] for noisy points [N,2], where point n is at original step <paramref name="t"/>[n]
    /// (0..scheduleSteps) of a schedule of <paramref name="scheduleSteps"/> steps.
    /// </summary>
    public Tensor Forward(Tensor points, IReadOnlyList<int> t, int scheduleSteps)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(t);
        ArgumentOutOfRangeException.ThrowIfLessThan(scheduleSteps, 1);
        if (points.Shape.Length != 2 || points.Shape[1] != 2 || points.Shape[0] < 1)
            throw new ArgumentException("The points must be a tensor [N, 2] with at least one point.", nameof(points));
        int n = points.Shape[0];
        if (t.Count != n) throw new ArgumentException($"Give one step for each of the {n} points, not {t.Count}.", nameof(t));

        int f = Config.TimeFeatures;
        var features = new double[n * f];
        for (int r = 0; r < n; r++)
        {
            if (t[r] < 0 || t[r] > scheduleSteps)
                throw new ArgumentOutOfRangeException(nameof(t), $"Step {t[r]} is outside the schedule of {scheduleSteps} steps.");
            Array.Copy(TimeFeatures(t[r], scheduleSteps, f), 0, features, r * f, f);
        }

        var input = Ops.ConcatColumns(new[] { points, Tensor.FromArray(features, n, f) });
        var h1 = Ops.Relu(Ops.Add(Ops.MatMul(input, _parameters[0].Value), _parameters[1].Value));
        var h2 = Ops.Relu(Ops.Add(Ops.MatMul(h1, _parameters[2].Value), _parameters[3].Value));
        return Ops.Add(Ops.MatMul(h2, _parameters[4].Value), _parameters[5].Value);
    }

    /// <summary>The settings a diffusion file stores: the network, the schedule and the shape it learned.</summary>
    private sealed record FileConfig(int Hidden, int TimeFeatures, int OriginalSteps, double BetaStart, double BetaEnd, int Steps, string? ShapeName);

    /// <summary>The longest shape name a file may hold.</summary>
    public const int MaxShapeNameLength = 100;

    /// <summary>
    /// Saves as a version 2 family file of kind <see cref="FamilyKinds.Diffusion"/>. The config holds the network's sizes,
    /// the schedule (the full schedule's steps and betas, and the number of steps, so a respaced schedule comes back
    /// respaced) and the name of the shape it learned.
    /// </summary>
    public void Save(Stream s, NoiseSchedule schedule, string shapeName, ModelInfo info)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(shapeName);
        ArgumentNullException.ThrowIfNull(info);
        if (string.IsNullOrWhiteSpace(shapeName) || shapeName.Length > MaxShapeNameLength)
            throw new ArgumentException($"The shape needs a name of 1 to {MaxShapeNameLength} characters.", nameof(shapeName));
        var config = new FileConfig(Config.Hidden, Config.TimeFeatures, schedule.OriginalSteps, schedule.BetaStart, schedule.BetaEnd,
            schedule.Steps, shapeName);
        FamilyModelFile.Save(s, FamilyKinds.Diffusion, config, null, info, _parameters);
    }

    /// <summary>Reads a diffusion network. Anything wrong with the file throws <see cref="InvalidDataException"/> with a plain message.</summary>
    public static (DiffusionNet Net, NoiseSchedule Schedule, string ShapeName, ModelInfo Info) Load(Stream s)
    {
        var data = FamilyModelFile.Load(s);
        if (data.Kind != FamilyKinds.Diffusion)
            throw new InvalidDataException($"This file holds {FamilyKinds.FriendlyName(data.Kind)}, not a diffusion network.");

        FileConfig? stored;
        try { stored = data.Config.Deserialize<FileConfig>(ModelContainer.Json); }
        catch (JsonException) { throw new InvalidDataException("The model file's settings for the diffusion network cannot be read."); }
        if (stored is null) throw new InvalidDataException("The model file's settings for the diffusion network cannot be read.");

        var config = new DiffusionConfig(stored.Hidden, stored.TimeFeatures);
        if (config.Hidden < 1 || config.Hidden > ModelContainer.MaxDimension || config.TimeFeatures < 2
            || config.TimeFeatures > DiffusionConfig.MaxTimeFeatures || config.TimeFeatures % 2 != 0
            || config.CountParameters() > ModelContainer.MaxParameters)
            throw new InvalidDataException("The model file describes a diffusion network with an impossible shape.");
        if (string.IsNullOrWhiteSpace(stored.ShapeName) || stored.ShapeName.Length > MaxShapeNameLength)
            throw new InvalidDataException("The model file does not name the shape the network learned.");

        NoiseSchedule schedule;
        try
        {
            var full = new NoiseSchedule(stored.OriginalSteps, stored.BetaStart, stored.BetaEnd);
            schedule = stored.Steps == full.Steps ? full : full.Respaced(stored.Steps);
        }
        catch (ArgumentOutOfRangeException) { throw new InvalidDataException("The model file describes a noise schedule that cannot be used."); }

        var shapes = ParameterShapes(config);
        if (data.Parameters.Count != Names.Length)
            throw new InvalidDataException($"The model file lists {data.Parameters.Count} parameters but a diffusion network has {Names.Length}.");
        var weights = new Tensor[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            var (name, shape, values) = data.Parameters[i];
            if (name != Names[i] || !shape.SequenceEqual(shapes[i]))
                throw new InvalidDataException($"The model file's parameter {i + 1} ('{name}') does not match what this diffusion network needs ('{Names[i]}').");
            weights[i] = Tensor.FromArray(values, shape).AsParameter();
        }
        return (FromWeights(config, weights), schedule, stored.ShapeName, data.Info);
    }

    private static int[][] ParameterShapes(DiffusionConfig c) => new[]
    {
        new[] { c.InputWidth, c.Hidden }, new[] { c.Hidden }, new[] { c.Hidden, c.Hidden }, new[] { c.Hidden },
        new[] { c.Hidden, 2 }, new[] { 2 },
    };

    private static void CheckConfig(DiffusionConfig config)
    {
        if (config.Hidden < 1) throw new ArgumentException("The network needs at least one hidden unit.", nameof(config));
        if (config.TimeFeatures < 2 || config.TimeFeatures % 2 != 0 || config.TimeFeatures > DiffusionConfig.MaxTimeFeatures)
            throw new ArgumentException($"The time features come in sin and cos pairs: give an even number from 2 to {DiffusionConfig.MaxTimeFeatures}.", nameof(config));
    }
}
