using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Contents;

/// <summary>The numbered contents: five sections of numbered lessons, with each lesson's saved mark.</summary>
public sealed class ContentsTreeViewModel
{
    private readonly IProgressService _progress;
    private readonly List<ContentsLesson> _lessons = [];

    public ContentsTreeViewModel(LessonCatalog catalog, IProgressService progress)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));

        var sections = new List<ContentsSection>();
        foreach (var section in Enum.GetValues<Section>())
        {
            var lessons = catalog.InSection(section)
                .Select(l => new ContentsLesson(l, catalog.NumberOf(l), progress.MarkOf(l.Id)))
                .ToList();
            _lessons.AddRange(lessons);
            bool isLater = lessons.Count > 0 && lessons.All(l => l.IsLater);
            sections.Add(new ContentsSection(section, $"{SectionInfo.Number(section)} {SectionInfo.Title(section)}", isLater, lessons));
        }
        Sections = sections;
        progress.Changed += (_, _) => RefreshMarks();
    }

    public IReadOnlyList<ContentsSection> Sections { get; }

    /// <summary>Highlights the lesson with this id and clears the highlight from the others.</summary>
    public void Select(string lessonId)
    {
        foreach (var lesson in _lessons) lesson.IsSelected = lesson.Lesson.Id == lessonId;
    }

    private void RefreshMarks()
    {
        foreach (var lesson in _lessons) lesson.Mark = _progress.MarkOf(lesson.Lesson.Id);
    }
}
