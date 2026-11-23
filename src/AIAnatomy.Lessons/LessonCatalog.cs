using AIAnatomy.Lessons.Markdown;

namespace AIAnatomy.Lessons;

public sealed class LessonCatalog
{
    public const string TextbookSource = "textbook";
    private const string FormulaHeading = "## Formula";
    private const string BrainsHeading = "## Brains and models";

    private readonly Dictionary<string, Lesson> _byId;

    private LessonCatalog(IReadOnlyList<Lesson> all)
    {
        All = all;
        _byId = all.ToDictionary(l => l.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<Lesson> All { get; }

    public IReadOnlyList<Lesson> InSection(Section section) => All.Where(l => l.Section == section).ToList();

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

        var (explanation, formula, brains) = SplitBody(lines[(end + 1)..]);
        return new Lesson(Required("id"), section, order, Required("title"), Required("lab"),
            explanation, formula, brains, sourceIds, sources.Contains(TextbookSource));
    }

    private static (MarkdownDocument, MarkdownDocument?, MarkdownDocument?) SplitBody(string[] body)
    {
        var parts = new Dictionary<string, List<string>> { ["main"] = new() };
        var current = "main";
        foreach (var line in body)
        {
            var t = line.Trim();
            if (t == FormulaHeading) { current = "formula"; parts[current] = new(); }
            else if (t == BrainsHeading) { current = "brains"; parts[current] = new(); }
            else parts[current].Add(line);
        }
        MarkdownDocument? Doc(string key) =>
            parts.TryGetValue(key, out var lines) ? MarkdownParser.Parse(string.Join("\n", lines)) : null;
        return (Doc("main")!, Doc("formula"), Doc("brains"));
    }
}
