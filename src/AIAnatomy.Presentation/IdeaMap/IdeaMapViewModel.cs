using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.IdeaMap;

/// <summary>One bubble on the idea map. <see cref="Number"/> is the contents number ("2.4", or "2" for a section);
/// <see cref="Part"/> is the lesson's part key; <see cref="IsLater"/> marks lessons and sections not built yet.</summary>
public sealed record IdeaBubble(string Id, string Label, string? Number, string? Part, bool IsLater, bool IsLesson, LessonMark Mark)
{
    /// <summary>The marks a lesson bubble can show, in words as well as the contents tree's symbols. The view sizes every
    /// lesson bubble for the widest of these, so a mark that changes never changes a bubble's size (final review I1, I2).</summary>
    public static IReadOnlyList<string> LessonMarkTexts { get; } = ["✓ opened", "✔ checked"];

    /// <summary>The mark after the number: the tree's symbols ("✓ opened", "✔ checked"), nothing before a lesson is
    /// opened, and "later" (not a mark) on a lesson or section that is not built yet.</summary>
    public string MarkText => IsLater ? "later"
        : !IsLesson ? ""
        : Mark switch { LessonMark.Checked => LessonMarkTexts[1], LessonMark.Opened => LessonMarkTexts[0], _ => "" };

    /// <summary>What a screen reader says: "2.1 Tokens, lesson, checked", "2 The parts, section".</summary>
    public string AutomationName
    {
        get
        {
            string kind = IsLesson ? "lesson" : Id.StartsWith("section-", StringComparison.Ordinal) ? "section" : "idea";
            string state = IsLater ? ", not built yet"
                : !IsLesson ? ""
                : Mark switch { LessonMark.Checked => ", checked", LessonMark.Opened => ", opened", _ => ", not opened yet" };
            return $"{(Number != null ? Number + " " : "")}{Label}, {kind}{state}";
        }
    }
}

/// <summary>The side card for the chosen bubble: its title, number, a short description, the sentences of its
/// connections on this map, and the lesson it opens (null for bubbles that are not lessons).</summary>
public sealed record IdeaCard(string Title, string? Number, string? Description, IReadOnlyList<string> Sentences, string? LessonId);

/// <summary>
/// The idea map: Radial (the contents round "How a transformer works") or Loop (the path text takes through the
/// model). The view measures the bubbles and calls <see cref="Arrange"/>; the layout is computed there and on a
/// change of mode, never when a bubble is chosen.
/// </summary>
public sealed partial class IdeaMapViewModel : ObservableObject
{
    /// <summary>Ids of the section bubbles in Radial mode: "section-1" to "section-5".</summary>
    public static string SectionId(Section section) => $"section-{SectionInfo.Number(section)}";

    private readonly LessonCatalog _catalog;
    private readonly ConnectionList _connections;
    private readonly IdeaMapData _map;
    private readonly IProgressService _progress;
    private readonly ISettingsService _settings;
    private readonly IActiveModelService _models;
    private readonly IGraphLayout _graphLayout;
    private IReadOnlyDictionary<string, (double Width, double Height)>? _sizes;
    private IReadOnlyDictionary<string, (double Width, double Height)> _labelSizes = new Dictionary<string, (double, double)>();

    public IdeaMapViewModel(LessonCatalog catalog, ConnectionList connections, IdeaMapData map, IProgressService progress,
        ISettingsService settings, IActiveModelService models, IGraphLayout layout)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _graphLayout = layout ?? throw new ArgumentNullException(nameof(layout));

        // Only the names count (Enum.TryParse would also take numbers such as "1" or "7").
        var saved = settings.Current.IdeaMapLayout;
        _mode = saved == nameof(MapMode.Loop) ? MapMode.Loop : MapMode.Radial;   // only "Radial" or "Loop" are read
        _groupLabel = MakeGroupLabel();
        _bubbles = BuildBubbles();
        _progress.Changed += (_, _) => UpdateMarks();
        _models.Changed += (_, _) => GroupLabel = MakeGroupLabel();
    }

    /// <summary>"Block × N", N being the active model's number of layers.</summary>
    [ObservableProperty] private string _groupLabel;

    /// <summary>The bubbles of the current map, in contents order. A new list (a change of mode) is raised as a property change
    /// and makes the view measure and lay out again; a change of marks is not (see <see cref="MarksChanged"/>).</summary>
    public IReadOnlyList<IdeaBubble> Bubbles
    {
        get => _bubbles;
        private set => SetProperty(ref _bubbles, value);
    }
    private IReadOnlyList<IdeaBubble> _bubbles;
    [ObservableProperty] private GraphLayoutResult? _layout;
    [ObservableProperty] private string? _chosenId;
    [ObservableProperty] private IReadOnlyList<Connection> _chosenConnections = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenChosenLessonCommand))]
    private IdeaCard? _card;

    /// <summary>Raised with a lesson id when the user asks to open the chosen bubble's lesson.</summary>
    public event EventHandler<string>? OpenLessonRequested;

    /// <summary>Raised when a progress mark changes. <see cref="Bubbles"/> then holds the same bubbles, in the same order,
    /// with their new marks; it is not raised as a property change, because a new map is measured and laid out again
    /// (spec 5.3: the layout is computed once per text size, not on every click). The view updates the text in place.</summary>
    public event EventHandler? MarksChanged;

    private void UpdateMarks()
    {
        _bubbles = _bubbles.Select(b => b.IsLesson ? b with { Mark = _progress.MarkOf(b.Id) } : b).ToList();
        MarksChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Radial or Loop. Saved to the settings, so the last choice is remembered.</summary>
    public MapMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            _settings.Save(_settings.Current with { IdeaMapLayout = value.ToString() });
            OnPropertyChanged();
            Bubbles = BuildBubbles();
            if (ChosenId != null && !Bubbles.Any(b => b.Id == ChosenId)) ClearChoice();
            else if (ChosenId != null) Choose(ChosenId);
            // Lay out again only if the last measured sizes cover this map's bubbles; otherwise the view measures
            // the new bubbles and calls Arrange.
            Layout = _sizes != null && Bubbles.All(b => _sizes.ContainsKey(b.Id)) ? RunLayout(_sizes) : null;
        }
    }
    private MapMode _mode;

    /// <summary>The labels drawn on this map's lines (Loop: the flow labels; Radial: none). Give their sizes to
    /// <see cref="Arrange"/> so the layout keeps room for them.</summary>
    public IReadOnlyList<string> LineLabels =>
        Mode == MapMode.Loop ? _map.FlowEdges.Select(f => f.Label).Where(l => l.Length > 0).Distinct().ToList() : [];

    /// <summary>Lays the map out for these bubble sizes (px, by bubble id; every bubble needs one) and, optionally, the
    /// sizes of the <see cref="LineLabels"/> (px, by label text; a label without one gets no room kept for it).</summary>
    public void Arrange(IReadOnlyDictionary<string, (double Width, double Height)> sizes,
        IReadOnlyDictionary<string, (double Width, double Height)>? labelSizes = null)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var missing = Bubbles.FirstOrDefault(b => !sizes.ContainsKey(b.Id));
        if (missing != null) throw new ArgumentException($"No size for bubble '{missing.Id}'.", nameof(sizes));
        _sizes = new Dictionary<string, (double, double)>(sizes);
        if (labelSizes != null) _labelSizes = new Dictionary<string, (double, double)>(labelSizes);
        Layout = RunLayout(_sizes);
    }

    /// <summary>Chooses a bubble: its connections on this map light up and the card describes it. Does not lay out.</summary>
    public void Choose(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var bubble = Bubbles.FirstOrDefault(b => b.Id == id)
            ?? throw new ArgumentException($"There is no bubble '{id}' on the {Mode} map.", nameof(id));
        ChosenId = id;
        ChosenConnections = _connections.Of(id).Where(IsOnThisMap).ToList();
        Card = MakeCard(bubble, ChosenConnections);
    }

    /// <summary>Enter on a bubble: chooses it, or, when it is already the chosen bubble, opens its lesson (a bubble that is
    /// not a lesson just stays chosen). Saves the keyboard user a long way back to the card's Open lesson button.</summary>
    public void ChooseOrOpen(string id)
    {
        if (ChosenId == id && Card?.LessonId != null) OpenChosenLesson();
        else Choose(id);
    }

    [RelayCommand(CanExecute = nameof(CanOpenChosenLesson))]
    private void OpenChosenLesson()
    {
        if (Card?.LessonId is { } id) OpenLessonRequested?.Invoke(this, id);
    }

    private bool CanOpenChosenLesson() => Card?.LessonId != null;

    /// <summary>Un-chooses the bubble: no card, no lit connections.</summary>
    public void ClearChoice()
    {
        ChosenId = null;
        ChosenConnections = [];
        Card = null;
    }

    private string MakeGroupLabel() => $"Block × {_models.Current.Transformer.Config.Layers}";

    private IdeaNode MapNode(string id) => _map.Nodes.Single(n => n.Id == id);

    /// <summary>The bubbles on the current map, in contents order (Radial: the centre, then each section and its lessons).</summary>
    private IReadOnlyList<IdeaBubble> BuildBubbles()
    {
        var bubbles = new List<IdeaBubble>();
        IdeaBubble LessonBubble(Lesson l) => new(l.Id, MapNode(l.Id).Label, _catalog.NumberOf(l), l.Part, l.IsPlaceholder,
            IsLesson: true, _progress.MarkOf(l.Id));
        IdeaBubble Plain(string id) => new(id, MapNode(id).Label, null, null, false, IsLesson: false, LessonMark.NotOpened);

        if (Mode == MapMode.Radial)
        {
            bubbles.Add(Plain(_map.Nodes.Single(n => n.Kind == IdeaMapData.Centre).Id));
            foreach (var section in Enum.GetValues<Section>())
            {
                var lessons = _catalog.InSection(section);
                bubbles.Add(new IdeaBubble(SectionId(section), SectionInfo.Title(section), SectionInfo.Number(section).ToString(),
                    null, lessons.Count > 0 && lessons.All(l => l.IsPlaceholder), IsLesson: false, LessonMark.NotOpened));
                bubbles.AddRange(lessons.Select(LessonBubble));
            }
        }
        else
        {
            var lessonIds = _catalog.All.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
            bubbles.AddRange(_map.Nodes.Where(n => n.Kind == IdeaMapData.Flow && !lessonIds.Contains(n.Id)).Select(n => Plain(n.Id)));
            // Section 4 is a family of many members; the Loop map, which follows the flow of text, shows only its first lesson (the family tree).
            var hiddenInLoop = _catalog.InSection(Section.Family).Skip(1).Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
            bubbles.AddRange(_catalog.InContentsOrder.Where(l => !hiddenInLoop.Contains(l.Id)).Select(LessonBubble));
        }
        return bubbles;
    }

    /// <summary>True when both ends of the connection are on the current map ("the whole loop" always is).</summary>
    private bool IsOnThisMap(Connection c) =>
        Bubbles.Any(b => b.Id == c.From) && (c.To == ConnectionList.WholeLoop || Bubbles.Any(b => b.Id == c.To));

    private GraphInput BuildInput(IReadOnlyDictionary<string, (double Width, double Height)> sizes)
    {
        var nodes = Bubbles.Select(b => new LayoutNode(b.Id, sizes[b.Id].Width, sizes[b.Id].Height)).ToList();
        var connections = _connections.All.Where(IsOnThisMap).ToList();
        if (Mode == MapMode.Radial)
        {
            var centre = Bubbles[0].Id;
            var tree = new List<LayoutEdge>();
            foreach (var section in Enum.GetValues<Section>())
            {
                tree.Add(new LayoutEdge(centre, SectionId(section)));
                tree.AddRange(_catalog.InSection(section).Select(l => new LayoutEdge(SectionId(section), l.Id)));
            }
            var ideas = connections.Select(c => new LayoutEdge(c.From, c.To == ConnectionList.WholeLoop ? centre : c.To)).ToList();
            return new GraphInput(MapMode.Radial, nodes, tree, ideas, centre, []);
        }

        var placement = _map.FlowEdges.Select(f => _labelSizes.TryGetValue(f.Label, out var label)
            ? new LayoutEdge(LayoutEnd(f.From), LayoutEnd(f.To), label.Width, label.Height)
            : new LayoutEdge(LayoutEnd(f.From), LayoutEnd(f.To))).ToList();
        // Section 4 (the family tree) is off the flow of text, so in Loop its connections are drawn but do not place
        // boxes: the family tree keeps its corner, and the rows of the flow stay as they are.
        var family = _catalog.InSection(Section.Family).Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
        var routedOnly = new List<LayoutEdge>();
        foreach (var c in connections)
        {
            if (placement.Any(e => SamePair(e, c.From, c.To)) || routedOnly.Any(e => SamePair(e, c.From, c.To))) continue;
            (family.Contains(c.From) || family.Contains(c.To) ? routedOnly : placement).Add(new LayoutEdge(c.From, c.To));
        }
        return new GraphInput(MapMode.Loop, nodes, placement, routedOnly, null, _map.GroupMembers);
    }

    /// <summary>True for a line of the map's own structure (Radial: centre to section, section to lesson; Loop: the flow
    /// of text), false for a line that only shows a connection between ideas. The view draws the first kind always and the
    /// second only for the chosen bubble.</summary>
    public bool IsStructural(RoutedEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        if (Mode == MapMode.Radial)
            return edge.From == Bubbles[0].Id || edge.From.StartsWith("section-", StringComparison.Ordinal);
        return _map.FlowEdges.Any(f => (LayoutEnd(f.From) == edge.From && LayoutEnd(f.To) == edge.To)
            || (LayoutEnd(f.From) == edge.To && LayoutEnd(f.To) == edge.From));
    }

    /// <summary>The label of the flow line this routed line draws ("split", "look up", ...), or null when it is not a flow
    /// line (Radial, or a connection line).</summary>
    public string? FlowLabel(RoutedEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        if (Mode != MapMode.Loop) return null;
        return _map.FlowEdges.FirstOrDefault(f => (LayoutEnd(f.From) == edge.From && LayoutEnd(f.To) == edge.To)
            || (LayoutEnd(f.From) == edge.To && LayoutEnd(f.To) == edge.From))?.Label;
    }

    /// <summary>A flow-edge end as the layout knows it: the group box (Block) is its frame, which the layout calls
    /// <see cref="ConnectionList.WholeLoop"/>.</summary>
    private string LayoutEnd(string id) =>
        _map.Nodes.Any(n => n.Id == id && n.Kind == IdeaMapData.Group) ? ConnectionList.WholeLoop : id;

    private static bool SamePair(LayoutEdge e, string a, string b) => (e.From == a && e.To == b) || (e.From == b && e.To == a);

    private GraphLayoutResult RunLayout(IReadOnlyDictionary<string, (double Width, double Height)> sizes) =>
        _graphLayout.Layout(BuildInput(sizes));

    private IdeaCard MakeCard(IdeaBubble bubble, IReadOnlyList<Connection> connections)
    {
        var sentences = connections.Select(c => c.Sentence).ToList();
        if (bubble.IsLesson)
        {
            var lesson = _catalog.Get(bubble.Id);
            return new IdeaCard(lesson.Title, bubble.Number, MapNode(bubble.Id).Description, sentences, lesson.Id);
        }
        if (bubble.Id.StartsWith("section-", StringComparison.Ordinal))
        {
            var section = Enum.GetValues<Section>().Single(s => SectionId(s) == bubble.Id);
            var titles = _catalog.InSection(section).Select(l => $"{_catalog.NumberOf(l)} {l.Title}");
            return new IdeaCard(bubble.Label, bubble.Number, $"The lessons in this section: {string.Join("; ", titles)}.",
                sentences, null);
        }
        return new IdeaCard(bubble.Label, null, MapNode(bubble.Id).Description, sentences, null);
    }
}
