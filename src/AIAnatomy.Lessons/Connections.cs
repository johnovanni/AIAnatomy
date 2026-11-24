using System.Text.Json;

namespace AIAnatomy.Lessons;

/// <summary>One link between two ideas on the idea map, with the sentence that explains it. <c>To</c> may be
/// <see cref="ConnectionList.WholeLoop"/>, meaning the whole loop of the model rather than one idea.</summary>
public sealed record Connection(string From, string To, string Sentence, IReadOnlyList<string> SourceIds);

public sealed class ConnectionList
{
    /// <summary>The "to" value that means the whole loop. Allowed only as <c>To</c>.</summary>
    public const string WholeLoop = "LOOP";

    private ConnectionList(IReadOnlyList<Connection> all) => All = all;

    public IReadOnlyList<Connection> All { get; }

    public static ConnectionList Create(IEnumerable<Connection> connections) => new(connections.ToList());

    public static ConnectionList Load(string path)
    {
        List<Connection>? list;
        try
        {
            list = JsonSerializer.Deserialize<List<Connection>>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)}: {ex.Message}", ex);
        }
        if (list == null || list.Any(c => c == null || c.From == null || c.To == null || c.Sentence == null || c.SourceIds == null))
            throw new InvalidDataException($"{Path.GetFileName(path)}: every connection needs from, to, sentence and sourceIds.");
        return new ConnectionList(list);
    }

    /// <summary>The connections that have this idea at either end.</summary>
    public IReadOnlyList<Connection> Of(string nodeId) =>
        All.Where(c => c.From == nodeId || c.To == nodeId).ToList();

    /// <summary>Everything wrong with the list; empty means fine.</summary>
    public IReadOnlyList<string> Validate(LessonCatalog catalog, SourceList sources, IdeaMapData map)
    {
        var problems = new List<string>();
        var known = map.Nodes.Select(n => n.Id).Concat(catalog.All.Select(l => l.Id)).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in All)
        {
            var name = $"{c.From} -> {c.To}";
            if (c.From == WholeLoop) problems.Add($"{name}: '{WholeLoop}' may only be the 'to' end.");
            else if (!known.Contains(c.From)) problems.Add($"{name}: unknown 'from' idea '{c.From}'.");
            if (c.To != WholeLoop && !known.Contains(c.To)) problems.Add($"{name}: unknown 'to' idea '{c.To}'.");
            if (c.From == c.To) problems.Add($"{name}: an idea cannot connect to itself.");
            if (string.IsNullOrWhiteSpace(c.Sentence)) problems.Add($"{name}: the sentence is empty.");
            if (c.SourceIds.Count == 0) problems.Add($"{name}: needs at least one source id.");
            foreach (var id in c.SourceIds)
                if (id != LessonCatalog.TextbookSource && sources.Find(id) == null)
                    problems.Add($"{name}: unknown source '{id}'.");

            var pair = string.CompareOrdinal(c.From, c.To) <= 0 ? $"{c.From}|{c.To}" : $"{c.To}|{c.From}";
            if (!seen.Add(pair)) problems.Add($"{name}: this pair of ideas is connected more than once.");
        }
        return problems;
    }
}
