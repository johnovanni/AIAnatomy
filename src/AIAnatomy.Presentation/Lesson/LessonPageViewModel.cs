using AIAnatomy.Lessons;

namespace AIAnatomy.Presentation;

/// <summary>What one lesson's page shows: where it is in the contents, its big idea, the lab, the "Try this" steps, its
/// connections to other ideas, and the lessons before and after it.</summary>
public sealed class LessonPageViewModel
{
    public LessonPageViewModel(Lesson lesson, LessonCatalog catalog, LabViewModel lab, ConnectionList connections, IdeaMapData map)
    {
        Lesson = lesson ?? throw new ArgumentNullException(nameof(lesson));
        ArgumentNullException.ThrowIfNull(catalog);
        Lab = lab ?? throw new ArgumentNullException(nameof(lab));
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(map);

        Number = catalog.NumberOf(lesson);
        Path = $"{SectionInfo.Number(lesson.Section)} {SectionInfo.Title(lesson.Section)} › {Number}";
        Title = lesson.Title;
        // A "coming later" page has nothing to teach yet, whatever its file says.
        Idea = lesson.IsPlaceholder ? null : lesson.Idea;
        Part = lesson.Part;
        TryThis = lesson.IsPlaceholder ? [] : lesson.TryThis;

        Connections = connections.Of(lesson.Id).Select(c => Chip(c, lesson.Id, catalog, map)).ToList();

        var order = catalog.InContentsOrder;
        int at = order.ToList().FindIndex(l => l.Id == lesson.Id);
        Previous = order.Take(at).LastOrDefault(l => !l.IsPlaceholder);
        Next = order.Skip(at + 1).FirstOrDefault(l => !l.IsPlaceholder);
    }

    public Lesson Lesson { get; }

    /// <summary>For example "2 The parts › 2.4".</summary>
    public string Path { get; }

    /// <summary>For example "2.4".</summary>
    public string Number { get; }

    public string Title { get; }
    public string? Idea { get; }
    public string? Part { get; }
    public IReadOnlyList<string> TryThis { get; }
    public LabViewModel Lab { get; }
    public IReadOnlyList<ConnectionChip> Connections { get; }

    /// <summary>The built lesson before this one in the contents (null at the start); "coming later" pages are skipped.</summary>
    public Lesson? Previous { get; }

    /// <summary>The built lesson after this one in the contents (null at the end); "coming later" pages are skipped.</summary>
    public Lesson? Next { get; }

    private static ConnectionChip Chip(Connection c, string ownId, LessonCatalog catalog, IdeaMapData map)
    {
        string otherId = c.From == ownId ? c.To : c.From;
        if (otherId == ConnectionList.WholeLoop) return new ConnectionChip(otherId, "The whole loop", null, c.Sentence, IsWholeLoop: true);
        var lesson = catalog.All.FirstOrDefault(l => l.Id == otherId);
        if (lesson != null) return new ConnectionChip(otherId, lesson.Title, lesson.Part, c.Sentence, IsWholeLoop: false);
        string label = map.Nodes.FirstOrDefault(n => n.Id == otherId)?.Label ?? otherId;
        return new ConnectionChip(otherId, label, null, c.Sentence, IsWholeLoop: false);
    }
}
