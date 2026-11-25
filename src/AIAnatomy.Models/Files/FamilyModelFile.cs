using System.Text.Json;
using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using static AIAnatomy.Models.Files.ModelContainer;

namespace AIAnatomy.Models.Files;

/// <summary>The kinds of family network a version 2 file can hold.</summary>
public static class FamilyKinds
{
    public const string Digits = "digit-network", Memory = "memory-network", Diffusion = "diffusion-network";

    /// <summary>
    /// Plain words for a kind, with its article, for messages a learner reads ("an image network"). Any kind not listed
    /// here, including odd or very long stored text, gives "a different kind of network", so a file's own text is never shown.
    /// </summary>
    public static string FriendlyName(string kind) => kind switch
    {
        Digits => "an image network",
        Memory => "a memory network",
        Diffusion => "a diffusion network",
        _ => "a different kind of network",
    };
}

/// <summary>
/// What a version 2 file holds. <c>Config</c> is left as JSON so each network type reads its own config record;
/// parameters keep the file's order, names and shapes.
/// </summary>
public sealed record FamilyModelData(string Kind, JsonElement Config, string? Vocabulary, ModelInfo Info,
    IReadOnlyList<(string Name, int[] Shape, double[] Values)> Parameters);

/// <summary>
/// The .aimodel format, version 2, for the family networks (image, memory, diffusion). Same container as version 1
/// (D14): one JSON header line <c>{ format, version: 2, kind, config, vocabulary, info, parameters: [{name, shape}] }</c>,
/// then every parameter's numbers as little-endian 64-bit floats in the listed order. This class only checks the
/// container; each network type checks that the kind, config, names and shapes are the ones it needs.
/// Family files live in <c>content/models/family/</c>, which <see cref="ModelLibrary"/> does not list.
/// </summary>
public static class FamilyModelFile
{
    public const int Version = FamilyVersion;

    private sealed record Header(
        string? Format, int Version, string? Kind, JsonElement? Config, string? Vocabulary, ModelInfo? Info, List<ParameterEntry>? Parameters);

    /// <summary>
    /// Writes a family network. Bad arguments (blank kind, a config that is not a JSON object, missing names, a
    /// parameter name used twice) throw <see cref="ArgumentException"/>; a number that is not finite throws
    /// <see cref="InvalidOperationException"/> with <see cref="ModelLibrary.NotFiniteMessage"/>. Nothing is written then.
    /// </summary>
    public static void Save(Stream s, string kind, object config, string? vocabulary, ModelInfo info, IReadOnlyList<Parameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(parameters);
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("A family network needs a kind.", nameof(kind));
        if (info.Name is null || info.TrainingTextName is null) throw new ArgumentException("The model's info needs a name and a training text name.", nameof(info));

        var configJson = JsonSerializer.SerializeToElement(config, config.GetType(), Json);
        if (configJson.ValueKind != JsonValueKind.Object) throw new ArgumentException("A family network's config must be a record, not a single value.", nameof(config));

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in parameters)
        {
            if (p is null || string.IsNullOrEmpty(p.Name)) throw new ArgumentException("Every parameter needs a name.", nameof(parameters));
            if (!names.Add(p.Name)) throw new ArgumentException($"The parameter name '{p.Name}' is used twice.", nameof(parameters));
        }
        if (parameters.Any(p => p.Value.Data.Any(v => !double.IsFinite(v))) || (info.Results?.Values.Any(v => !double.IsFinite(v)) ?? false))
            throw new InvalidOperationException(ModelLibrary.NotFiniteMessage);

        var header = new Header(FormatName, Version, kind, configJson, vocabulary, info,
            parameters.Select(p => new ParameterEntry(p.Name, p.Value.Shape)).ToList());
        WriteHeader(s, header);
        WriteValues(s, parameters.Select(p => p.Value.Data));
    }

    /// <summary>Reads a family network file. Anything wrong with it throws <see cref="InvalidDataException"/> with a plain message.</summary>
    public static FamilyModelData Load(Stream s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Read(() => LoadCore(s));
    }

    private static FamilyModelData LoadCore(Stream s)
    {
        var line = ReadHeaderLine(s);
        var preamble = ReadPreamble(line);
        if (preamble.Version == LanguageVersion) throw new InvalidDataException("This is a language model file, not a family network.");
        if (preamble.Version != Version) throw new InvalidDataException(VersionMessage(preamble.Version, Version));

        var header = ReadHeader<Header>(line);
        if (string.IsNullOrWhiteSpace(header.Kind) || header.Config is not { ValueKind: JsonValueKind.Object } || header.Info is null
            || header.Info.Name is null || header.Info.TrainingTextName is null || header.Parameters is null)
            throw new InvalidDataException("The model file's header is missing required information.");
        if (header.Info.Results?.Values.Any(v => !double.IsFinite(v)) ?? false)
            throw new InvalidDataException("The model file has a number that is not valid in its results.");

        var listed = header.Parameters;
        if (listed.Any(entry => entry is null || string.IsNullOrEmpty(entry.Name) || entry.Shape is null))
            throw new InvalidDataException("The model file's parameter list has an incomplete entry.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var entry in listed)
        {
            if (!names.Add(entry.Name)) throw new InvalidDataException($"The model file lists the parameter '{entry.Name}' twice.");
            if (entry.Shape.Length == 0 || entry.Shape.Any(d => d < 1))
                throw new InvalidDataException($"The model file gives the parameter '{entry.Name}' an impossible shape.");
            long size = 1;
            foreach (int d in entry.Shape)
            {
                if (d > MaxDimension) throw new InvalidDataException("The model file describes a model that is too large to open.");
                size *= d;
                if (size > MaxParameters) throw new InvalidDataException("The model file describes a model that is too large to open.");
            }
            total += size;
            if (total > MaxParameters) throw new InvalidDataException("The model file describes a model that is too large to open.");
        }

        // When the length is known, a header that promises more numbers than the file holds is refused before any allocation.
        if (s.CanSeek && s.Length - s.Position < total * 8)
            throw new InvalidDataException("The model file is cut short: some of its numbers are missing.");

        var buffer = NewBuffer();
        var parameters = new List<(string Name, int[] Shape, double[] Values)>(listed.Count);
        foreach (var entry in listed)
        {
            var values = new double[entry.Shape.Aggregate(1, (a, d) => a * d)];
            ReadValues(s, entry.Name, values, buffer);
            parameters.Add((entry.Name, entry.Shape, values));
        }
        ExpectEnd(s);

        return new FamilyModelData(header.Kind, header.Config.Value, header.Vocabulary, header.Info, parameters);
    }
}
