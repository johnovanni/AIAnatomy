using System.Text;
using AIAnatomy.Lessons.Markdown;

namespace AIAnatomy.Lessons.Tests;

/// <summary>Checks on the lesson texts themselves: length, formulas, sources, and Markdown that renders cleanly.</summary>
public class LessonContentTests
{
    private static readonly string Content = Path.Combine(AppContext.BaseDirectory, "content");
    private static readonly LessonCatalog Catalog = LessonCatalog.Load(Content);
    private static readonly SourceList Sources = SourceList.Load(Path.Combine(Content, "sources.json"));

    // Tone rules for the explanation pane: plain explanations of 150 to 350 words, short "Brains and models" notes.
    private const int MinExplanationWords = 150, MaxExplanationWords = 350;
    private const int MinBrainsWords = 60, MaxBrainsWords = 150;
    // A "coming later" placeholder is one short paragraph (plus its source line) and is held to a lower minimum.
    private const int MinPlaceholderWords = 40;

    /// <summary>The lessons still "coming later". Each lab task removes its own id when it turns its lesson into a built one.</summary>
    private static readonly string[] StillComingLater =
    [
        "beyond-coming", "expert-system", "search", "classic-ml", "learning-by-reward",
        "image-filters", "image-network", "memory-networks", "diffusion-shapes", "diffusion-steps",
    ];

    [Fact]
    public void EveryLessonHasExplanationOver80Words()
    {
        var tooShort = Catalog.All.Where(l => !l.IsPlaceholder && WordCount(l.Explanation) <= 80).Select(Describe).ToList();
        Assert.True(tooShort.Count == 0, "Explanations of 80 words or fewer: " + string.Join(", ", tooShort));
    }

    [Fact]
    public void ExplanationsAndNotes_KeepToTheirLengths()
    {
        var problems = new List<string>();
        foreach (var lesson in Catalog.All)
        {
            int words = WordCount(lesson.Explanation);
            if (words < (lesson.IsPlaceholder ? MinPlaceholderWords : MinExplanationWords) || words > MaxExplanationWords)
                problems.Add($"{lesson.Id} explanation has {words} words");
            if (lesson.BrainsAndModels is { } brains && WordCount(brains) is var b and (< MinBrainsWords or > MaxBrainsWords))
                problems.Add($"{lesson.Id} brains-and-models note has {b} words");
        }
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void EveryPartsAndWholeLessonHasFormula()
    {
        var lessons = Catalog.All.Where(l => l.Section is Section.Parts or Section.Whole).ToList();
        Assert.Equal(12, lessons.Count);
        foreach (var lesson in lessons)
        {
            Assert.True(lesson.Formula != null, $"{lesson.Id} has no formula section");
            Assert.True(lesson.Formula!.Blocks.OfType<FormulaBlock>().Any(), $"{lesson.Id}'s formula section has no $$ formula");
            // Each formula section also names its symbols in words.
            Assert.True(lesson.Formula.Blocks.Any(b => b is Paragraph or BulletList), $"{lesson.Id}'s formula section does not name its symbols");
        }
    }

    [Fact]
    public void EveryLessonCitesAtLeastOneSourceOrIsMarkedTextbook()
    {
        foreach (var lesson in Catalog.All)
            Assert.True(lesson.SourceIds.Count > 0 || lesson.IsTextbookMaterial, $"{lesson.Id} cites nothing and is not marked textbook");
    }

    /// <summary>The app does not list a lesson's sources itself, so each cited source must be named in the lesson's own text.</summary>
    [Fact]
    public void EveryCitedSource_IsNamedInTheLessonText()
    {
        var missing = new List<string>();
        foreach (var lesson in Catalog.All)
        {
            string text = AllText(lesson);
            foreach (var id in lesson.SourceIds)
            {
                string name = FirstAuthor(Sources.Find(id)!.Citation);
                if (!text.Contains(name, StringComparison.Ordinal)) missing.Add($"{lesson.Id} cites {id} but never names {name}");
            }
            if (lesson.IsTextbookMaterial && !text.Contains("textbook", StringComparison.Ordinal))
                missing.Add($"{lesson.Id} is marked textbook but does not say so");
        }
        Assert.True(missing.Count == 0, string.Join("; ", missing));
    }

    /// <summary>Nested or unclosed markers are shown as literal characters by the lenient parser; none may reach the screen.</summary>
    [Fact]
    public void NoLessonText_ShowsMarkdownMarkers()
    {
        var leaks = new List<string>();
        foreach (var lesson in Catalog.All)
            foreach (var doc in Docs(lesson))
                foreach (var inline in Inlines(doc))
                {
                    string shown = inline switch { TextRun t => t.Text, BoldRun b => b.Text, ItalicRun i => i.Text, LessonLink l => l.Text, _ => "" };
                    foreach (var marker in new[] { "*", "[", "](", "$$", "##" })
                        if (shown.Contains(marker, StringComparison.Ordinal)) leaks.Add($"{lesson.Id}: \"{marker}\" in \"{shown}\"");
                }
        Assert.True(leaks.Count == 0, string.Join("; ", leaks));
    }

    /// <summary>Every lesson that is not a "coming later" placeholder.</summary>
    private static IEnumerable<Lesson> BuiltLessons => Catalog.All.Where(l => !l.IsPlaceholder);

    [Fact]
    public void EveryBuiltLesson_HasIdeaPartTryThisAndTwoToThreeQuestions()
    {
        // Only the "coming later" lessons are placeholders, so every other lesson is held to the checks below.
        Assert.Equal(StillComingLater.Order(StringComparer.Ordinal), Catalog.All.Where(l => l.IsPlaceholder).Select(l => l.Id).Order(StringComparer.Ordinal));
        var problems = new List<string>();
        foreach (var lesson in BuiltLessons)
        {
            if (string.IsNullOrWhiteSpace(lesson.Idea)) problems.Add($"{lesson.Id} has no idea");
            if (!Parts.IsKnown(lesson.Part)) problems.Add($"{lesson.Id} has no known part");
            if (lesson.TryThis.Count == 0) problems.Add($"{lesson.Id} has no Try this");
            if (lesson.Questions.Count is < 2 or > 3) problems.Add($"{lesson.Id} has {lesson.Questions.Count} questions");
        }
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void BigIdea_IsOneSentence_UnderTwentyFiveWords()
    {
        var problems = new List<string>();
        foreach (var lesson in BuiltLessons)
        {
            string idea = lesson.Idea ?? "";
            int words = idea.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            if (words >= 25) problems.Add($"{lesson.Id} idea has {words} words");
            if (!idea.EndsWith('.')) problems.Add($"{lesson.Id} idea does not end with a full stop");
            // A second sentence shows as an end mark followed by a space inside the text.
            if (System.Text.RegularExpressions.Regex.IsMatch(idea.TrimEnd('.'), @"[.!?]\s")) problems.Add($"{lesson.Id} idea is more than one sentence");
        }
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void TryThis_HasTwoToFourSteps()
    {
        var problems = BuiltLessons.Where(l => l.TryThis.Count is < 2 or > 4).Select(l => $"{l.Id} has {l.TryThis.Count} steps").ToList();
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void PlaceholderLessons_HaveNoIdeaTryThisOrQuestions()
    {
        foreach (var lesson in Catalog.All.Where(l => l.IsPlaceholder))
        {
            Assert.Null(lesson.Idea);
            Assert.Empty(lesson.TryThis);
            Assert.Empty(lesson.Questions);
        }
    }

    /// <summary>The big idea, Try this steps and questions are shown as plain text, so Markdown markers would show as typed.</summary>
    [Fact]
    public void IdeasStepsAndQuestions_HaveNoMarkdownMarkers()
    {
        var leaks = new List<string>();
        foreach (var lesson in Catalog.All)
        {
            var texts = new List<string?> { lesson.Idea };
            texts.AddRange(lesson.TryThis);
            foreach (var q in lesson.Questions) { texts.Add(q.Prompt); texts.AddRange(q.Answers); texts.Add(q.Why); }
            foreach (var text in texts.Where(t => t != null))
                foreach (var marker in new[] { "**", "*", "[", "](", "$$", "##", "`" })
                    if (text!.Contains(marker, StringComparison.Ordinal)) leaks.Add($"{lesson.Id}: \"{marker}\" in \"{text}\"");
        }
        Assert.True(leaks.Count == 0, string.Join("; ", leaks));
    }

    [Fact]
    public void ComingLaterLessons_HaveNoFormulaOrNote()
    {
        foreach (var id in StillComingLater)
        {
            var lesson = Catalog.Get(id);
            Assert.Null(lesson.Formula);
            Assert.Null(lesson.BrainsAndModels);
            if (id == "beyond-coming") Assert.Contains(lesson.Explanation.Blocks, b => b is BulletList);
        }
    }

    private static string Describe(Lesson l) => $"{l.Id} ({WordCount(l.Explanation)})";

    private static string FirstAuthor(string citation)
    {
        int end = citation.IndexOfAny([',', '(']);
        return (end < 0 ? citation : citation[..end]).Trim();
    }

    private static IEnumerable<MarkdownDocument> Docs(Lesson l) =>
        new[] { l.Explanation, l.Formula, l.BrainsAndModels }.Where(d => d != null)!;

    private static IEnumerable<MarkdownInline> Inlines(MarkdownDocument doc) =>
        doc.Blocks.SelectMany(block => block switch
        {
            Paragraph p => p.Inlines,
            Heading h => h.Inlines,
            BulletList b => b.Items.SelectMany(i => i),
            _ => Array.Empty<MarkdownInline>(),
        });

    private static string Text(MarkdownDocument doc)
    {
        var sb = new StringBuilder();
        foreach (var inline in Inlines(doc))
            sb.Append(inline switch { TextRun t => t.Text, BoldRun b => b.Text, ItalicRun i => i.Text, LessonLink l => l.Text, _ => "" }).Append(' ');
        return sb.ToString();
    }

    private static string AllText(Lesson l) => string.Join(" ", Docs(l).Select(Text));

    private static int WordCount(MarkdownDocument doc) =>
        Text(doc).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Count(w => w.Any(char.IsLetterOrDigit));
}
