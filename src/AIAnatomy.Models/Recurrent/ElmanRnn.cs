using System.Text.Json;
using AIAnatomy.Core;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Recurrent;

/// <summary>The shape of a memory network: the vocabulary it reads and writes, and the size of its memory (hidden state).</summary>
public sealed record RnnConfig(int VocabularySize, int Hidden = 128)
{
    /// <summary>Total number of learnable numbers; equals the summed size of <c>ElmanRnn.Parameters()</c>.</summary>
    public long CountParameters()
    {
        long v = VocabularySize, h = Hidden;
        return v * h + h * h + h + h * v + v;
    }
}

/// <summary>
/// One forward pass. <c>Hidden[t]</c> is the memory after reading position t, [B,H] (row b is sequence b).
/// <c>Logits</c> is [T*B, V] in position-major order: row t*B + b is sequence b's prediction after position t.
/// </summary>
public sealed record RnnForward(IReadOnlyList<Tensor> Hidden, Tensor Logits);

/// <summary>
/// A character-level Elman network (Elman 1990): h_t = tanh(x_t W_in + h_{t-1} W_rec + b), with h_0 = 0, and
/// logits_t = h_t W_out + b_out. x_t is the one-hot vector of character t, so x_t W_in is just row t of W_in.
/// The memory h is the only thing carried from one character to the next.
/// </summary>
public sealed class ElmanRnn : Module
{
    public const string InputName = "input.weight", RecurrentName = "recurrent.weight", BiasName = "hidden.bias",
        OutputName = "output.weight", OutputBiasName = "output.bias";

    private readonly Parameter[] _parameters;

    /// <summary>
    /// Starting weights: the input rows have standard deviation 1 (a one-hot input has a fan-in of 1), the recurrent and
    /// output weights 1/sqrt(H) (fan-in H), biases 0. A random H by H matrix with that spread has its eigenvalues spread
    /// over a disc of radius about 1, so the memory neither dies away nor blows up at the start.
    /// </summary>
    public ElmanRnn(RnnConfig config, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rng);
        CheckConfig(config);
        Config = config;
        int v = config.VocabularySize, h = config.Hidden;
        double fanInH = 1 / Math.Sqrt(h);
        _parameters = new[]
        {
            new Parameter(InputName, Tensor.RandomNormal(rng, 1.0, v, h).AsParameter()),
            new Parameter(RecurrentName, Tensor.RandomNormal(rng, fanInH, h, h).AsParameter()),
            new Parameter(BiasName, Tensor.Zeros(h).AsParameter()),
            new Parameter(OutputName, Tensor.RandomNormal(rng, fanInH, h, v).AsParameter()),
            new Parameter(OutputBiasName, Tensor.Zeros(v).AsParameter()),
        };
    }

    private ElmanRnn(RnnConfig config, Parameter[] parameters)
    {
        Config = config;
        _parameters = parameters;
    }

    /// <summary>
    /// A network that uses the given tensors as its parameters (not copies), in <see cref="Parameters"/> order. Used for
    /// gradient checks and when loading a file. Shapes must match the config.
    /// </summary>
    public static ElmanRnn FromWeights(RnnConfig config, IReadOnlyList<Tensor> weights)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(weights);
        CheckConfig(config);
        var names = new[] { InputName, RecurrentName, BiasName, OutputName, OutputBiasName };
        var shapes = Shapes(config);
        if (weights.Count != names.Length)
            throw new ArgumentException($"A memory network has {names.Length} weight tensors, not {weights.Count}.", nameof(weights));
        for (int i = 0; i < names.Length; i++)
            if (weights[i] is null || !weights[i].Shape.SequenceEqual(shapes[i]))
                throw new ArgumentException($"The tensor for '{names[i]}' needs shape [{string.Join(", ", shapes[i])}].", nameof(weights));
        return new ElmanRnn(config, names.Select((n, i) => new Parameter(n, weights[i])).ToArray());
    }

    public RnnConfig Config { get; }

    public Parameter InputWeights => _parameters[0];
    public Parameter RecurrentWeights => _parameters[1];
    public Parameter HiddenBias => _parameters[2];
    public Parameter OutputWeights => _parameters[3];
    public Parameter OutputBias => _parameters[4];

    public override IEnumerable<Parameter> Parameters() => _parameters;

    /// <summary>Runs B sequences of equal length T side by side, every one starting from an empty memory.</summary>
    public RnnForward Forward(IReadOnlyList<IReadOnlyList<int>> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        if (sequences.Count == 0) throw new ArgumentException("Give at least one sequence.", nameof(sequences));
        int length = sequences[0]?.Count ?? 0;
        if (length == 0) throw new ArgumentException("A sequence needs at least one token.", nameof(sequences));
        if (sequences.Any(s => s is null || s.Count != length))
            throw new ArgumentException("All the sequences must have the same length.", nameof(sequences));

        int batch = sequences.Count;
        var hidden = new List<Tensor>(length);
        Tensor? h = null;
        var ids = new int[batch];
        for (int t = 0; t < length; t++)
        {
            for (int b = 0; b < batch; b++) ids[b] = sequences[b][t];
            h = Step(h, ids);
            hidden.Add(h);
        }

        // Stack the [B,H] memories into [T*B, H], position-major: transpose each, place side by side, transpose back.
        var stacked = length == 1 ? hidden[0] : Ops.Transpose(Ops.ConcatColumns(hidden.Select(Ops.Transpose).ToArray()));
        var logits = Ops.Add(Ops.MatMul(stacked, OutputWeights.Value), OutputBias.Value);
        return new RnnForward(hidden, logits);
    }

    /// <summary>
    /// One step for B sequences: the new memory [B,H] from the previous one (null for the empty starting memory h_0 = 0)
    /// and each sequence's next token.
    /// </summary>
    public Tensor Step(Tensor? previous, IReadOnlyList<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (int id in ids)
            if (id < 0 || id >= Config.VocabularySize)
                throw new ArgumentOutOfRangeException(nameof(ids), $"Token {id} is outside the vocabulary of {Config.VocabularySize}.");
        if (previous is not null && (previous.Shape.Length != 2 || previous.Shape[0] != ids.Count || previous.Shape[1] != Config.Hidden))
            throw new ArgumentException($"The previous memory must be [{ids.Count}, {Config.Hidden}].", nameof(previous));

        var input = Ops.Rows(InputWeights.Value, ids);   // x_t W_in for one-hot x_t
        var pre = previous is null ? input : Ops.Add(input, Ops.MatMul(previous, RecurrentWeights.Value));
        return Ops.Tanh(Ops.Add(pre, HiddenBias.Value));
    }

    /// <summary>The logits [B,V] for memories [B,H].</summary>
    public Tensor Logits(Tensor hidden) => Ops.Add(Ops.MatMul(hidden, OutputWeights.Value), OutputBias.Value);

    /// <summary>Saves as a version 2 family file of kind <see cref="FamilyKinds.Memory"/>, with the tokenizer's vocabulary.</summary>
    public void Save(Stream s, CharTokenizer tokenizer, ModelInfo info)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(info);
        if (tokenizer.VocabularySize != Config.VocabularySize)
            throw new ArgumentException($"The network has {Config.VocabularySize} tokens but the tokenizer has {tokenizer.VocabularySize}.", nameof(tokenizer));
        FamilyModelFile.Save(s, FamilyKinds.Memory, Config, new string(tokenizer.Vocabulary.ToArray()), info, _parameters);
    }

    /// <summary>Reads a memory network. Anything wrong with the file throws <see cref="InvalidDataException"/> with a plain message.</summary>
    public static (ElmanRnn Rnn, CharTokenizer Tokenizer, ModelInfo Info) Load(Stream s)
    {
        var data = FamilyModelFile.Load(s);
        if (data.Kind != FamilyKinds.Memory)
            throw new InvalidDataException($"This file holds {FamilyKinds.FriendlyName(data.Kind)}, not a memory network.");

        RnnConfig? config;
        try { config = data.Config.Deserialize<RnnConfig>(ModelContainer.Json); }
        catch (JsonException) { throw new InvalidDataException("The model file's settings for the memory network cannot be read."); }
        if (config is null || config.VocabularySize < 1 || config.Hidden < 1
            || config.VocabularySize > ModelContainer.MaxDimension || config.Hidden > ModelContainer.MaxDimension
            || config.CountParameters() > ModelContainer.MaxParameters)
            throw new InvalidDataException("The model file describes a memory network with an impossible shape.");
        if (data.Vocabulary is null) throw new InvalidDataException("The model file has no vocabulary.");

        CharTokenizer tokenizer;
        try { tokenizer = CharTokenizer.FromVocabulary(data.Vocabulary.ToCharArray()); }
        catch (ArgumentException) { throw new InvalidDataException("The model file's vocabulary lists a character twice."); }
        if (tokenizer.VocabularySize != config.VocabularySize)
            throw new InvalidDataException($"The model file lists {config.VocabularySize} tokens but its vocabulary has {tokenizer.VocabularySize}.");

        var names = new[] { InputName, RecurrentName, BiasName, OutputName, OutputBiasName };
        var shapes = Shapes(config);
        if (data.Parameters.Count != names.Length)
            throw new InvalidDataException($"The model file lists {data.Parameters.Count} parameters but a memory network has {names.Length}.");
        var weights = new Tensor[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var (name, shape, values) = data.Parameters[i];
            if (name != names[i] || !shape.SequenceEqual(shapes[i]))
                throw new InvalidDataException($"The model file's parameter {i + 1} ('{name}') does not match what this memory network needs ('{names[i]}').");
            weights[i] = Tensor.FromArray(values, shape).AsParameter();
        }
        return (FromWeights(config, weights), tokenizer, data.Info);
    }

    private static int[][] Shapes(RnnConfig c) => new[]
    {
        new[] { c.VocabularySize, c.Hidden }, new[] { c.Hidden, c.Hidden }, new[] { c.Hidden },
        new[] { c.Hidden, c.VocabularySize }, new[] { c.VocabularySize },
    };

    private static void CheckConfig(RnnConfig config)
    {
        if (config.VocabularySize < 1) throw new ArgumentException("The vocabulary needs at least one token.", nameof(config));
        if (config.Hidden < 1) throw new ArgumentException("The memory needs at least one number.", nameof(config));
    }
}
