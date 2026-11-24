using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.IdeaMap;

/// <summary>One bubble on the idea map. <see cref="Number"/> is the contents number ("2.4", or "2" for a section);
/// <see cref="Part"/> is the lesson's part key; <see cref="IsLater"/> marks lessons and sections not built yet.</summary>
public sealed record IdeaBubble(string Id, string Label, string? Number, string? Part, bool IsLater, bool IsLesson, LessonMark Mark);

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

        _mode = Enum.TryParse<MapMode>(settings.Current.IdeaMapLayout, out var saved) ? saved : MapMode.Radial;
        _groupLabel = MakeGroupLabel();
        _bubbles = BuildBubbles();
        _progress.Changed += (_, _) => Bubbles = BuildBubbles();
        _models.Changed += (_, _) => GroupLabel = MakeGroupLabel();
    }

    /// <summary>"Block × N", N being the active model's number of layers.</summary>
    [ObservableProperty] private string _groupLabel;

    [ObservableProperty] private IReadOnlyList<IdeaBubble> _bubbles;
    [ObservableProperty] private GraphLayoutResult? _layout;
    [ObservableProperty] private string? _chosenId;
    [ObservableProperty] private IReadOnlyList<Connection> _chosenConnections = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenChosenLessonCommand))]
    private IdeaCard? _card;

    /// <summary>Raised with a lesson id when the user asks to open the chosen bubble's lesson.</summary>
    public event EventHandler<string>? OpenLessonRequested;

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

    /// <summary>Lays the map out for these bubble sizes (px, by bubble id). Every bubble needs a size.</summary>
    public void Arrange(IReadOnlyDictionary<string, (double Width, double Height)> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var missing = Bubbles.FirstOrDefault(b => !sizes.ContainsKey(b.Id));
        if (missing != null) throw new ArgumentException($"No size for bubble '{missing.Id}'.", nameof(sizes));
        _sizes = new Dictionary<string, (double, double)>(sizes);
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

    [RelayCommand(CanExecute = nameof(CanOpenChosenLesson))]
    private void OpenChosenLesson()
    {
        if (Card?.LessonId is { } id) OpenLessonRequested?.Invoke(this, id);
    }

    private bool CanOpenChosenLesson() => Card?.LessonId != null;

    private void ClearChoice()
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
            bubbles.AddRange(_catalog.InContentsOrder.Select(LessonBubble));
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

        var placement = _map.FlowEdges.Select(f => new LayoutEdge(f.From, f.To)).ToList();
        foreach (var c in connections)
            if (!placement.Any(e => SamePair(e, c.From, c.To))) placement.Add(new LayoutEdge(c.From, c.To));
        return new GraphInput(MapMode.Loop, nodes, placement, [], null, _map.GroupMembers);
    }

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
