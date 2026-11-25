using AIAnatomy.Core;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Transformers;
using static AIAnatomy.Models.Files.ModelContainer;

namespace AIAnatomy.Models.Language;

/// <summary>
/// The .aimodel format, version 1 (language models): one UTF-8 JSON header line, "\n", then every parameter as
/// little-endian 64-bit floats in <c>Transformer.Parameters()</c> order. The header lists each parameter's name and shape
/// so a file can be checked before use. The shared parts live in <see cref="ModelContainer"/>; a version 2 file (a family
/// network, read by <see cref="FamilyModelFile"/>) is refused here with a message naming what it holds.
/// </summary>
public static class ModelFile
{
    private const int Version = LanguageVersion;

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
        WriteHeader(s, header);
        WriteValues(s, parameters.Select(p => p.Value.Data));
    }

    /// <summary>Rebuilds a model. Anything wrong with the file throws <see cref="InvalidDataException"/> with a plain message.</summary>
    public static LanguageModel Load(Stream s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Read(() => LoadCore(s));
    }

    private static LanguageModel LoadCore(Stream s)
    {
        var header = ReadLanguageHeader(ReadHeaderLine(s));
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
        if (listed.Any(entry => entry is null || entry.Name is null || entry.Shape is null))
            throw new InvalidDataException("The model file's parameter list has an incomplete entry.");
        for (int i = 0; i < parameters.Count; i++)
        {
            if (listed[i].Name != parameters[i].Name || listed[i].Shape is null || !listed[i].Shape.SequenceEqual(parameters[i].Value.Shape))
                throw new InvalidDataException($"The model file's parameter {i + 1} ('{listed[i].Name}') does not match what this model shape needs ('{parameters[i].Name}').");
        }

        var buffer = NewBuffer();
        foreach (var p in parameters) ReadValues(s, p.Name, p.Value.Data, buffer);
        ExpectEnd(s);

        return new LanguageModel(transformer, tokenizer, header.Info!);
    }

    private static Header ReadLanguageHeader(byte[] line)
    {
        var preamble = ReadPreamble(line);
        // A version 2 header without a kind is not a family file either, so it gets the plain version message.
        if (preamble.Version == FamilyVersion && !string.IsNullOrWhiteSpace(preamble.Kind))
            throw new InvalidDataException($"This file holds a {preamble.Kind}, not a language model.");
        if (preamble.Version != Version) throw new InvalidDataException(VersionMessage(preamble.Version, Version));
        var header = ReadHeader<Header>(line);
        if (header.Config is null || header.Vocabulary is null || header.Info is null || header.Parameters is null || header.Info.Name is null || header.Info.TrainingTextName is null)
            throw new InvalidDataException("The model file's header is missing required information.");
        return header;
    }

    private static void CheckConfigSize(TransformerConfig c)
    {
        // Every size is checked before CountParameters runs, because it divides by the number of heads.
        int[] positive = { c.VocabularySize, c.ContextLength, c.ModelWidth, c.Heads, c.FeedForwardWidth };
        if (positive.Any(v => v < 1) || c.Layers < 0)
            throw new InvalidDataException("The model file describes a model with an impossible shape.");
        if (c.ModelWidth % c.Heads != 0)
            throw new InvalidDataException($"The model file's width {c.ModelWidth} does not divide evenly into {c.Heads} heads.");
        if (positive.Any(v => v > MaxDimension) || c.Layers > MaxDimension || c.CountParameters() > MaxParameters)
            throw new InvalidDataException("The model file describes a model that is too large to open.");
    }
}
