using AIAnatomy.Lessons.Markdown;

namespace AIAnatomy.Lessons.Tests;

public class LessonCatalogTests
{
    private static readonly string Content = Path.Combine(AppContext.BaseDirectory, "content");

    private static readonly string[] Expected =
    {
        "rules-vs-learning", "architecture-map",
        "tokens", "embeddings", "position", "attention", "multi-head", "feed-forward", "residual-stream", "output-sampling", "learning",
        "train", "step-through", "scale",
        "family-coming", "beyond-coming",
    };

    [Fact]
    public void Loads_AllStage1Lessons()
    {
        var catalog = LessonCatalog.Load(Content);
        Assert.Equal(Expected, catalog.All.Select(l => l.Id).ToArray());
        Assert.Equal(2, catalog.InSection(Section.Map).Count);
        Assert.Equal(9, catalog.InSection(Section.Parts).Count);
        Assert.Equal(3, catalog.InSection(Section.Whole).Count);
        Assert.Equal("AttentionLab", catalog.Get("attention").LabId);
        Assert.Equal("ComingLaterLab", catalog.Get("beyond-coming").LabId);
    }

    [Fact]
    public void Get_UnknownId_ThrowsNamingTheId()
    {
        var ex = Assert.Throws<KeyNotFoundException>(() => LessonCatalog.Load(Content).Get("no-such-lesson"));
        Assert.Contains("no-such-lesson", ex.Message);
    }

    [Fact]
    public void EveryLessonLink_PointsToExistingLesson()
    {
        var catalog = LessonCatalog.Load(Content);
        var links = new List<string>();
        foreach (var lesson in catalog.All)
            foreach (var doc in new[] { lesson.Explanation, lesson.Formula, lesson.BrainsAndModels })
                if (doc != null) links.AddRange(LinkIds(doc));
        Assert.NotEmpty(links);
        foreach (var id in links) Assert.Equal(id, catalog.Get(id).Id);
    }

    private static IEnumerable<string> LinkIds(MarkdownDocument doc)
    {
        foreach (var block in doc.Blocks)
        {
            IEnumerable<MarkdownInline> inlines = block switch
            {
                Paragraph p => p.Inlines,
                Heading h => h.Inlines,
                BulletList b => b.Items.SelectMany(i => i),
                _ => Array.Empty<MarkdownInline>(),
            };
            foreach (var l in inlines.OfType<LessonLink>()) yield return l.LessonId;
        }
    }

    [Fact]
    public void EverySourceId_Resolves()
    {
        var catalog = LessonCatalog.Load(Content);
        var sources = SourceList.Load(Path.Combine(Content, "sources.json"));
        Assert.Equal(12, sources.All.Count);
        foreach (var lesson in catalog.All)
            foreach (var id in lesson.SourceIds)
                Assert.NotNull(sources.Find(id));
        Assert.Null(sources.Find("textbook"));
    }

    [Fact]
    public void TextbookSource_IsFlaggedNotListed()
    {
        var catalog = LessonCatalog.Load(Content);
        var rules = catalog.Get("rules-vs-learning");
        Assert.True(rules.IsTextbookMaterial);
        Assert.Empty(rules.SourceIds);
        var tokens = catalog.Get("tokens");
        Assert.False(tokens.IsTextbookMaterial);
        Assert.Equal(new[] { "sennrich2016", "radford2019" }, tokens.SourceIds.ToArray());
    }

    [Fact]
    public void AttentionLesson_HasFormulaAndBrainsSections()
    {
        var attention = LessonCatalog.Load(Content).Get("attention");
        Assert.NotNull(attention.Formula);
        Assert.Contains("softmax", Assert.IsType<FormulaBlock>(attention.Formula!.Blocks.Single()).Latex);
        Assert.NotNull(attention.BrainsAndModels);
        Assert.DoesNotContain(attention.Explanation.Blocks, b => b is Heading);
    }

    [Fact]
    public void MissingOptionalSections_AreNull()
    {
        var tokens = LessonCatalog.Load(Content).Get("tokens");
        Assert.Null(tokens.Formula);
        Assert.Null(tokens.BrainsAndModels);
        Assert.NotEmpty(tokens.Explanation.Blocks);
    }

    [Fact]
    public void SectionNames_AreCaseInsensitive()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/01-a.md", "---\nid: a\nsection: PARTS\norder: 1\ntitle: A\nlab: ALab\nsources: textbook\n---\nBody");
        Assert.Equal(Section.Parts, LessonCatalog.Load(dir.Path).Get("a").Section);
    }

    [Theory]
    [InlineData("no front matter at all")]
    [InlineData("---\nid: a\nsection: parts\n")]
    [InlineData("---\nid: a\nsection: nowhere\norder: 1\ntitle: A\nlab: L\nsources: textbook\n---\nB")]
    [InlineData("---\nid: a\nsection: parts\norder: x\ntitle: A\nlab: L\nsources: textbook\n---\nB")]
    [InlineData("---\nsection: parts\norder: 1\ntitle: A\nlab: L\nsources: textbook\n---\nB")]
    public void BadFrontMatter_ThrowsWithFileName(string text)
    {
        using var dir = new TempContent();
        dir.Write("lessons/parts/07-broken-file.md", text);
        var ex = Assert.Throws<InvalidDataException>(() => LessonCatalog.Load(dir.Path));
        Assert.Contains("07-broken-file.md", ex.Message);
    }

    [Fact]
    public void DuplicateIds_Throw()
    {
        using var dir = new TempContent();
        var text = "---\nid: a\nsection: parts\norder: 1\ntitle: A\nlab: L\nsources: textbook\n---\nB";
        dir.Write("lessons/parts/01-a.md", text);
        dir.Write("lessons/parts/02-a.md", text);
        Assert.Throws<InvalidDataException>(() => LessonCatalog.Load(dir.Path));
    }

    private sealed class TempContent : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aianatomy-" + Guid.NewGuid().ToString("N"));
        public TempContent() => Directory.CreateDirectory(Path);
        public void Write(string relative, string text)
        {
            var full = System.IO.Path.Combine(Path, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
