using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One bubble on the family tree. <see cref="Number"/> is the contents number of its lesson ("4.2"), null for a
/// bubble with no lesson (the root, the three branches, Neural networks); <see cref="IsLater"/> marks a lesson not built yet.</summary>
public sealed record FamilyBubble(string Id, string Label, string? Number, bool IsLater, bool HasLesson)
{
    /// <summary>The part key whose colour edges the bubble: its lesson's part, or "family" for a bubble with no lesson
    /// (or a placeholder lesson with no part yet). Colour is never the only sign: the number and "later" are words.</summary>
    public string Part { get; init; } = FamilyPart;

    public const string FamilyPart = "family";

    /// <summary>The line above the label: the lesson number, with "later" for a lesson not built yet ("4.6  later").</summary>
    public string HeadText => (Number ?? "") + (IsLater ? (Number != null ? "  " : "") + "later" : "");

    /// <summary>What a screen reader says: "4.2 Rules, family member", "Neural networks, group", "4.6 Image networks, not built yet".</summary>
    public string AutomationName =>
        $"{(Number != null ? Number + " " : "")}{Label}, {(IsLater ? "not built yet" : HasLesson ? "family member" : "group")}";
}

/// <summary>The card for the chosen bubble: what it is, good at, bad at, a real-world example (the last three null for a
/// bubble without them), and the lesson it opens (null for none).</summary>
public sealed record FamilyCard(string Title, string WhatItIs, string? GoodAt, string? BadAt, string? Example,
    string? LessonId, string? LessonTitle, bool LessonIsLater)
{
    /// <summary>The lesson's contents number ("4.2"), or null with no lesson.</summary>
    public string? LessonNumber { get; init; }

    /// <summary>"Open lesson 4.2 Rules: an expert system" (with "(not built yet)" for a placeholder), or null with no lesson.</summary>
    public string? OpenLessonText => LessonId == null ? null
        : $"Open lesson {LessonNumber} {LessonTitle}" + (LessonIsLater ? " (not built yet)" : "");

    /// <summary>The card's text, for the "card" screen.</summary>
    internal string ScreenText => string.Join("\n", new[] { Title, WhatItIs, GoodAt, BadAt, Example, OpenLessonText }.Where(t => t != null));
}

/// <summary>
/// Lesson 4.1's lab: the family tree of AI, laid out top-down by the graph layout (Tree mode). The view measures the
/// bubbles at the current text size and calls <see cref="Arrange"/>; choosing a bubble shows its card and never lays out
/// again. Screen: "card".
/// </summary>
public sealed partial class FamilyTreeLabViewModel : LabViewModel
{
    public const string Id = "FamilyTreeLab";
    private const string CardScreen = "card";

    private readonly FamilyTreeData _tree;
    private readonly LessonCatalog _catalog;
    private readonly IGraphLayout _graphLayout;

    public FamilyTreeLabViewModel(FamilyTreeData tree, LessonCatalog catalog, IGraphLayout layout)
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _graphLayout = layout ?? throw new ArgumentNullException(nameof(layout));
        Bubbles = tree.Nodes.Select(n =>
        {
            var lesson = n.LessonId != null ? catalog.Get(n.LessonId) : null;
            return new FamilyBubble(n.Id, n.Label, lesson != null ? catalog.NumberOf(lesson) : null,
                lesson?.IsPlaceholder ?? false, lesson != null)
            { Part = lesson?.Part ?? FamilyBubble.FamilyPart };
        }).ToList();
        ReportScreen(CardScreen, "");   // the baseline: no card
    }

    public override string LabId => Id;

    /// <summary>The bubbles in file order (the root first), which is the view's Tab order.</summary>
    public IReadOnlyList<FamilyBubble> Bubbles { get; }

    [ObservableProperty] private GraphLayoutResult? _layout;
    [ObservableProperty] private string? _chosenId;
    [ObservableProperty] private FamilyCard? _card;

    /// <summary>Lays the tree out for these bubble sizes (px, by bubble id; every bubble needs one).</summary>
    public void Arrange(IReadOnlyDictionary<string, (double Width, double Height)> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var missing = Bubbles.FirstOrDefault(b => !sizes.ContainsKey(b.Id));
        if (missing != null) throw new ArgumentException($"No size for bubble '{missing.Id}'.", nameof(sizes));
        var nodes = Bubbles.Select(b => new LayoutNode(b.Id, sizes[b.Id].Width, sizes[b.Id].Height)).ToList();
        var edges = _tree.Nodes.Where(n => n.Parent != null).Select(n => new LayoutEdge(n.Parent!, n.Id)).ToList();
        Layout = _graphLayout.Layout(new GraphInput(MapMode.Tree, nodes, edges, [], _tree.Root.Id, []));
    }

    /// <summary>Chooses a bubble and shows its card. Does not lay out.</summary>
    public void Choose(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var node = _tree.Nodes.FirstOrDefault(n => n.Id == id)
            ?? throw new ArgumentException($"There is no bubble '{id}' on the family tree.", nameof(id));
        var lesson = node.LessonId != null ? _catalog.Get(node.LessonId) : null;
        ChosenId = id;
        Card = new FamilyCard(node.Label, node.WhatItIs, node.GoodAt, node.BadAt, node.Example,
            lesson?.Id, lesson?.Title, lesson?.IsPlaceholder ?? false)
        { LessonNumber = lesson != null ? _catalog.NumberOf(lesson) : null };
        ReportScreen(CardScreen, Card.ScreenText);
    }

    /// <summary>Enter on a bubble: chooses it and returns null, or, when it is already the chosen bubble and has a lesson,
    /// returns that lesson's id for the view to open (a bubble without a lesson just stays chosen).</summary>
    public string? ChooseOrOpen(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (ChosenId == id && Card?.LessonId is { } lesson) return lesson;
        Choose(id);
        return null;
    }

    /// <summary>Un-chooses the bubble: no card.</summary>
    public void ClearChoice()
    {
        ChosenId = null;
        Card = null;
        ReportScreen(CardScreen, "");
    }

    public override void Reset() => ClearChoice();
}
