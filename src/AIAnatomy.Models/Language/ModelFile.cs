using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAnatomy.Core;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Language;

/// <summary>
/// The .aimodel format: one UTF-8 JSON header line, "\n", then every parameter as little-endian 64-bit floats
/// in <c>Transformer.Parameters()</c> order. The header lists each parameter's name and shape so a file can be checked before use.
/// </summary>
public static class ModelFile
{
    private const string FormatName = "aianatomy-model";
    private const int Version = 1;
    private const int MaxHeaderBytes = 4 * 1024 * 1024;
    private const int MaxDimension = 100_000;
    private const long MaxParameters = 50_000_000;

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record ParameterEntry(string Name, int[] Shape);

    private sealed record Header(
        string? Format, int Version, TransformerConfig? Config, string? Vocabulary, ModelInfo? Info, List<ParameterEntry>? Parameters);

    public static void Save(LanguageModel m, Stream s)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(s);
        var parameters = m.Transformer.Parameters().ToList();
        var header = new Header(
            FormatName, Version, m.Transformer.Config, new string(m.Tokenizer.Vocabulary.ToArray()), m.Info,
            parameters.Select(p => new ParameterEntry(p.Name, p.Value.Shape)).ToList());
        s.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header, Json) + "\n"));

        var buffer = new byte[8 * 4096];
        foreach (var p in parameters)
        {
            var data = p.Value.Data;
            for (int offset = 0; offset < data.Length; offset += 4096)
            {
                int count = Math.Min(4096, data.Length - offset);
                for (int i = 0; i < count; i++) BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(i * 8), data[offset + i]);
                s.Write(buffer, 0, count * 8);
            }
        }
    }

    /// <summary>Rebuilds a model. Anything wrong with the file throws <see cref="InvalidDataException"/> with a plain message.</summary>
    public static LanguageModel Load(Stream s)
    {
        ArgumentNullException.ThrowIfNull(s);
        try
        {
            return LoadCore(s);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("The model file is cut short: some of its numbers are missing.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("The model file does not start with a readable header.");
        }
        catch (Exception e) when (e is ArgumentException or OverflowException or NotSupportedException)
        {
            throw new InvalidDataException("The model file describes a model that cannot be built: " + e.Message);
        }
    }

    private static LanguageModel LoadCore(Stream s)
    {
        var header = ReadHeader(ReadHeaderLine(s));
        var config = header.Config!;
        CheckConfigSize(config);

        var tokenizer = CharTokenizer.FromVocabulary(header.Vocabulary!.ToCharArray());
        if (config.VocabularySize != tokenizer.VocabularySize)
            throw new InvalidDataException($"The model file lists {config.VocabularySize} tokens but its vocabulary has {tokenizer.VocabularySize}.");

        var transformer = new Transformer(config, new SeededRandom(0));
        var parameters = transformer.Parameters().ToList();
        var listed = header.Parameters!;
        if (listed.Count != parameters.Count)
            throw new InvalidDataException($"The model file lists {listed.Count} parameters but this model shape has {parameters.Count}.");
        for (int i = 0; i < parameters.Count; i++)
        {
            if (listed[i].Name != parameters[i].Name || listed[i].Shape is null || !listed[i].Shape.SequenceEqual(parameters[i].Value.Shape))
                throw new InvalidDataException($"The model file's parameter {i + 1} ('{listed[i].Name}') does not match what this model shape needs ('{parameters[i].Name}').");
        }

        var buffer = new byte[8 * 4096];
        foreach (var p in parameters)
        {
            var data = p.Value.Data;
            for (int offset = 0; offset < data.Length; offset += 4096)
            {
                int count = Math.Min(4096, data.Length - offset);
                s.ReadExactly(buffer, 0, count * 8);
                for (int i = 0; i < count; i++)
                {
                    double value = BinaryPrimitives.ReadDoubleLittleEndian(buffer.AsSpan(i * 8));
                    if (!double.IsFinite(value)) throw new InvalidDataException($"The model file has a number that is not valid in '{p.Name}'.");
                    data[offset + i] = value;
                }
            }
        }
        if (s.ReadByte() != -1) throw new InvalidDataException("The model file has extra data after its numbers.");

        return new LanguageModel(transformer, tokenizer, header.Info!);
    }

    private static byte[] ReadHeaderLine(Stream s)
    {
        var bytes = new List<byte>();
        while (true)
        {
            int b = s.ReadByte();
            if (b < 0) throw new InvalidDataException("The model file ends before its header does.");
            if (b == '\n') return bytes.ToArray();
            bytes.Add((byte)b);
            if (bytes.Count > MaxHeaderBytes) throw new InvalidDataException("The model file does not start with a readable header.");
        }
    }

    private static Header ReadHeader(byte[] line)
    {
        var header = JsonSerializer.Deserialize<Header>(line, Json)
            ?? throw new InvalidDataException("The model file does not start with a readable header.");
        if (header.Format != FormatName) throw new InvalidDataException("This is not an AI Anatomy model file.");
        if (header.Version != Version) throw new InvalidDataException($"This model file has version {header.Version}, but this program reads version {Version}.");
        if (header.Config is null || header.Vocabulary is null || header.Info is null || header.Parameters is null || header.Info.Name is null || header.Info.TrainingTextName is null)
            throw new InvalidDataException("The model file's header is missing required information.");
        return header;
    }

    private static void CheckConfigSize(TransformerConfig c)
    {
        int[] sizes = { c.VocabularySize, c.ContextLength, c.ModelWidth, c.Layers, c.Heads, c.FeedForwardWidth };
        if (sizes.Any(v => v < 0 || v > MaxDimension) || c.CountParameters() > MaxParameters)
            throw new InvalidDataException("The model file describes a model that is too large to open.");
    }
}
