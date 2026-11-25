namespace AIAnatomy.Lessons.Tests;

public class FamilyTreeDataTests
{
    private static readonly string Content = Path.Combine(AppContext.BaseDirectory, "content");
    private static readonly LessonCatalog Catalog = LessonCatalog.Load(Content);
    private static readonly FamilyTreeData Tree = FamilyTreeData.Load(Path.Combine(Content, "family-tree.json"));

    private static FamilyNode N(string id, string? parent, string? lesson = null, string label = "L", string what = "It is something.") =>
        new(id, label, parent, lesson, what, lesson == null ? null : "good", lesson == null ? null : "bad", lesson == null ? null : "example");

    /// <summary>A small valid tree: a root with three branches.</summary>
    private static List<FamilyNode> Small() =>
    [
        N("root", null), N("a", "root"), N("b", "root"), N("c", "root"),
    ];

    private static void AssertProblem(IEnumerable<FamilyNode> nodes, string text) =>
        Assert.Contains(FamilyTreeData.Create(nodes).Validate(Catalog), p => p.Contains(text));

    [Fact]
    public void SmallTree_IsValid() => Assert.Empty(FamilyTreeData.Create(Small()).Validate(Catalog));

    [Fact]
    public void ShippedTree_IsValid() => Assert.Empty(Tree.Validate(Catalog));

    [Fact]
    public void ShippedTree_HasTheThreeTopBranches() =>
        Assert.Equal(["Written by a person", "Learned from examples", "Learned from reward"],
            Tree.ChildrenOf(Tree.Root.Id).Select(n => n.Label));

    [Fact]
    public void ShippedTree_LinksTheEightMembers()
    {
        var ids = Tree.Nodes.Where(n => n.LessonId != null).Select(n => n.LessonId!).ToHashSet();
        Assert.Equal(new HashSet<string>(["expert-system", "search", "classic-ml", "learning-by-reward",
            "image-filters", "memory-networks", "architecture-map", "diffusion-shapes"]), ids);
        var transformer = Tree.Nodes.Single(n => n.LessonId == "architecture-map");
        Assert.Equal("Transformer", transformer.Label);
        var neural = Tree.Nodes.Single(n => n.Id == transformer.Parent);
        Assert.Equal("Neural networks", neural.Label);
        Assert.Equal("Learned from examples", Tree.Nodes.Single(n => n.Id == neural.Parent).Label);
    }

    [Fact]
    public void RepeatedId_IsAProblem()
    {
        var nodes = Small(); nodes.Add(N("a", "root"));
        AssertProblem(nodes, "repeated id");
    }

    [Fact]
    public void SecondRoot_IsAProblem()
    {
        var nodes = Small(); nodes.Add(N("x", null));
        AssertProblem(nodes, "exactly one root");
    }

    [Fact]
    public void RootNotFirst_IsAProblem()
    {
        var nodes = Small(); (nodes[0], nodes[1]) = (nodes[1], nodes[0]);
        AssertProblem(nodes, "the root must come first");
    }

    [Fact]
    public void UnknownParent_IsAProblem()
    {
        var nodes = Small(); nodes.Add(N("x", "nowhere"));
        AssertProblem(nodes, "unknown parent");
    }

    [Fact]
    public void Cycle_IsAProblem()
    {
        var nodes = Small(); nodes.Add(N("x", "y")); nodes.Add(N("y", "x"));
        AssertProblem(nodes, "cycle");
    }

    [Fact]
    public void EmptyLabel_IsAProblem()
    {
        var nodes = Small(); nodes[1] = N("a", "root", label: " ");
        AssertProblem(nodes, "empty label");
    }

    [Fact]
    public void EmptyWhatItIs_IsAProblem()
    {
        var nodes = Small(); nodes[1] = N("a", "root", what: "");
        AssertProblem(nodes, "what it is");
    }

    [Theory]
    [InlineData("goodAt")]
    [InlineData("badAt")]
    [InlineData("example")]
    public void LessonNodeMissingACardPart_IsAProblem(string missing)
    {
        var full = N("a", "root", "search");
        var node = missing switch { "goodAt" => full with { GoodAt = null }, "badAt" => full with { BadAt = " " }, _ => full with { Example = null } };
        var nodes = Small(); nodes[1] = node;
        AssertProblem(nodes, "good at, bad at and an example");
    }

    [Fact]
    public void UnknownLesson_IsAProblem()
    {
        var nodes = Small(); nodes[1] = N("a", "root", "no-such-lesson");
        AssertProblem(nodes, "not a lesson");
    }

    [Fact]
    public void LessonUsedTwice_IsAProblem()
    {
        var nodes = Small(); nodes[1] = N("a", "root", "search"); nodes[2] = N("b", "root", "search");
        AssertProblem(nodes, "used twice");
    }

    [Fact]
    public void RootWithOtherThanThreeChildren_IsAProblem()
    {
        var nodes = Small(); nodes.RemoveAt(3);
        AssertProblem(nodes, "three branches");
    }

    [Fact]
    public void Load_BadJson_NamesTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bad-family-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => FamilyTreeData.Load(path));
            Assert.Contains(Path.GetFileName(path), ex.Message);
        }
        finally { File.Delete(path); }
    }
}
