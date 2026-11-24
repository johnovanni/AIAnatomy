namespace AIAnatomy.Presentation.Services;

public enum LessonMark { NotOpened, Opened, Checked }

/// <summary>Which lessons the learner has opened or checked themselves on. Kept in a small file between runs.</summary>
public interface IProgressService
{
    LessonMark MarkOf(string lessonId);

    /// <summary>Marks the lesson opened. Never lowers a lesson that is already checked.</summary>
    void MarkOpened(string lessonId);

    void MarkChecked(string lessonId);

    /// <summary>Forgets every mark.</summary>
    void Reset();

    /// <summary>Raised after any mark changes (not when nothing changed).</summary>
    event EventHandler? Changed;

    /// <summary>Why the last change could not be written to disk, or null if it was. The marks are kept for this run either way.</summary>
    string? LastSaveProblem { get; }

    /// <summary>Raised when a change could not be written to disk (see <see cref="LastSaveProblem"/>), before <see cref="Changed"/>.</summary>
    event EventHandler? SaveFailed;
}
