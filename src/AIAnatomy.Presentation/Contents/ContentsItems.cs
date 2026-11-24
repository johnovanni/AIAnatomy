using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Contents;

/// <summary>One numbered section of the contents. <see cref="IsLater"/> is true for sections that are not built yet.</summary>
public sealed record ContentsSection(Section Section, string Heading, bool IsLater, IReadOnlyList<ContentsLesson> Lessons);

public sealed partial class ContentsLesson : ObservableObject
{
    public ContentsLesson(Lesson lesson, string number, LessonMark mark)
    {
        Lesson = lesson;
        Number = number;
        Label = $"{number} {lesson.Title}";
        Branches = lesson.Branches;
        IsLater = lesson.IsPlaceholder;
        _mark = mark;
    }

    public Lesson Lesson { get; }

    /// <summary>For example "2.4".</summary>
    public string Number { get; }

    /// <summary>For example "2.4 Attention".</summary>
    public string Label { get; }

    public IReadOnlyList<LessonBranch> Branches { get; }
    public bool IsLater { get; }

    [ObservableProperty] private LessonMark _mark;
    [ObservableProperty] private bool _isSelected;
}
