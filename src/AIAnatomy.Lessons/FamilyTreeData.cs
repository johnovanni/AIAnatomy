using System.Text.Json;

namespace AIAnatomy.Lessons;

/// <summary>One bubble on the family tree. <c>Parent</c> is null only for the root; <c>LessonId</c> is null for a bubble with no lesson of its own.</summary>
public sealed record FamilyNode(string Id, string Label, string? Parent, string? LessonId,
    string WhatItIs, string? GoodAt, string? BadAt, string? Example);

/// <summary>The family tree of lesson 4.1: its bubbles and their cards, read from content/family-tree.json.</summary>
public sealed class FamilyTreeData
{
    private sealed record RawNode(string? Id, string? Label, string? Parent, string? Lesson,
        string? WhatItIs, string? GoodAt, string? BadAt, string? Example);

    private sealed record Document(List<RawNode?>? Nodes);

    private FamilyTreeData(IReadOnlyList<FamilyNode> nodes) => Nodes = nodes;

    /// <summary>In file order; the first is the root.</summary>
    public IReadOnlyList<FamilyNode> Nodes { get; }
    public FamilyNode Root => Nodes[0];

    public IReadOnlyList<FamilyNode> ChildrenOf(string id) =>
        Nodes.Where(n => n.Parent == id).ToList();

    public static FamilyTreeData Create(IEnumerable<FamilyNode> nodes) => new(nodes.ToList());

    public static FamilyTreeData Load(string path)
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
        if (doc?.Nodes is not { Count: > 0 } raw
            || raw.Any(n => n == null || n.Id == null || n.Label == null || n.WhatItIs == null))
            throw new InvalidDataException($"{Path.GetFileName(path)}: needs a non-empty nodes list, each with id, label and whatItIs.");
        return new FamilyTreeData(raw.Select(n => new FamilyNode(n!.Id!, n.Label!, n.Parent, n.Lesson,
            n.WhatItIs!, n.GoodAt, n.BadAt, n.Example)).ToList());
    }

    /// <summary>Everything wrong with the tree; empty means fine.</summary>
    public IReadOnlyList<string> Validate(LessonCatalog catalog)
    {
        var problems = new List<string>();
        var lessonIds = catalog.All.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var lessonsUsed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in Nodes)
            if (string.IsNullOrWhiteSpace(n.Id) || !ids.Add(n.Id)) problems.Add($"node '{n.Id}': empty or repeated id.");

        var roots = Nodes.Where(n => n.Parent == null).ToList();
        if (roots.Count != 1) problems.Add("the tree needs exactly one root (a node with no parent).");
        else if (Nodes[0].Parent != null) problems.Add("the root must come first in the file.");

        foreach (var n in Nodes)
        {
            if (string.IsNullOrWhiteSpace(n.Label)) problems.Add($"node '{n.Id}': empty label.");
            if (string.IsNullOrWhiteSpace(n.WhatItIs)) problems.Add($"node '{n.Id}': empty what it is.");
            if (n.Parent != null && !ids.Contains(n.Parent)) problems.Add($"node '{n.Id}': unknown parent '{n.Parent}'.");
            if (n.LessonId != null)
            {
                if (!lessonIds.Contains(n.LessonId)) problems.Add($"node '{n.Id}': '{n.LessonId}' is not a lesson id.");
                else if (!lessonsUsed.Add(n.LessonId)) problems.Add($"node '{n.Id}': lesson '{n.LessonId}' is used twice.");
                if (string.IsNullOrWhiteSpace(n.GoodAt) || string.IsNullOrWhiteSpace(n.BadAt) || string.IsNullOrWhiteSpace(n.Example))
                    problems.Add($"node '{n.Id}': a node with a lesson needs good at, bad at and an example.");
            }
            if (HasCycle(n)) problems.Add($"node '{n.Id}': its parents lead round in a cycle.");
        }

        if (roots.Count == 1 && ChildrenOf(roots[0].Id).Count != 3)
            problems.Add("the root needs exactly three branches.");
        return problems;
    }

    private bool HasCycle(FamilyNode start)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = start;
        while (current.Parent != null)
        {
            if (!seen.Add(current.Id)) return true;
            var next = Nodes.FirstOrDefault(n => n.Id == current.Parent);
            if (next == null) return false;
            current = next;
        }
        return false;
    }
}
