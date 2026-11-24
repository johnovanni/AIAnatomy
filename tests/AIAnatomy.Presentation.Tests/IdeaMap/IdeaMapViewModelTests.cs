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

        string[] loop = ["your-text", "next-character", .. lessons];
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
        Assert.Empty(loop.RoutedOnlyEdges);
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

        Assert.Equal(["beyond-coming", "family-coming", "section-4", "section-5"], later);
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
