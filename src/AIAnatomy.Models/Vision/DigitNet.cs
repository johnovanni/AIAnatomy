using System.Text.Json;
using System.Text.Json.Serialization;
using AIAnatomy.Core;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Vision;

/// <summary>
/// The shape of the image network: the side of its square input images (divisible by 4, since it is halved twice), the
/// filters in each convolution layer and the width of the fully connected hidden layer. Every convolution is 3x3 with
/// padding 1, so it keeps the image size; each is followed by ReLU and 2x2 max pooling.
/// </summary>
public sealed record DigitNetConfig(int ImageSize = 28, int Filters1 = 8, int Filters2 = 16, int Hidden = 64)
{
    public const int Kernel = 3, Padding = 1, Pool = 2, Classes = 10;

    /// <summary>The numbers fed to the first fully connected layer: Filters2 maps of (ImageSize / 4) squared.</summary>
    [JsonIgnore]
    public int Flattened => Filters2 * (ImageSize / 4) * (ImageSize / 4);

    /// <summary>Total number of learnable numbers; equals the summed size of <c>DigitNet.Parameters()</c>.</summary>
    public long CountParameters()
    {
        long k2 = Kernel * Kernel, f1 = Filters1, f2 = Filters2, flat = Flattened, h = Hidden;
        return f1 * k2 + f1 + f2 * f1 * k2 + f2 + flat * h + h + h * Classes + Classes;
    }
}

/// <summary>
/// Everything one forward pass computed, for the lab to show. Conv1 and Conv2 are the feature maps after ReLU,
/// [N, Filters, S, S] and [N, Filters2, S/2, S/2]; Pool1 and Pool2 are after max pooling; Hidden is after ReLU, [N, Hidden];
/// Logits is [N, 10].
/// </summary>
public sealed record DigitTrace(Tensor Conv1, Tensor Pool1, Tensor Conv2, Tensor Pool2, Tensor Hidden, Tensor Logits)
{
    /// <summary>The softmax of image <paramref name="image"/>'s logits: its probability for each digit 0-9.</summary>
    public double[] Probabilities(int image)
    {
        int n = Logits.Shape[0], classes = Logits.Shape[1];
        if ((uint)image >= (uint)n) throw new ArgumentOutOfRangeException(nameof(image), $"Image {image} is outside 0..{n - 1}.");
        var result = new double[classes];
        double max = double.NegativeInfinity;
        for (int j = 0; j < classes; j++) max = Math.Max(max, Logits.Data[image * classes + j]);
        double sum = 0;
        for (int j = 0; j < classes; j++) sum += result[j] = Math.Exp(Logits.Data[image * classes + j] - max);
        for (int j = 0; j < classes; j++) result[j] /= sum;
        return result;
    }
}

/// <summary>
/// A small convolutional network for digits, in the style of LeCun et al. (1998):
/// conv 3x3 (Filters1) -> ReLU -> max pool 2x2 -> conv 3x3 (Filters2) -> ReLU -> max pool 2x2 -> flatten
/// -> fully connected (Hidden) -> ReLU -> fully connected (10 logits).
/// A filter's weights are one row [C*3*3] of its layer's matrix (channel, then row, then column), as <see cref="Ops.Conv2d"/>
/// takes them; the fully connected weights are [in, out], used as x W + b.
/// </summary>
public sealed class DigitNet : Module
{
    public const string Conv1Name = "conv1.weight", Conv1BiasName = "conv1.bias", Conv2Name = "conv2.weight",
        Conv2BiasName = "conv2.bias", Fc1Name = "fc1.weight", Fc1BiasName = "fc1.bias", Fc2Name = "fc2.weight", Fc2BiasName = "fc2.bias";

    private static readonly string[] Names = { Conv1Name, Conv1BiasName, Conv2Name, Conv2BiasName, Fc1Name, Fc1BiasName, Fc2Name, Fc2BiasName };

    private readonly Parameter[] _parameters;

    /// <summary>
    /// Starting weights: He initialisation (He et al. 2015), normal with standard deviation sqrt(2 / fan-in), for every
    /// weight matrix (fan-in 9 for conv1, Filters1*9 for conv2, Flattened for fc1, Hidden for fc2); biases 0.
    /// </summary>
    public DigitNet(DigitNetConfig config, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rng);
        CheckConfig(config);
        Config = config;
        var shapes = ParameterShapes(config);
        _parameters = new Parameter[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            var shape = shapes[i];
            _parameters[i] = new Parameter(Names[i], shape.Length == 1
                ? Tensor.Zeros(shape).AsParameter()
                : Tensor.RandomNormal(rng, Math.Sqrt(2.0 / FanIn(shape, i)), shape).AsParameter());
        }
    }

    private DigitNet(DigitNetConfig config, Parameter[] parameters)
    {
        Config = config;
        _parameters = parameters;
    }

    /// <summary>A convolution's fan-in is its row length (C*3*3); a fully connected layer's is its number of rows.</summary>
    private static int FanIn(int[] shape, int index) => index < 4 ? shape[1] : shape[0];

    /// <summary>
    /// A network that uses the given tensors as its parameters (not copies), in <see cref="Parameters"/> order. Used for
    /// gradient checks and when loading a file. Shapes must match the config.
    /// </summary>
    public static DigitNet FromWeights(DigitNetConfig config, IReadOnlyList<Tensor> weights)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(weights);
        CheckConfig(config);
        var shapes = ParameterShapes(config);
        if (weights.Count != Names.Length)
            throw new ArgumentException($"An image network has {Names.Length} weight tensors, not {weights.Count}.", nameof(weights));
        for (int i = 0; i < Names.Length; i++)
            if (weights[i] is null || !weights[i].Shape.SequenceEqual(shapes[i]))
                throw new ArgumentException($"The tensor for '{Names[i]}' needs shape [{string.Join(", ", shapes[i])}].", nameof(weights));
        return new DigitNet(config, Names.Select((n, i) => new Parameter(n, weights[i])).ToArray());
    }

    public DigitNetConfig Config { get; }

    public override IEnumerable<Parameter> Parameters() => _parameters;

    /// <summary>Runs images [N, 1, S, S] (values 0..1, S = ImageSize) through the network, keeping every stage.</summary>
    public DigitTrace Forward(Tensor images)
    {
        ArgumentNullException.ThrowIfNull(images);
        int s = Config.ImageSize;
        if (images.Shape.Length != 4 || images.Shape[0] < 1 || images.Shape[1] != 1 || images.Shape[2] != s || images.Shape[3] != s)
            throw new ArgumentException($"The images must be a tensor [N, 1, {s}, {s}] with at least one image.", nameof(images));
        int n = images.Shape[0];
        var w = _parameters;
        var conv1 = Ops.Relu(Ops.Conv2d(images, w[0].Value, w[1].Value, DigitNetConfig.Kernel, DigitNetConfig.Padding));
        var pool1 = Ops.MaxPool2d(conv1, DigitNetConfig.Pool);
        var conv2 = Ops.Relu(Ops.Conv2d(pool1, w[2].Value, w[3].Value, DigitNetConfig.Kernel, DigitNetConfig.Padding));
        var pool2 = Ops.MaxPool2d(conv2, DigitNetConfig.Pool);
        var flat = Ops.Reshape(pool2, n, Config.Flattened);
        var hidden = Ops.Relu(Ops.Add(Ops.MatMul(flat, w[4].Value), w[5].Value));
        var logits = Ops.Add(Ops.MatMul(hidden, w[6].Value), w[7].Value);
        return new DigitTrace(conv1, pool1, conv2, pool2, hidden, logits);
    }

    /// <summary>Saves as a version 2 family file of kind <see cref="FamilyKinds.Digits"/>; the config is the <see cref="DigitNetConfig"/>.</summary>
    public void Save(Stream s, ModelInfo info)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(info);
        FamilyModelFile.Save(s, FamilyKinds.Digits, Config, null, info, _parameters);
    }

    /// <summary>Reads an image network. Anything wrong with the file throws <see cref="InvalidDataException"/> with a plain message.</summary>
    public static (DigitNet Net, ModelInfo Info) Load(Stream s)
    {
        var data = FamilyModelFile.Load(s);
        if (data.Kind != FamilyKinds.Digits)
            throw new InvalidDataException($"This file holds {FamilyKinds.FriendlyName(data.Kind)}, not an image network.");

        DigitNetConfig? config;
        try { config = data.Config.Deserialize<DigitNetConfig>(ModelContainer.Json); }
        catch (JsonException) { throw new InvalidDataException("The model file's settings for the image network cannot be read."); }
        if (config is null || !IsPossible(config))
            throw new InvalidDataException("The model file describes an image network with an impossible shape.");

        var shapes = ParameterShapes(config);
        if (data.Parameters.Count != Names.Length)
            throw new InvalidDataException($"The model file lists {data.Parameters.Count} parameters but an image network has {Names.Length}.");
        var weights = new Tensor[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            var (name, shape, values) = data.Parameters[i];
            if (name != Names[i] || !shape.SequenceEqual(shapes[i]))
                throw new InvalidDataException($"The model file's parameter {i + 1} ('{name}') does not match what this image network needs ('{Names[i]}').");
            weights[i] = Tensor.FromArray(values, shape).AsParameter();
        }
        return (FromWeights(config, weights), data.Info);
    }

    private static bool IsPossible(DigitNetConfig c) =>
        c.ImageSize >= 4 && c.ImageSize % 4 == 0 && c.ImageSize <= 1024
        && c.Filters1 >= 1 && c.Filters2 >= 1 && c.Hidden >= 1
        && c.Filters1 <= ModelContainer.MaxDimension && c.Filters2 <= ModelContainer.MaxDimension && c.Hidden <= ModelContainer.MaxDimension
        && (long)c.Filters2 * (c.ImageSize / 4) * (c.ImageSize / 4) <= ModelContainer.MaxDimension
        && c.CountParameters() <= ModelContainer.MaxParameters;

    private static int[][] ParameterShapes(DigitNetConfig c)
    {
        int k2 = DigitNetConfig.Kernel * DigitNetConfig.Kernel;
        return new[]
        {
            new[] { c.Filters1, k2 }, new[] { c.Filters1 },
            new[] { c.Filters2, c.Filters1 * k2 }, new[] { c.Filters2 },
            new[] { c.Flattened, c.Hidden }, new[] { c.Hidden },
            new[] { c.Hidden, DigitNetConfig.Classes }, new[] { DigitNetConfig.Classes },
        };
    }

    private static void CheckConfig(DigitNetConfig config)
    {
        if (config.ImageSize < 4 || config.ImageSize % 4 != 0)
            throw new ArgumentException($"The image size must be a multiple of 4 (it is halved twice), not {config.ImageSize}.", nameof(config));
        if (config.Filters1 < 1 || config.Filters2 < 1) throw new ArgumentException("Each convolution layer needs at least one filter.", nameof(config));
        if (config.Hidden < 1) throw new ArgumentException("The hidden layer needs at least one unit.", nameof(config));
    }
}
