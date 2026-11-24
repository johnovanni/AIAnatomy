namespace AIAnatomy.Lessons.Tests;

public class IdeaMapDataTests
{
    private static readonly string Content = Path.Combine(AppContext.BaseDirectory, "content");
    private static readonly LessonCatalog Catalog = LessonCatalog.Load(Content);
    private static readonly IdeaMapData Map = IdeaMapData.Load(Path.Combine(Content, "idea-map.json"));

    [Fact]
    public void RealMap_HasABoxForEveryLessonAndTheMapOnlyIdeas()
    {
        var ids = Map.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var lesson in Catalog.All) Assert.Contains(lesson.Id, ids);
        foreach (var id in IdeaMapData.MapOnlyIds) Assert.Contains(id, ids);
        Assert.Equal(Catalog.All.Count + IdeaMapData.MapOnlyIds.Count, Map.Nodes.Count);
    }

    [Fact]
    public void RealMap_FlowEdgesAndGroupAreAsDesigned()
    {
        Assert.Equal(8, Map.FlowEdges.Count);
        // The Block group is on the flow: the stream goes into it (an edge to the group box, drawn to its frame).
        Assert.Contains(Map.FlowEdges, e => e is { From: "residual-stream", To: "block", Label: "each block reads it and adds back" });
        Assert.Contains(Map.FlowEdges, e => e is { From: "residual-stream", To: "output-sampling", Label: "after the last block" });
        Assert.Empty(Map.Validate(Catalog));
        Assert.Contains(Map.FlowEdges, e => e is { From: "next-character", To: "your-text", Label: "added to the text" });
        Assert.Equal(["attention", "multi-head", "feed-forward"], Map.GroupMembers);
        Assert.Single(Map.Nodes, n => n.Kind == IdeaMapData.Centre);
        Assert.Single(Map.Nodes, n => n.Kind == IdeaMapData.Group);
    }

    private static IdeaMapData Make(IEnumerable<IdeaNode> extra, IEnumerable<FlowEdge>? edges = null, IEnumerable<string>? members = null) =>
        IdeaMapData.Create(Map.Nodes.Concat(extra), edges ?? Map.FlowEdges, members ?? Map.GroupMembers);

    private static void AssertProblem(IdeaMapData map, string text) =>
        Assert.Contains(map.Validate(Catalog), p => p.Contains(text));

    private static IdeaNode Recast(string id, string kind) =>
        Map.Nodes.Single(n => n.Id == id) with { Kind = kind };

    private static IEnumerable<IdeaNode> Replace(string id, IdeaNode node) =>
        Map.Nodes.Select(n => n.Id == id ? node : n);

    [Fact]
    public void RepeatedId_IsAProblem() =>
        AssertProblem(Make([new IdeaNode("tokens", "Again", "Twice.", "flow")]), "empty or repeated id");

    [Fact]
    public void NodeThatIsNotALesson_IsAProblem() =>
        AssertProblem(Make([new IdeaNode("invented", "X", "Not a lesson.", "flow")]), "not a lesson id");

    [Fact]
    public void UnknownFlowEdgeEnd_IsAProblem() =>
        AssertProblem(Make([], [new FlowEdge("tokens", "nowhere", "go")]), "unknown 'to' box");

    [Fact]
    public void UnknownGroupMember_IsAProblem() =>
        AssertProblem(Make([], null, ["tokens", "nowhere"]), "group member 'nowhere'");

    [Fact]
    public void LessonWithoutABox_IsAProblem() =>
        AssertProblem(IdeaMapData.Create(Map.Nodes.Where(n => n.Id != "scale"), Map.FlowEdges, Map.GroupMembers), "lesson 'scale' has no box");

    [Fact]
    public void TwoCentres_AreAProblem()
    {
        // An existing box is recast, so no other rule is involved.
        var map = IdeaMapData.Create(Replace("your-text", Recast("your-text", IdeaMapData.Centre)), Map.FlowEdges, Map.GroupMembers);
        var problems = map.Validate(Catalog);
        Assert.Equal(["the map needs exactly one centre box."], problems);
    }

    [Fact]
    public void NoCentre_IsAProblem()
    {
        var map = IdeaMapData.Create(Replace("centre", Recast("centre", IdeaMapData.Flow)), Map.FlowEdges, Map.GroupMembers);
        Assert.Equal(["the map needs exactly one centre box."], map.Validate(Catalog));
    }
}
