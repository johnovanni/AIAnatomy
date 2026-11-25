namespace AIAnatomy.Lessons.Tests;

public class ConnectionsTests
{
    private static readonly string Content = Path.Combine(AppContext.BaseDirectory, "content");
    private static readonly LessonCatalog Catalog = LessonCatalog.Load(Content);
    private static readonly SourceList Sources = SourceList.Load(Path.Combine(Content, "sources.json"));
    private static readonly IdeaMapData Map = IdeaMapData.Load(Path.Combine(Content, "idea-map.json"));
    private static readonly ConnectionList Real = ConnectionList.Load(Path.Combine(Content, "connections.json"));

    private static Connection C(string from, string to, string sentence = "A sentence.", params string[] sources) =>
        new(from, to, sentence, sources.Length == 0 ? ["textbook"] : sources);

    private static IReadOnlyList<string> Check(params Connection[] list) =>
        ConnectionList.Create(list).Validate(Catalog, Sources, Map);

    [Fact]
    public void RealConnections_Validate()
    {
        Assert.Empty(Real.Validate(Catalog, Sources, Map));
        Assert.Empty(Map.Validate(Catalog));
        Assert.Equal(29, Real.All.Count);   // expert-system -> rules-vs-learning added in Stage 2A Task 7, search -> learning-by-reward in Task 9, classic-ml -> learning in Task 11, learning-by-reward -> learning in Task 13; memory-networks -> attention and -> residual-stream in Stage 2B Task 15; image-filters -> feed-forward and -> image-network in Task 13
    }

    [Fact]
    public void Of_FindsEitherEnd()
    {
        var embeddings = Real.Of("embeddings");
        Assert.Contains(embeddings, c => c.From == "tokens");   // as the "to" end
        Assert.Contains(embeddings, c => c.From == "learning"); // as the "to" end
        Assert.Contains(embeddings, c => c.To == "residual-stream"); // as the "from" end
        Assert.Empty(Real.Of("no-such-idea"));
    }

    [Fact]
    public void UnknownEnd_IsAProblem()
    {
        Assert.NotEmpty(Check(C("nonsense", "tokens")));
        Assert.NotEmpty(Check(C("tokens", "nonsense")));
    }

    [Fact]
    public void UnknownSource_IsAProblem() => Assert.NotEmpty(Check(C("tokens", "embeddings", "A sentence.", "nobody1999")));

    [Fact]
    public void EmptySentence_IsAProblem() => Assert.NotEmpty(Check(C("tokens", "embeddings", "  ")));

    [Fact]
    public void NoSource_IsAProblem() =>
        Assert.NotEmpty(Check(new Connection("tokens", "embeddings", "A sentence.", [])));

    [Fact]
    public void LoopIsAllowedOnlyAsTo()
    {
        Assert.Empty(Check(C("scale", ConnectionList.WholeLoop)));
        var problems = Check(C(ConnectionList.WholeLoop, "scale"));
        Assert.Contains(problems, p => p.Contains("may only be the 'to' end"));
        Assert.DoesNotContain(problems, p => p.Contains("unknown"));
    }

    [Fact]
    public void DuplicatePair_IsAProblem()
    {
        Assert.NotEmpty(Check(C("tokens", "embeddings"), C("tokens", "embeddings", "Another.")));
        Assert.NotEmpty(Check(C("tokens", "embeddings"), C("embeddings", "tokens", "Another.")));
        Assert.Empty(Check(C("tokens", "embeddings"), C("embeddings", "residual-stream")));
    }

    [Fact]
    public void SelfConnection_IsAProblem() => Assert.NotEmpty(Check(C("tokens", "tokens")));
}
