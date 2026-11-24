using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation;

/// <summary>What one lesson's page shows: where it is in the contents, its big idea, the lab, the "Try this" steps, its
/// connections to other ideas, and the lessons before and after it.</summary>
public sealed class LessonPageViewModel
{
    public LessonPageViewModel(Lesson lesson, LessonCatalog catalog, LabViewModel lab, ConnectionList connections, IdeaMapData map,
        IProgressService progress, Action<string> requestAnchor)
    {
        Lesson = lesson ?? throw new ArgumentNullException(nameof(lesson));
        ArgumentNullException.ThrowIfNull(catalog);
        Lab = lab ?? throw new ArgumentNullException(nameof(lab));
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(map);

        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(requestAnchor);

        Number = catalog.NumberOf(lesson);
        Path = $"{SectionInfo.Number(lesson.Section)} {SectionInfo.Title(lesson.Section)} › {Number}";
        Title = lesson.Title;
        // A "coming later" page has nothing to teach yet, whatever its file says.
        Idea = lesson.IsPlaceholder ? null : lesson.Idea;
        Part = lesson.Part;
        TryThis = lesson.IsPlaceholder ? [] : lesson.TryThis;

        Check = !lesson.IsPlaceholder && lesson.Questions.Count > 0 ? new CheckYourselfViewModel(lesson, progress, requestAnchor) : null;
        Connections = connections.Of(lesson.Id).Select(c => Chip(c, lesson.Id, catalog, map)).ToList();

        var order = catalog.InContentsOrder;
        int at = order.ToList().FindIndex(l => l.Id == lesson.Id);
        Previous = order.Take(at).LastOrDefault(l => !l.IsPlaceholder);
        Next = order.Skip(at + 1).FirstOrDefault(l => !l.IsPlaceholder);
        PreviousLabel = Previous == null ? null : $"← {catalog.NumberOf(Previous)} {Previous.Title}";
        NextLabel = Next == null ? null : $"Next: {catalog.NumberOf(Next)} {Next.Title} →";
        PreviousName = Previous == null ? null : $"Back to the previous lesson, {catalog.NumberOf(Previous)} {Previous.Title}";
        NextName = Next == null ? null : $"On to the next lesson, {catalog.NumberOf(Next)} {Next.Title}";
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

    /// <summary>The Check yourself questions; null when the lesson has none.</summary>
    public CheckYourselfViewModel? Check { get; }
    public IReadOnlyList<ConnectionChip> Connections { get; }

    /// <summary>The built lesson before this one in the contents (null at the start); "coming later" pages are skipped.</summary>
    public Lesson? Previous { get; }

    /// <summary>The built lesson after this one in the contents (null at the end); "coming later" pages are skipped.</summary>
    public Lesson? Next { get; }

    /// <summary>The Back button's text, for example "← 2.3 Position"; null when there is no lesson before this one.</summary>
    public string? PreviousLabel { get; }

    /// <summary>The Next button's text, for example "Next: 2.5 Multi-head attention →"; null when there is no lesson after this one.</summary>
    public string? NextLabel { get; }

    /// <summary>The Back button's name for screen readers, for example "Back to the previous lesson, 2.3 Position".</summary>
    public string? PreviousName { get; }

    /// <summary>The Next button's name for screen readers, for example "On to the next lesson, 2.5 Multi-head attention".</summary>
    public string? NextName { get; }

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
