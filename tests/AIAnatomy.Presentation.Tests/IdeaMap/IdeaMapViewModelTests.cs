using AIAnatomy.Lessons;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Core;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.IdeaMap;

public class IdeaMapViewModelTests
{
    internal static LessonCatalog Catalog() => LessonCatalog.Load(TestEnvironment.Content);
    internal static ConnectionList Connections() => ConnectionList.Load(Path.Combine(TestEnvironment.Content, "connections.json"));
    internal static IdeaMapData Map() => IdeaMapData.Load(Path.Combine(TestEnvironment.Content, "idea-map.json"));

    internal static IdeaMapViewModel NewMap(TestEnvironment env, IGraphLayout? layout = null, ISettingsService? settings = null,
        IProgressService? progress = null, IActiveModelService? models = null)
    {
        settings ??= env.NewSettings();
        return new IdeaMapViewModel(Catalog(), Connections(), Map(), progress ?? env.NewProgress(), settings,
            models ?? env.NewActiveModels(settings), layout ?? new MsaglGraphLayout());
    }

    /// <summary>Counts layouts and returns an empty result.</summary>
    private sealed class CountingLayout : IGraphLayout
    {
        public int Runs { get; private set; }
        public GraphInput? Last { get; private set; }

        public GraphLayoutResult Layout(GraphInput input)
        {
            Runs++;
            Last = input;
            return new GraphLayoutResult([], [], 0, 0, null);
        }
    }

    private static Dictionary<string, (double, double)> AnySizes(IdeaMapViewModel map) =>
        map.Bubbles.ToDictionary(b => b.Id, _ => (100.0, 36.0));

    /// <summary>A size for every bubble of both maps.</summary>
    private static Dictionary<string, (double, double)> BothMapsSizes() =>
        new[] { "centre", "your-text", "next-character" }.Concat(Enumerable.Range(1, 5).Select(i => $"section-{i}"))
            .Concat(Catalog().All.Select(l => l.Id)).ToDictionary(id => id, _ => (100.0, 36.0));

    private static List<(string, string)> Pairs(IEnumerable<Connection> connections) => connections.Select(c => (c.From, c.To)).ToList();

    [Fact]
    public void IsStructural_TellsTreeAndFlowLinesFromConnectionLines()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env);
        Assert.True(map.IsStructural(new RoutedEdge("centre", "section-1", [])));
        Assert.True(map.IsStructural(new RoutedEdge("section-2", Catalog().InSection(Section.Parts)[0].Id, [])));
        Assert.False(map.IsStructural(new RoutedEdge("attention", "residual", [])));

        map.Mode = MapMode.Loop;
        var flow = Map().FlowEdges[0];
        Assert.True(map.IsStructural(new RoutedEdge(flow.To, flow.From, [])));
        Assert.False(map.IsStructural(new RoutedEdge("attention", "LOOP", [])));
    }

    [Fact]
    public void FlowLabel_GivesTheFlowEdgesLabelInLoopOnly()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env);
        var flow = Map().FlowEdges.First(f => f.Label.Length > 0 && Map().Nodes.Single(n => n.Id == f.From).Kind != IdeaMapData.Group
            && Map().Nodes.Single(n => n.Id == f.To).Kind != IdeaMapData.Group);
        Assert.Null(map.FlowLabel(new RoutedEdge(flow.From, flow.To, [])));

        map.Mode = MapMode.Loop;
        Assert.Equal(flow.Label, map.FlowLabel(new RoutedEdge(flow.From, flow.To, [])));
        Assert.Null(map.FlowLabel(new RoutedEdge("attention", "LOOP", [])));
    }

    [Fact]
    public void Map_DefaultsToRadial_AndRemembersLoop()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env);
        Assert.Equal(MapMode.Radial, map.Mode);
        Assert.Contains(map.Bubbles, b => b.Id == "centre");

        map.Mode = MapMode.Loop;

        Assert.Equal("Loop", env.NewSettings().Current.IdeaMapLayout);
        Assert.Equal(MapMode.Loop, NewMap(env).Mode);
        Assert.Contains(map.Bubbles, b => b.Id == "your-text");
        Assert.DoesNotContain(map.Bubbles, b => b.Id == "centre");
    }

    private static readonly string[] Section4 =
        ["family-tree", "expert-system", "search", "classic-ml", "learning-by-reward",
         "image-filters", "image-network", "memory-networks", "diffusion-shapes", "diffusion-steps"];

    [Fact]
    public void Loop_ShowsOnlyTheFamilyTreeFromSection4()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env);
        map.Mode = MapMode.Loop;
        var ids = map.Bubbles.Select(b => b.Id).ToList();
        Assert.Contains("family-tree", ids);
        foreach (var id in Section4.Skip(1)) Assert.DoesNotContain(id, ids);
    }

    [Fact]
    public void Radial_Section4HasItsTenLessonsInOrder()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env);
        var after = map.Bubbles.SkipWhile(b => b.Id != "section-4").Skip(1).Take(10).ToList();
        Assert.Equal(Section4, after.Select(b => b.Id));
        Assert.False(after[0].IsLater);   // 4.1 The family tree is built (Stage 2A, Task 5)
        Assert.All(after.Skip(1), b => Assert.True(b.IsLater));
    }

    /// <summary>Guards the Tree mode added later: a saved "Tree" (or any other name) is read as Radial.</summary>
    [Fact]
    public void SavedTreeMode_IsReadAsRadial()
    {
        using var env = new TestEnvironment();
        var settings = env.NewSettings();
        settings.Save(settings.Current with { IdeaMapLayout = "Tree" });
        Assert.Equal(MapMode.Radial, NewMap(env, settings: settings).Mode);
    }

    [Fact]
    public void Bubbles_RadialHasCentreSectionsAndLessons_LoopHasFlowBubblesAndLessons()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env);
        var lessons = Catalog().All.Select(l => l.Id).ToList();

        string[] radial = ["centre", .. Enumerable.Range(1, 5).Select(i => $"section-{i}"), .. lessons];
        Assert.Equal(radial.Order(), map.Bubbles.Select(b => b.Id).Order());
        Assert.Equal("The parts", map.Bubbles.Single(b => b.Id == "section-2").Label);
        Assert.Equal("2", map.Bubbles.Single(b => b.Id == "section-2").Number);
        var attention = map.Bubbles.Single(b => b.Id == "attention");
        Assert.Equal(("Attention", "2.4", Catalog().Get("attention").Part, true), (attention.Label, attention.Number, attention.Part, attention.IsLesson));

        map.Mode = MapMode.Loop;

        string[] loop = ["your-text", "next-character", .. lessons.Except(Section4.Skip(1))];   // Loop shows only the first lesson of section 4
        Assert.Equal(loop.Order(), map.Bubbles.Select(b => b.Id).Order());
    }

    [Fact]
    public void Layout_RunsOnArrangeAndModeChange_WithTheDescribedEdges()
    {
        using var env = new TestEnvironment();
        var layout = new CountingLayout();
        var map = NewMap(env, layout);
        Assert.Null(map.Layout);

        map.Arrange(AnySizes(map));
        Assert.Equal(1, layout.Runs);
        var radial = layout.Last!;
        Assert.Equal("centre", radial.CentreId);
        Assert.Contains(new LayoutEdge("centre", "section-2"), radial.PlacementEdges);
        Assert.Contains(new LayoutEdge("section-2", "attention"), radial.PlacementEdges);
        Assert.Contains(new LayoutEdge("step-through", "centre"), radial.RoutedOnlyEdges);   // LOOP -> centre
        Assert.DoesNotContain(radial.RoutedOnlyEdges, e => e.From == "your-text" || e.To == "your-text");

        // The last sizes don't cover the Loop map's bubbles, so the layout waits for Arrange.
        map.Mode = MapMode.Loop;
        Assert.Null(map.Layout);
        map.Arrange(BothMapsSizes());
        Assert.Equal(2, layout.Runs);
        var loop = layout.Last!;
        Assert.Equal(MapMode.Loop, loop.Mode);
        Assert.Null(loop.CentreId);
        Assert.Equal(["attention", "multi-head", "feed-forward"], loop.GroupMembers);
        // Section 4 is off the flow of text: its connections are routed in Loop but do not place boxes.
        Assert.Equal([new LayoutEdge("family-tree", "rules-vs-learning")], loop.RoutedOnlyEdges);
        Assert.DoesNotContain(loop.PlacementEdges, e => e.From == "family-tree" || e.To == "family-tree");
        Assert.Contains(new LayoutEdge("next-character", "your-text"), loop.PlacementEdges);
        Assert.Contains(new LayoutEdge("learning", "attention"), loop.PlacementEdges);
        Assert.Contains(new LayoutEdge("scale", ConnectionList.WholeLoop), loop.PlacementEdges);
        Assert.Single(loop.PlacementEdges, e => e == new LayoutEdge("your-text", "tokens")); // a flow edge and a connection

        // The last sizes cover both maps now, so changing the mode lays out at once.
        map.Mode = MapMode.Radial;
        Assert.Equal(3, layout.Runs);
        Assert.Equal(MapMode.Radial, layout.Last!.Mode);
        Assert.NotNull(map.Layout);
    }

    [Fact]
    public void Choose_ShowsOnlyThatBubblesConnections()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env, new CountingLayout());

        map.Choose("learning");

        Assert.Equal("learning", map.ChosenId);
        Assert.Equal(Pairs(Connections().Of("learning")), Pairs(map.ChosenConnections));
        Assert.All(map.ChosenConnections, c => Assert.True(c.From == "learning" || c.To == "learning"));
        Assert.Equal(map.ChosenConnections.Select(c => c.Sentence), map.Card!.Sentences);
    }

    [Fact]
    public void Choose_InRadial_LeavesOutConnectionsToBubblesNotOnThatMap()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env, new CountingLayout());

        map.Choose("tokens");
        Assert.DoesNotContain(map.ChosenConnections, c => c.From == "your-text" || c.To == "your-text");
        Assert.Contains(map.ChosenConnections, c => c.To == "embeddings");

        map.Mode = MapMode.Loop;
        Assert.Equal(Pairs(Connections().Of("tokens")), Pairs(map.ChosenConnections));
    }

    [Fact]
    public void Choose_DoesNotRelayout()
    {
        using var env = new TestEnvironment();
        var layout = new CountingLayout();
        var map = NewMap(env, layout);
        map.Arrange(AnySizes(map));
        var before = map.Layout;

        map.Choose("attention");
        map.Choose("centre");

        Assert.Same(before, map.Layout);
        Assert.Equal(1, layout.Runs);
    }

    [Fact]
    public void Card_ForNonLessonNode_HasDescriptionAndNoLesson()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env, new CountingLayout());
        map.Mode = MapMode.Loop;

        map.Choose("your-text");

        var card = map.Card!;
        Assert.Equal("Your text", card.Title);
        Assert.Equal(Map().Nodes.Single(n => n.Id == "your-text").Description, card.Description);
        Assert.Null(card.LessonId);
        Assert.Null(card.Number);
        Assert.False(map.OpenChosenLessonCommand.CanExecute(null));

        map.Mode = MapMode.Radial;      // "your-text" is not on the Radial map
        Assert.Null(map.ChosenId);
        Assert.Null(map.Card);

        map.Choose("section-2");
        Assert.Null(map.Card!.LessonId);
        Assert.Contains("2.4 Attention", map.Card.Description);
    }

    [Fact]
    public void OpenChosenLesson_RaisesWithLessonId()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env, new CountingLayout());
        string? opened = null;
        map.OpenLessonRequested += (_, id) => opened = id;

        map.Choose("attention");
        Assert.Equal("Attention", map.Card!.Title);
        Assert.Equal("2.4", map.Card.Number);
        Assert.Equal("attention", map.Card.LessonId);
        Assert.True(map.OpenChosenLessonCommand.CanExecute(null));
        map.OpenChosenLessonCommand.Execute(null);

        Assert.Equal("attention", opened);
    }

    [Fact]
    public void LaterBubbles_AreMarkedLater()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env, new CountingLayout());

        var later = map.Bubbles.Where(b => b.IsLater).Select(b => b.Id).Order();

        var catalog = Catalog();
        var placeholders = catalog.All.Where(l => l.IsPlaceholder).Select(l => l.Id);
        var laterSections = Enum.GetValues<Section>()
            .Where(s => catalog.InSection(s).Count > 0 && catalog.InSection(s).All(l => l.IsPlaceholder))
            .Select(IdeaMapViewModel.SectionId);
        Assert.NotEmpty(placeholders);
        Assert.Equal(placeholders.Concat(laterSections).Order(), later);
        Assert.All(map.Bubbles.Where(b => b.IsLater), b => Assert.DoesNotContain("(later)", b.Label));
    }

    /// <summary>Settings whose saved map layout is whatever the test says, unchecked.</summary>
    private sealed class RawSettings(string layout) : ISettingsService
    {
        public AppSettings Current { get; private set; } = new(IdeaMapLayout: layout);
        public string? LastSaveProblem => null;
        public event EventHandler? SaveFailed { add { } remove { } }
        public void Save(AppSettings settings) => Current = settings;
    }

    [Theory]
    [InlineData("1")]
    [InlineData("7")]
    [InlineData("loop")]
    [InlineData("")]
    public void Mode_IgnoresASavedLayoutThatIsNotAName(string saved)
    {
        using var env = new TestEnvironment();
        var settings = new RawSettings(saved);

        Assert.Equal(MapMode.Radial, NewMap(env, new CountingLayout(), settings, models: env.NewActiveModels()).Mode);
        Assert.Equal(MapMode.Loop, NewMap(env, new CountingLayout(), new RawSettings("Loop"), models: env.NewActiveModels()).Mode);
    }

    [Fact]
    public void Loop_TheStreamFlowsIntoTheBlock_AsAStructuralLineToTheFrame()
    {
        using var env = new TestEnvironment();
        var map = NewMap(env);
        map.Mode = MapMode.Loop;
        map.Arrange(GraphLayoutTests.Sizes(map, 13));

        var layout = map.Layout!;
        var g = layout.Group!.Value;
        var intoBlock = Assert.Single(layout.Edges, e => e.From == "residual-stream" && e.To == ConnectionList.WholeLoop);
        Assert.True(map.IsStructural(intoBlock));
        var end = intoBlock.Points[^1];
        double outside = Math.Max(Math.Max(g.X - end.X, end.X - (g.X + g.Width)), Math.Max(g.Y - end.Y, end.Y - (g.Y + g.Height)));
        double inside = Math.Min(Math.Min(end.X - g.X, g.X + g.Width - end.X), Math.Min(end.Y - g.Y, g.Y + g.Height - end.Y));
        Assert.True(outside <= 2 && inside <= 2, "The line from the stream should end on the Block frame.");
        Assert.False(map.IsStructural(layout.Edges.Single(e => e.From == "scale")));   // a whole-loop connection
    }

    [Fact]
    public void Bubbles_FollowProgressMarks_WithoutRelayout()
    {
        using var env = new TestEnvironment();
        var progress = env.NewProgress();
        var layout = new CountingLayout();
        var map = NewMap(env, layout, progress: progress);
        map.Arrange(AnySizes(map));
        Assert.Equal(LessonMark.NotOpened, map.Bubbles.Single(b => b.Id == "tokens").Mark);

        progress.MarkChecked("tokens");

        Assert.Equal(LessonMark.Checked, map.Bubbles.Single(b => b.Id == "tokens").Mark);
        Assert.Equal(1, layout.Runs);
    }

    [Fact]
    public void ProgressChange_UpdatesMarksInPlace_WithoutANewMapOrLayout()
    {
        // The view measures and calls Arrange (the full layout) when Bubbles or Mode changes; a mark change must raise
        // neither, only MarksChanged, so the bubbles keep their places (final review I2).
        using var env = new TestEnvironment();
        var progress = env.NewProgress();
        var layout = new CountingLayout();
        var map = NewMap(env, layout, progress: progress);
        map.Arrange(AnySizes(map));
        var before = map.Layout;
        var raised = new List<string?>();
        map.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        int marks = 0;
        map.MarksChanged += (_, _) => marks++;

        progress.MarkOpened("attention");
        progress.MarkChecked("tokens");

        Assert.Equal(2, marks);
        Assert.DoesNotContain(nameof(IdeaMapViewModel.Bubbles), raised);
        Assert.DoesNotContain(nameof(IdeaMapViewModel.Layout), raised);
        Assert.Same(before, map.Layout);
        Assert.Equal(1, layout.Runs);
        Assert.Equal("✓ opened", map.Bubbles.Single(b => b.Id == "attention").MarkText);
        Assert.Equal("✔ checked", map.Bubbles.Single(b => b.Id == "tokens").MarkText);
    }

    [Fact]
    public void MarkText_UsesTheContentsTreesSymbols_AndLaterOnPlaceholders()
    {
        // The tree: no mark until opened, ✓ opened, ✔ checked; placeholders carry no mark (final review I1).
        using var env = new TestEnvironment();
        var progress = env.NewProgress();
        var map = NewMap(env, new CountingLayout(), progress: progress);
        string placeholder = Catalog().All.First(l => l.IsPlaceholder).Id;
        progress.MarkOpened(placeholder);   // Ctrl+4 opens a placeholder page
        progress.MarkOpened("attention");
        progress.MarkChecked("tokens");

        IdeaBubble B(string id) => map.Bubbles.Single(b => b.Id == id);
        Assert.Equal("", B("embeddings").MarkText);
        Assert.Equal("✓ opened", B("attention").MarkText);
        Assert.Equal("✔ checked", B("tokens").MarkText);
        Assert.Equal("later", B(placeholder).MarkText);
        Assert.Equal("", B("centre").MarkText);
        Assert.Equal("", B("section-2").MarkText);
        Assert.Equal("", B("section-4").MarkText);   // Family: 4.1 is built, so the section is not "later"
        Assert.Equal(["✓ opened", "✔ checked"], IdeaBubble.LessonMarkTexts);
    }

    [Fact]
    public void AutomationName_SaysNumberLabelKindAndMark()
    {
        using var env = new TestEnvironment();
        var progress = env.NewProgress();
        var map = NewMap(env, new CountingLayout(), progress: progress);
        progress.MarkChecked("tokens");
        string placeholder = Catalog().All.First(l => l.IsPlaceholder).Id;

        IdeaBubble B(string id) => map.Bubbles.Single(b => b.Id == id);
        Assert.Equal("2.1 Tokens, lesson, checked", B("tokens").AutomationName);
        Assert.Equal("2.4 Attention, lesson, not opened yet", B("attention").AutomationName);
        Assert.Equal("2 The parts, section", B("section-2").AutomationName);
        Assert.EndsWith(", lesson, not built yet", B(placeholder).AutomationName);
    }

    [Fact]
    public void ChooseOrOpen_ChoosesFirst_ThenOpensTheChosenLesson()
    {
        // Enter on a bubble chooses it; Enter again on the chosen bubble opens its lesson (final review M6).
        using var env = new TestEnvironment();
        var map = NewMap(env, new CountingLayout());
        string? opened = null;
        map.OpenLessonRequested += (_, id) => opened = id;

        map.ChooseOrOpen("attention");
        Assert.Equal("attention", map.ChosenId);
        Assert.Null(opened);

        map.ChooseOrOpen("attention");
        Assert.Equal("attention", opened);

        opened = null;
        map.ChooseOrOpen("section-2");
        map.ChooseOrOpen("section-2");   // not a lesson: stays chosen, opens nothing
        Assert.Equal("section-2", map.ChosenId);
        Assert.Null(opened);
    }

    [Fact]
    public void GroupLabel_FollowsTheActiveModel()
    {
        using var env = new TestEnvironment();
        var settings = env.NewSettings();
        var models = env.NewActiveModels(settings);
        var map = NewMap(env, new CountingLayout(), settings, models: models);
        Assert.Equal($"Block × {models.Current.Transformer.Config.Layers}", map.GroupLabel);
        var current = models.Current;
        var deeper = new LanguageModel(new Transformer(current.Transformer.Config with { Layers = 3 }, new SeededRandom(1)),
            current.Tokenizer, current.Info with { Name = "three blocks", IsShipped = false });
        bool told = false;
        map.PropertyChanged += (_, e) => told |= e.PropertyName == nameof(IdeaMapViewModel.GroupLabel);

        models.SetActive(models.Library.Save(deeper, "three blocks"));

        Assert.True(told);
        Assert.Equal("Block × 3", map.GroupLabel);
    }
}
