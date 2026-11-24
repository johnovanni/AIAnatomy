using System.Text.Json;

namespace AIAnatomy.Lessons;

/// <summary>One box on the idea map. <c>Kind</c> is "centre", "flow" or "group". Lesson boxes use the lesson's id.</summary>
public sealed record IdeaNode(string Id, string Label, string Description, string Kind);

/// <summary>An arrow on the path the text takes through the model.</summary>
public sealed record FlowEdge(string From, string To, string Label);

public sealed class IdeaMapData
{
    public const string Centre = "centre", Flow = "flow", Group = "group";

    /// <summary>Boxes that are not lessons.</summary>
    public static IReadOnlyList<string> MapOnlyIds { get; } = ["centre", "your-text", "next-character", "block"];

    private sealed record Document(List<IdeaNode>? Nodes, List<FlowEdge>? FlowEdges, List<string>? GroupMembers);

    private IdeaMapData(IReadOnlyList<IdeaNode> nodes, IReadOnlyList<FlowEdge> flowEdges, IReadOnlyList<string> groupMembers)
    {
        Nodes = nodes;
        FlowEdges = flowEdges;
        GroupMembers = groupMembers;
    }

    public IReadOnlyList<IdeaNode> Nodes { get; }
    public IReadOnlyList<FlowEdge> FlowEdges { get; }
    /// <summary>The lesson ids drawn inside the Block group.</summary>
    public IReadOnlyList<string> GroupMembers { get; }

    public static IdeaMapData Create(IEnumerable<IdeaNode> nodes, IEnumerable<FlowEdge> flowEdges, IEnumerable<string> groupMembers) =>
        new(nodes.ToList(), flowEdges.ToList(), groupMembers.ToList());

    public static IdeaMapData Load(string path)
    {
        Document? doc;
        try
        {
            doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)}: {ex.Message}", ex);
        }
        if (doc?.Nodes == null || doc.FlowEdges == null || doc.GroupMembers == null
            || doc.Nodes.Any(n => n == null || n.Id == null || n.Label == null || n.Description == null || n.Kind == null)
            || doc.FlowEdges.Any(e => e == null || e.From == null || e.To == null || e.Label == null))
            throw new InvalidDataException($"{Path.GetFileName(path)}: needs nodes, flowEdges and groupMembers, each complete.");
        return new IdeaMapData(doc.Nodes, doc.FlowEdges, doc.GroupMembers);
    }

    /// <summary>Everything wrong with the map; empty means fine.</summary>
    public IReadOnlyList<string> Validate(LessonCatalog catalog)
    {
        var problems = new List<string>();
        var lessonIds = catalog.All.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in Nodes)
        {
            if (string.IsNullOrWhiteSpace(n.Id) || !ids.Add(n.Id)) problems.Add($"node '{n.Id}': empty or repeated id.");
            if (string.IsNullOrWhiteSpace(n.Label)) problems.Add($"node '{n.Id}': empty label.");
            if (string.IsNullOrWhiteSpace(n.Description)) problems.Add($"node '{n.Id}': empty description.");
            if (n.Kind is not (Centre or Flow or Group)) problems.Add($"node '{n.Id}': unknown kind '{n.Kind}'.");
            if (!lessonIds.Contains(n.Id) && !MapOnlyIds.Contains(n.Id))
                problems.Add($"node '{n.Id}': not a lesson id and not one of {string.Join(", ", MapOnlyIds)}.");
        }
        foreach (var lessonId in lessonIds.Where(id => !ids.Contains(id)))
            problems.Add($"lesson '{lessonId}' has no box on the map.");
        if (Nodes.Count(n => n.Kind == Centre) != 1) problems.Add("the map needs exactly one centre box.");
        foreach (var e in FlowEdges)
        {
            if (!ids.Contains(e.From)) problems.Add($"flow edge {e.From} -> {e.To}: unknown 'from' box.");
            if (!ids.Contains(e.To)) problems.Add($"flow edge {e.From} -> {e.To}: unknown 'to' box.");
            if (string.IsNullOrWhiteSpace(e.Label)) problems.Add($"flow edge {e.From} -> {e.To}: empty label.");
        }
        foreach (var m in GroupMembers)
            if (!lessonIds.Contains(m) || !ids.Contains(m)) problems.Add($"group member '{m}' is not a lesson box on the map.");
        if (GroupMembers.Count != GroupMembers.Distinct().Count()) problems.Add("a group member is listed twice.");
        if (GroupMembers.Count > 0 && !Nodes.Any(n => n.Kind == Group)) problems.Add("group members need a group box.");
        return problems;
    }
}
