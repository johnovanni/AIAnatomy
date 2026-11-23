using System.Text.Json;

namespace AIAnatomy.Lessons;

public sealed record Source(string Id, string Citation, string? Url);

public sealed class SourceList
{
    private readonly Dictionary<string, Source> _byId;

    private SourceList(IReadOnlyList<Source> all)
    {
        All = all;
        _byId = all.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<Source> All { get; }

    public static SourceList Load(string path)
    {
        List<Source>? list;
        try
        {
            list = JsonSerializer.Deserialize<List<Source>>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)}: {ex.Message}", ex);
        }
        if (list == null || list.Any(s => string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Citation)))
            throw new InvalidDataException($"{Path.GetFileName(path)}: every source needs an id and a citation.");
        if (list.Select(s => s.Id).Distinct().Count() != list.Count)
            throw new InvalidDataException($"{Path.GetFileName(path)}: duplicate source id.");
        return new SourceList(list);
    }

    public Source? Find(string id) => _byId.GetValueOrDefault(id);
}
