using AIAnatomy.Lessons.Markdown;

namespace AIAnatomy.Lessons;

public sealed class LessonCatalog
{
    public const string TextbookSource = "textbook";
    private const string FormulaHeading = "## Formula";
    private const string BrainsHeading = "## Brains and models";
    private const string TryThisHeading = "## Try this";
    private const string CheckHeading = "## Check yourself";
    private static readonly string[] FixedAnchors = ["explanation", "formula", "brains", "lab"];

    private readonly Dictionary<string, Lesson> _byId;

    private LessonCatalog(IReadOnlyList<Lesson> all)
    {
        All = all;
        _byId = all.ToDictionary(l => l.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<Lesson> All { get; }

    public IReadOnlyList<Lesson> InSection(Section section) => All.Where(l => l.Section == section).ToList();

    /// <summary>Every lesson, section first and then order within the section.</summary>
    public IReadOnlyList<Lesson> InContentsOrder => All;

    /// <summary>The contents number, e.g. "2.4": section number, then position within the section from 1.</summary>
    public string NumberOf(Lesson lesson)
    {
        int position = InSection(lesson.Section).ToList().FindIndex(l => l.Id == lesson.Id);
        if (position < 0) throw new KeyNotFoundException($"Lesson '{lesson.Id}' is not in this catalog.");
        return $"{SectionInfo.Number(lesson.Section)}.{position + 1}";
    }

    public Lesson Get(string id) =>
        _byId.TryGetValue(id, out var lesson) ? lesson : throw new KeyNotFoundException($"No lesson with id '{id}'.");

    /// <summary>Reads every <c>lessons/**/*.md</c> file under the content folder.</summary>
    public static LessonCatalog Load(string contentFolder)
    {
        var folder = Path.Combine(contentFolder, "lessons");
        var lessons = new List<Lesson>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.md", SearchOption.AllDirectories))
        {
            try { lessons.Add(ParseFile(File.ReadAllText(file))); }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"{Path.GetFileName(file)}: {ex.Message}", ex);
            }
        }
        var duplicate = lessons.GroupBy(l => l.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null) throw new InvalidDataException($"Lesson id '{duplicate.Key}' is used by more than one file.");
        return new LessonCatalog(lessons.OrderBy(l => l.Section).ThenBy(l => l.Order).ToList());
    }

    private static Lesson ParseFile(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines[0].Trim() != "---") throw new InvalidDataException("the file must start with a '---' front matter line.");
        int end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
        if (end < 0) throw new InvalidDataException("the front matter has no closing '---' line.");

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines[1..end])
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int colon = line.IndexOf(':');
            if (colon <= 0) throw new InvalidDataException($"front matter line '{line}' is not 'key: value'.");
            fields[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        string Required(string key) =>
            fields.TryGetValue(key, out var v) && v.Length > 0 ? v : throw new InvalidDataException($"front matter is missing '{key}'.");

        if (!Enum.TryParse<Section>(Required("section"), ignoreCase: true, out var section) || !Enum.IsDefined(section))
            throw new InvalidDataException($"unknown section '{fields["section"]}'.");
        if (!int.TryParse(Required("order"), out var order))
            throw new InvalidDataException($"order '{fields["order"]}' is not a number.");

        var sources = Required("sources").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sourceIds = sources.Where(s => s != TextbookSource).ToList();

        string? Optional(string key) => fields.TryGetValue(key, out var v) && v.Length > 0 ? v : null;

        var part = Optional("part");
        if (part != null && !Parts.IsKnown(part))
            throw new InvalidDataException($"unknown part '{part}' (use one of {string.Join(", ", Parts.All)}).");
        var branches = ParseBranches(Optional("branches"));

        var (explanation, formula, brains, tryThis, check) = SplitBody(lines[(end + 1)..]);
        var anchors = FixedAnchors.Concat(branches.Select(b => b.Anchor)).ToHashSet(StringComparer.Ordinal);
        return new Lesson(Required("id"), section, order, Required("title"), Required("lab"),
            explanation, formula, brains, sourceIds, sources.Contains(TextbookSource))
        {
            Idea = Optional("idea"),
            Part = part,
            Branches = branches,
            TryThis = tryThis,
            Questions = QuestionParser.Parse(check, anchors),
        };
    }

    private static List<LessonBranch> ParseBranches(string? text)
    {
        var branches = new List<LessonBranch>();
        if (text == null) return branches;
        foreach (var item in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = item.LastIndexOf('=');
            if (eq <= 0 || eq == item.Length - 1)
                throw new InvalidDataException($"branch '{item}' is not 'Label=anchor'.");
            var anchor = item[(eq + 1)..].Trim();
            if (FixedAnchors.Contains(anchor) || branches.Any(b => b.Anchor == anchor))
                throw new InvalidDataException($"branch anchor '{anchor}' is already used.");
            branches.Add(new LessonBranch(item[..eq].Trim(), anchor));
        }
        return branches;
    }

    private static (MarkdownDocument, MarkdownDocument?, MarkdownDocument?, List<string>, List<string>) SplitBody(string[] body)
    {
        var parts = new Dictionary<string, List<string>> { ["main"] = new() };
        var current = "main";
        foreach (var line in body)
        {
            var t = line.Trim();
            if (t == FormulaHeading) { current = "formula"; parts[current] = new(); }
            else if (t == BrainsHeading) { current = "brains"; parts[current] = new(); }
            else if (t == TryThisHeading) { current = "try"; parts[current] = new(); }
            else if (t == CheckHeading) { current = "check"; parts[current] = new(); }
            else parts[current].Add(line);
        }
        MarkdownDocument? Doc(string key) =>
            parts.TryGetValue(key, out var lines) ? MarkdownParser.Parse(string.Join("\n", lines)) : null;
        var steps = new List<string>();
        foreach (var line in parts.GetValueOrDefault("try") ?? new())
        {
            var m = System.Text.RegularExpressions.Regex.Match(line.Trim(), @"^\d+\.\s+(.*)$");
            if (m.Success) steps.Add(m.Groups[1].Value.Trim());
        }
        return (Doc("main")!, Doc("formula"), Doc("brains"), steps, parts.GetValueOrDefault("check") ?? new());
    }
}
