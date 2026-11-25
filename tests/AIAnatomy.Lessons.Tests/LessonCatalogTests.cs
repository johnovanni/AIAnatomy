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
        "family-tree", "expert-system", "search", "classic-ml", "learning-by-reward",
        "image-filters", "image-network", "memory-networks", "diffusion-shapes", "diffusion-steps",
        "beyond-coming",
    };

    private const string Header = "---\nid: a\nsection: parts\norder: 1\ntitle: A\nlab: ALab\nsources: textbook\n";

    [Fact]
    public void Parse_ReadsIdeaPartBranches()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/01-a.md", Header + "idea: Each token gathers.\npart: attention\nbranches: By hand=toy; In the model=heads\n---\nBody");
        var a = LessonCatalog.Load(dir.Path).Get("a");
        Assert.Equal("Each token gathers.", a.Idea);
        Assert.Equal("attention", a.Part);
        Assert.Equal([new LessonBranch("By hand", "toy"), new LessonBranch("In the model", "heads")], a.Branches);
        Assert.False(a.IsPlaceholder);
    }

    [Fact]
    public void Parse_BranchAnchorUsedByTwoLessons_Throws()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/01-a.md", Header + "branches: One=toy\n---\nBody");
        dir.Write("lessons/x/02-b.md", Header.Replace("id: a", "id: b").Replace("order: 1", "order: 2") + "branches: Two=toy\n---\nBody");
        var e = Assert.Throws<InvalidDataException>(() => LessonCatalog.Load(dir.Path));
        Assert.Contains("'toy'", e.Message);
        Assert.Contains("'a'", e.Message);
        Assert.Contains("'b'", e.Message);
    }

    [Fact]
    public void Parse_NoNewFields_GivesEmptyDefaults()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/01-a.md", Header + "---\nBody");
        var a = LessonCatalog.Load(dir.Path).Get("a");
        Assert.Null(a.Idea);
        Assert.Null(a.Part);
        Assert.Empty(a.Branches);
        Assert.Empty(a.TryThis);
        Assert.Empty(a.Questions);
    }

    [Fact]
    public void Parse_TryThisAndCheckYourself_AreNotPartOfExplanation()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/01-a.md", Header + "branches: In the model=heads\n---\nExplained here.\n\n## Try this\n1. Drag Q.\n2. Watch weight 2 grow.\n\n" +
            "## Check yourself\n? Which?\n- one\n* two\nwhy: Because.\nsee: heads\n");
        var a = LessonCatalog.Load(dir.Path).Get("a");
        Assert.Equal(["Drag Q.", "Watch weight 2 grow."], a.TryThis);
        Assert.Single(a.Questions);
        Assert.Single(a.Explanation.Blocks);
        var text = string.Join(" ", a.Explanation.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines).Select(i => i.ToString()));
        Assert.Contains("Explained", text);
        Assert.DoesNotContain("Drag", text);
        Assert.DoesNotContain("Which", text);
    }

    [Fact]
    public void Parse_TryThisLineThatIsNotAStep_FailsNamingTheLesson()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/05-odd.md", Header + "---\nBody\n\n## Try this\n1. Drag Q.\n\nWatch weight 2 grow.\n");
        var ex = Assert.Throws<InvalidDataException>(() => LessonCatalog.Load(dir.Path));
        Assert.Equal("05-odd.md: lesson 'a': the Try this line 'Watch weight 2 grow.' is not a numbered step like '1. Do this.'.", ex.Message);
    }

    [Fact]
    public void Parse_TryThisFollowedByFormula_EndsAtTheFormulaHeading()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/01-a.md", Header + "---\nBody\n\n## Try this\n1. Drag Q.\n2. Watch weight 2 grow.\n\n## Formula\n\n$$\nx = 1\n$$\n\n- x: a number.\n");
        var a = LessonCatalog.Load(dir.Path).Get("a");
        Assert.Equal(["Drag Q.", "Watch weight 2 grow."], a.TryThis);
        Assert.NotNull(a.Formula);
        Assert.Single(a.Formula!.Blocks.OfType<FormulaBlock>());
        Assert.Single(a.Formula.Blocks.OfType<BulletList>());
    }

    [Fact]
    public void Parse_CheckYourselfFollowedByFormula_EndsAtTheFormulaHeading()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/01-a.md", Header + "---\nBody\n\n## Check yourself\n? Which?\n- one\n* two\nwhy: Because.\nsee: formula\n\n" +
            "## Formula\n\n$$\nx = 1\n$$\n\n- x: a number.\n");
        var a = LessonCatalog.Load(dir.Path).Get("a");
        var q = Assert.Single(a.Questions);
        Assert.Equal(["one", "two"], q.Answers);
        Assert.NotNull(a.Formula);
        Assert.Single(a.Formula!.Blocks.OfType<FormulaBlock>());
        Assert.Single(a.Formula.Blocks.OfType<BulletList>());
    }

    [Fact]
    public void Parse_UnknownPart_FailsNamingTheFile()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/05-odd.md", Header + "part: nonsense\n---\nBody");
        var ex = Assert.Throws<InvalidDataException>(() => LessonCatalog.Load(dir.Path));
        Assert.Contains("05-odd.md", ex.Message);
        Assert.Contains("part", ex.Message);
    }

    [Fact]
    public void Parse_BadQuestion_NamesFileAndQuestion()
    {
        using var dir = new TempContent();
        dir.Write("lessons/x/05-odd.md", Header + "---\nBody\n## Check yourself\n? Q?\n- a\n* b\nwhy: x\nsee: nowhere\n");
        var ex = Assert.Throws<InvalidDataException>(() => LessonCatalog.Load(dir.Path));
        Assert.Contains("05-odd.md", ex.Message);
        Assert.Contains("question 1", ex.Message);
    }

    [Fact]
    public void Numbering_FollowsSectionAndOrder()
    {
        var catalog = LessonCatalog.Load(Content);
        Assert.Equal("1.1", catalog.NumberOf(catalog.Get("rules-vs-learning")));
        Assert.Equal("2.1", catalog.NumberOf(catalog.Get("tokens")));
        Assert.Equal("2.4", catalog.NumberOf(catalog.Get("attention")));
        Assert.Equal("3.3", catalog.NumberOf(catalog.Get("scale")));
        Assert.Equal(catalog.All, catalog.InContentsOrder);
    }

    [Fact]
    public void Placeholders_AreMarked()
    {
        var catalog = LessonCatalog.Load(Content);
        Assert.True(catalog.Get("diffusion-steps").IsPlaceholder);   // a Part B lesson
        Assert.False(catalog.Get("family-tree").IsPlaceholder);
        Assert.True(catalog.Get("beyond-coming").IsPlaceholder);
        Assert.False(catalog.Get("attention").IsPlaceholder);
    }

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
        Assert.Equal(28, sources.All.Count);   // rombach2022 added in the final review (M7), shortliffe1976 in Stage 2A Task 7, shannon1950 in Task 9, breiman1984 and coverhart1967 in Task 11, watkins1992 and schultz1997 in Task 13
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
        // A lesson may cite sources and also be marked textbook; "textbook" itself is never a source id.
        var tokens = catalog.Get("tokens");
        Assert.True(tokens.IsTextbookMaterial);
        Assert.Equal(new[] { "sennrich2016", "radford2019", "gemma2024" }, tokens.SourceIds.ToArray());
    }

    [Fact]
    public void AttentionLesson_HasFormulaAndBrainsSections()
    {
        var attention = LessonCatalog.Load(Content).Get("attention");
        Assert.NotNull(attention.Formula);
        var main = attention.Formula!.Blocks.OfType<FormulaBlock>().First().Latex;
        Assert.Contains("softmax", main);
        Assert.Contains("+ M", main);   // the causal mask is part of the formula
        Assert.NotNull(attention.BrainsAndModels);
        Assert.DoesNotContain(attention.Explanation.Blocks, b => b is Heading);
    }

    [Fact]
    public void MissingOptionalSections_AreNull()
    {
        var coming = LessonCatalog.Load(Content).Get("beyond-coming");
        Assert.Null(coming.Formula);
        Assert.Null(coming.BrainsAndModels);
        Assert.NotEmpty(coming.Explanation.Blocks);
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
