using AIAnatomy.Lessons.Markdown;

namespace AIAnatomy.Lessons;

public enum Section { Map, Parts, Whole, Family, Beyond }

/// <summary>
/// One lesson. <see cref="SourceIds"/> lists ids from sources.json; the reserved source "textbook"
/// is not in that list and is reported by <see cref="IsTextbookMaterial"/> instead.
/// </summary>
public sealed record Lesson(
    string Id, Section Section, int Order, string Title, string LabId,
    MarkdownDocument Explanation, MarkdownDocument? Formula, MarkdownDocument? BrainsAndModels,
    IReadOnlyList<string> SourceIds, bool IsTextbookMaterial)
{
    /// <summary>The one-sentence big idea shown with the lesson.</summary>
    public string? Idea { get; init; }

    /// <summary>The part of the transformer this lesson belongs to (a <see cref="Parts"/> key).</summary>
    public string? Part { get; init; }

    public IReadOnlyList<LessonBranch> Branches { get; init; } = [];
    public IReadOnlyList<string> TryThis { get; init; } = [];
    public IReadOnlyList<Question> Questions { get; init; } = [];

    public bool IsPlaceholder => LabId == "ComingLaterLab";
}
