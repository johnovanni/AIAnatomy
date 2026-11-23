using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class ArchitectureMapTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private ArchitectureMapLabViewModel NewLab() =>
        new(_env.NewActiveModels());

    [Fact]
    public void TypingText_UpdatesTop10()
    {
        var lab = NewLab();
        Assert.Equal(10, lab.Top10.Count);
        var first = lab.Top10.Select(i => i.Label).ToList();
        Assert.InRange(lab.Top10.Sum(i => i.Value), 0.0, 1.0 + 1e-9);
        Assert.Equal(lab.Top10.OrderByDescending(i => i.Value).Select(i => i.Label), first);
    }

    [Fact]
    public void TypingMoreText_ChangesTheChoices()
    {
        var lab = NewLab();
        var before = lab.Top10.Select(i => i.Label).ToList();
        lab.Text = "The crow sat on the ";
        Assert.Equal(10, lab.Top10.Count);
        Assert.DoesNotContain(lab.Top10, i => i.Label == " ");   // a space is shown as the visible mark
        Assert.NotEqual(before, lab.Top10.Select(i => i.Label).ToList());
    }

    [Fact]
    public void UnknownCharacters_ShownAsReplacement_NoThrow()
    {
        var lab = NewLab();
        lab.Text = "Hi \U0001F642 é\t";
        Assert.Contains("�", lab.ShownText);
        Assert.Equal(10, lab.Top10.Count);
        Assert.Null(lab.Message);
    }

    [Fact]
    public void LongText_ShowsTruncationNote()
    {
        var lab = NewLab();
        lab.Text = new string('a', 70);
        Assert.Equal("Only the last 64 characters are used: that is this model's context length.", lab.Notice);
        lab.Text = "short";
        Assert.Null(lab.Notice);
    }

    [Fact]
    public void EmptyText_ShowsPrompt_NotAnError()
    {
        var lab = NewLab();
        lab.Text = "";
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Empty(lab.Top10);
        Assert.NotEmpty(lab.Nodes);   // the diagram still shows
    }

    [Fact]
    public void EveryLinkedBox_OpensARealLesson()
    {
        // NetworkDiagram navigates through its LinkLessonId; the catalog throws for an id that does not exist.
        var catalog = AIAnatomy.Lessons.LessonCatalog.Load(TestEnvironment.Content);
        var models = _env.NewActiveModels();
        var lab = new ArchitectureMapLabViewModel(models);
        foreach (var entry in models.Library.List())
        {
            models.SetActive(entry);
            lab.OnActiveModelChanged();
            foreach (var link in lab.Nodes.Select(n => n.LinkLessonId).Where(l => !string.IsNullOrEmpty(l)))
                Assert.Equal(link, catalog.Get(link!).Id);
        }
    }

    [Fact]
    public void Diagram_ShowsTheRealBlockStructure()
    {
        var lab = NewLab();
        bool Edge(string from, string to) => lab.Edges.Any(e => e.FromId == from && e.ToId == to);
        Assert.True(Edge("multihead", "residual"));     // each sublayer adds its result back into the residual stream
        Assert.True(Edge("feedforward", "residual"));
        Assert.True(Edge("attention", "multihead"));    // the heads sit side by side inside multi-head attention
        Assert.Null(lab.Edges.Single(e => e.FromId == "attention").Label);   // short arrow: no label to stray
        Assert.Contains("4 heads",lab.Nodes.Single(n => n.Id == "multihead").Value);
        Assert.False(Edge("multihead", "feedforward")); // not a chain: both sublayers work on the residual stream
        Assert.False(Edge("feedforward", "norm"));
        Assert.True(Edge("residual", "norm"));          // the final norm reads the residual stream after the last block
        // Final review M1: each sublayer also reads (a normalised copy of) the stream, so the picture shows its input.
        Assert.True(Edge("residual", "multihead"));
        Assert.True(Edge("residual", "feedforward"));
        // R37: the four short arrows carry no text (their labels were pushed beside other arrows); one caption says it instead.
        foreach (var (from, to) in new[] { ("residual", "multihead"), ("multihead", "residual"), ("residual", "feedforward"), ("feedforward", "residual") })
            Assert.True(string.IsNullOrEmpty(lab.Edges.Single(e => e.FromId == from && e.ToId == to).Label), $"{from} -> {to}");
        Assert.Equal("Attention and feed-forward each read a normalised copy of the residual stream and add their result back.", lab.DiagramCaption);
    }

    [Fact]
    public void AttentionOnlyModel_HasNoReadArrowToAFeedForwardBox()
    {
        var models = _env.NewActiveModels();
        var lab = new ArchitectureMapLabViewModel(models);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        Assert.Contains(lab.Edges, e => e.FromId == "residual" && e.ToId == "multihead");
        Assert.DoesNotContain(lab.Edges, e => e.ToId == "feedforward");
        Assert.Equal("Attention reads a normalised copy of the residual stream and adds its result back.", lab.DiagramCaption);
    }

    [Fact]
    public void ShownText_ShowsTheTextTheModelReads_WithReplacementMarks()
    {
        var lab = NewLab();
        Assert.Equal("The\u2423fox\u2423and\u2423the\u2423", lab.ShownText);   // final review I4: a space is shown as \u2423, as the caption says
        lab.Text = "a\U0001F642b";
        Assert.Equal("a\uFFFDb", lab.ShownText);
    }

    [Fact]
    public void LongText_ShownTextIsOnlyTheTokensTheModelUses()
    {
        // Final review I4: with a long text, "What the model reads" shows only the last ContextLength tokens.
        var models = _env.NewActiveModels();
        var lab = new ArchitectureMapLabViewModel(models);
        int context = models.Current.Transformer.Config.ContextLength;
        lab.Text = new string('a', 30) + new string('b', context);
        Assert.NotNull(lab.Notice);
        Assert.Equal(context, LabText.CountScalars(lab.ShownText));
        Assert.Equal(new string('b', context), lab.ShownText);
    }

    [Fact]
    public void Diagram_LinksEveryPartLesson_AndDrawsTheRealLayerCount()
    {
        var lab = NewLab();
        var links = lab.Nodes.Select(n => n.LinkLessonId).Where(l => l != null).ToHashSet();
        foreach (var id in new[] { "tokens", "embeddings", "position", "attention", "multi-head", "feed-forward", "residual-stream", "output-sampling" })
            Assert.Contains(id, links);
        Assert.Contains(lab.Nodes, n => n.Label == "Block × 2");
        Assert.All(lab.Edges, e =>
        {
            Assert.Contains(lab.Nodes, n => n.Id == e.FromId);
            Assert.Contains(lab.Nodes, n => n.Id == e.ToId);
        });
    }

    [Fact]
    public void InductionDemo_HasNoFeedForwardBox_AndItsOwnLayerCount()
    {
        var models = _env.NewActiveModels();
        var lab = new ArchitectureMapLabViewModel(models);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();

        var config = models.Current.Transformer.Config;
        Assert.False(config.UseFeedForward);
        Assert.DoesNotContain(lab.Nodes, n => n.LinkLessonId == "feed-forward");
        Assert.Contains(lab.Nodes, n => n.Label == $"Block × {config.Layers}");
        Assert.All(lab.Edges, e => Assert.Contains(lab.Nodes, n => n.Id == e.ToId));

        lab.Text = "ABCD";
        Assert.Equal(10, lab.Top10.Count);
    }

    [Fact]
    public void Reset_RestoresDefaultText()
    {
        var lab = NewLab();
        lab.Text = "x";
        lab.Reset();
        Assert.Equal(ArchitectureMapLabViewModel.DefaultText, lab.Text);
    }
}
