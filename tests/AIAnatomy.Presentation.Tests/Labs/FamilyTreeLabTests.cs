using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class FamilyTreeLabTests
{
    private static readonly LessonCatalog Catalog = LessonCatalog.Load(TestEnvironment.Content);
    private static readonly FamilyTreeData Tree = FamilyTreeData.Load(Path.Combine(TestEnvironment.Content, "family-tree.json"));

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

    private static FamilyTreeLabViewModel NewLab(IGraphLayout? layout = null) =>
        new(Tree, Catalog, layout ?? new CountingLayout());

    private static Dictionary<string, (double Width, double Height)> Sizes(FamilyTreeLabViewModel lab, double width = 120, double height = 40) =>
        lab.Bubbles.ToDictionary(b => b.Id, _ => (width, height));

    private static FamilyBubble Bubble(FamilyTreeLabViewModel lab, string lessonId) =>
        lab.Bubbles.Single(b => b.Id == Tree.Nodes.Single(n => n.LessonId == lessonId).Id);

    [Fact]
    public void Bubbles_FollowTheFileOrder_WithLessonNumbers()
    {
        var lab = NewLab();
        Assert.Equal(Tree.Nodes.Select(n => n.Id), lab.Bubbles.Select(b => b.Id));
        Assert.Equal(Tree.Nodes.Select(n => n.Label), lab.Bubbles.Select(b => b.Label));
        Assert.Equal("4.2", Bubble(lab, "expert-system").Number);
        Assert.Equal("1.2", Bubble(lab, "architecture-map").Number);
        var neural = lab.Bubbles.Single(b => b.Id == "neural");
        Assert.Null(neural.Number);
        Assert.False(neural.HasLesson);
        Assert.True(Bubble(lab, "expert-system").HasLesson);
        Assert.Equal("1.2 Transformer, family member", Bubble(lab, "architecture-map").AutomationName);
        Assert.Equal("Neural networks, group", neural.AutomationName);
    }

    [Fact]
    public void Bubbles_ForPlaceholderLessons_AreLater()
    {
        var lab = NewLab();
        // The first tree node whose lesson is still a placeholder (found from the catalog, so building lessons does not break this).
        var node = Tree.Nodes.First(n => n.LessonId != null && Catalog.Get(n.LessonId).IsPlaceholder);
        var later = Bubble(lab, node.LessonId!);
        string number = Catalog.NumberOf(Catalog.Get(node.LessonId!));
        Assert.True(later.IsLater);
        Assert.Equal($"{number} {node.Label}, not built yet", later.AutomationName);
        Assert.Equal($"{number}  later", later.HeadText);   // "later" in words, not colour alone
        // A built section 4 lesson's bubble is not later.
        Assert.False(Catalog.Get("image-filters").IsPlaceholder);
        Assert.False(Bubble(lab, "image-filters").IsLater);
        Assert.Equal("4.6", Bubble(lab, "image-filters").HeadText);
        Assert.Equal("1.2", Bubble(lab, "architecture-map").HeadText);
        Assert.Equal("", lab.Bubbles.Single(b => b.Id == "neural").HeadText);
        Assert.Equal("whole", Bubble(lab, "architecture-map").Part);
        Assert.Equal("family", lab.Bubbles.Single(b => b.Id == "neural").Part);
        Assert.False(Bubble(lab, "architecture-map").IsLater);
        Assert.False(lab.Bubbles.Single(b => b.Id == "neural").IsLater);
    }

    [Fact]
    public void Arrange_LaysOutOnce_InTreeMode_WithTheRootAsCentre()
    {
        var layout = new CountingLayout();
        var lab = NewLab(layout);
        Assert.Equal(0, layout.Runs);
        Assert.Null(lab.Layout);

        lab.Arrange(Sizes(lab));
        Assert.Equal(1, layout.Runs);
        Assert.NotNull(lab.Layout);
        var input = layout.Last!;
        Assert.Equal(MapMode.Tree, input.Mode);
        Assert.Equal(Tree.Root.Id, input.CentreId);
        Assert.Equal(Tree.Nodes.Select(n => n.Id), input.Nodes.Select(n => n.Id));
        Assert.Equal(Tree.Nodes.Skip(1).Select(n => (n.Parent!, n.Id)), input.PlacementEdges.Select(e => (e.From, e.To)));
        Assert.Empty(input.RoutedOnlyEdges);
        Assert.Empty(input.GroupMembers);

        lab.Choose("rules");
        lab.ClearChoice();
        Assert.Equal(1, layout.Runs);   // choosing never lays out again
    }

    [Fact]
    public void Arrange_WithoutASizeForEveryBubble_Throws()
    {
        var lab = NewLab();
        var sizes = Sizes(lab);
        sizes.Remove("neural");
        Assert.Throws<ArgumentException>(() => lab.Arrange(sizes));
    }

    [Fact]
    public void Choose_ShowsTheCard()
    {
        var lab = NewLab();
        Assert.Null(lab.Card);
        lab.Choose("rules");
        var node = Tree.Nodes.Single(n => n.Id == "rules");
        Assert.Equal("rules", lab.ChosenId);
        var card = lab.Card!;
        Assert.Equal(node.Label, card.Title);
        Assert.Equal("expert-system", card.LessonId);
        Assert.Equal(Catalog.Get("expert-system").Title, card.LessonTitle);
        Assert.Equal(node.WhatItIs, card.WhatItIs);
        Assert.Equal(node.GoodAt, card.GoodAt);
        Assert.Equal(node.BadAt, card.BadAt);
        Assert.Equal(node.Example, card.Example);
        // 4.2 is built by the next task; until then its lesson is a placeholder and the button says so.
        Assert.Equal(Catalog.Get("expert-system").IsPlaceholder, card.LessonIsLater);
        Assert.Equal($"Open lesson 4.2 Rules: an expert system{(card.LessonIsLater ? " (not built yet)" : "")}", card.OpenLessonText);

        lab.Choose("neural");
        Assert.Null(lab.Card!.LessonId);
        Assert.Null(lab.Card.OpenLessonText);
    }

    [Fact]
    public void Choose_UnknownId_Throws()
    {
        var lab = NewLab();
        Assert.Throws<ArgumentException>(() => lab.Choose("no-such-bubble"));
        Assert.Throws<ArgumentException>(() => lab.ChooseOrOpen("no-such-bubble"));
    }

    [Fact]
    public void ChooseOrOpen_SecondPressOpensTheLesson()
    {
        var lab = NewLab();
        Assert.Null(lab.ChooseOrOpen("rules"));
        Assert.Equal("rules", lab.ChosenId);
        Assert.Equal("expert-system", lab.ChooseOrOpen("rules"));
        Assert.Equal("rules", lab.ChosenId);

        Assert.Null(lab.ChooseOrOpen("neural"));
        Assert.Equal("neural", lab.ChosenId);
        Assert.Null(lab.ChooseOrOpen("neural"));
        Assert.Equal("neural", lab.ChosenId);
    }

    [Fact]
    public void Reset_ClearsTheChoice()
    {
        var lab = NewLab();
        lab.Choose("rules");
        lab.Reset();
        Assert.Null(lab.ChosenId);
        Assert.Null(lab.Card);
    }

    [Fact]
    public void CardScreen_LightsWhenABubbleIsChosen()
    {
        var lab = NewLab();
        Assert.Equal(["card"], lab.ReportedScreens);   // the baseline, reported in the constructor
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => raised.Add(e.Screen);

        lab.Choose("rules");
        Assert.Equal(["card"], raised);
        raised.Clear();
        lab.Choose("rules");
        Assert.Empty(raised);   // the same card
        lab.ClearChoice();
        Assert.Equal(["card"], raised);
    }

    [Theory]
    [InlineData(300, 90)]    // about 24 pt
    [InlineData(120, 40)]    // about 13 pt
    public void Arrange_At24pt_NoBoxesOverlap(double width, double height)
    {
        var lab = NewLab(new MsaglGraphLayout());
        lab.Arrange(Sizes(lab, width, height));
        var layout = lab.Layout!;
        Assert.Equal(Tree.Nodes.Count, layout.Nodes.Count);
        Assert.Equal(Tree.Nodes.Count - 1, layout.Edges.Count);
        var nodes = layout.Nodes;
        for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var a = nodes[i]; var b = nodes[j];
                bool apart = a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y;
                Assert.True(apart, $"'{a.Id}' and '{b.Id}' overlap.");
            }
        // Each row runs left to right in file order (a branch's members under it, the branches in order), so no lines cross.
        var depth = new Dictionary<string, int> { [Tree.Root.Id] = 0 };
        foreach (var n in Tree.Nodes.Skip(1)) depth[n.Id] = depth[n.Parent!] + 1;
        foreach (var row in Tree.Nodes.GroupBy(n => depth[n.Id]))
        {
            var xs = row.Select(n => nodes.Single(p => p.Id == n.Id).X).ToList();
            for (int i = 1; i < xs.Count; i++)
                Assert.True(xs[i] > xs[i - 1], $"Row {row.Key} is out of file order: {string.Join(", ", row.Select(n => n.Id))} at {string.Join(", ", xs)}.");
        }
        // Top-down: every child is below its parent.
        foreach (var n in Tree.Nodes.Skip(1))
            Assert.True(nodes.Single(p => p.Id == n.Id).Y > nodes.Single(p => p.Id == n.Parent).Y, $"'{n.Id}' is not below its parent.");
    }

    [Fact]
    public void Factory_BuildsTheFamilyTreeLab()
    {
        using var env = new TestEnvironment();
        var factory = new LabFactory(env.NewActiveModels(), TestEnvironment.Content, lessons: Catalog);
        var lab = Assert.IsType<FamilyTreeLabViewModel>(factory.Create(FamilyTreeLabViewModel.Id));
        Assert.Equal(Tree.Nodes.Count, lab.Bubbles.Count);
        Assert.Equal(FamilyTreeLabViewModel.Id, Catalog.Get("family-tree").LabId);
    }
}
