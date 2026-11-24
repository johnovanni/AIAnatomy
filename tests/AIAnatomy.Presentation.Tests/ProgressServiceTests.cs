using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public sealed class ProgressServiceTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void Progress_MissingFile_IsEmpty()
    {
        var progress = _env.NewProgress();
        Assert.Equal(LessonMark.NotOpened, progress.MarkOf("anything"));
        Assert.Null(progress.LastSaveProblem);
    }

    [Fact]
    public void Progress_CorruptFile_IsEmpty_AndNextSaveWorks()
    {
        File.WriteAllText(_env.ProgressFile, "{ not json");
        var progress = _env.NewProgress();
        Assert.Equal(LessonMark.NotOpened, progress.MarkOf("a"));

        progress.MarkOpened("a");

        Assert.Null(progress.LastSaveProblem);
        Assert.Equal(LessonMark.Opened, _env.NewProgress().MarkOf("a"));
    }

    [Fact]
    public void Progress_OpenedThenChecked_RoundTrips()
    {
        var progress = _env.NewProgress();
        progress.MarkOpened("a");
        progress.MarkOpened("b");
        progress.MarkChecked("b");

        var reloaded = _env.NewProgress();
        Assert.Equal(LessonMark.Opened, reloaded.MarkOf("a"));
        Assert.Equal(LessonMark.Checked, reloaded.MarkOf("b"));
        Assert.Equal(LessonMark.NotOpened, reloaded.MarkOf("c"));
    }

    [Fact]
    public void Progress_OpenedAfterChecked_StaysChecked()
    {
        var progress = _env.NewProgress();
        progress.MarkChecked("a");
        progress.MarkOpened("a");

        Assert.Equal(LessonMark.Checked, progress.MarkOf("a"));
        Assert.Equal(LessonMark.Checked, _env.NewProgress().MarkOf("a"));
    }

    [Fact]
    public void Progress_SaveFails_KeepsMarksForTheSession()
    {
        string folder = Path.Combine(_env.Root, "blocked");
        File.WriteAllText(folder, "a file where the folder should be");
        var progress = new ProgressService(Path.Combine(folder, "progress.json"));

        progress.MarkOpened("a");

        Assert.Equal(LessonMark.Opened, progress.MarkOf("a"));
        Assert.NotNull(progress.LastSaveProblem);
    }

    [Fact]
    public void Progress_SaveFails_RaisesSaveFailed_EachTime()
    {
        // Final review M1: the failure is raised, as ISettingsService.SaveFailed is, so the window can say so.
        string folder = Path.Combine(_env.Root, "blocked");
        File.WriteAllText(folder, "a file where the folder should be");
        var progress = new ProgressService(Path.Combine(folder, "progress.json"));
        int raised = 0;
        progress.SaveFailed += (_, _) => raised++;

        progress.MarkOpened("a");
        progress.MarkChecked("a");

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Progress_SaveSucceeds_DoesNotRaiseSaveFailed()
    {
        var progress = _env.NewProgress();
        int raised = 0;
        progress.SaveFailed += (_, _) => raised++;
        progress.MarkOpened("a");
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Progress_Reset_ClearsAll()
    {
        var progress = _env.NewProgress();
        progress.MarkChecked("a");
        progress.MarkOpened("b");
        int changed = 0;
        progress.Changed += (_, _) => changed++;

        progress.Reset();

        Assert.Equal(1, changed);
        Assert.Equal(LessonMark.NotOpened, progress.MarkOf("a"));
        Assert.Equal(LessonMark.NotOpened, _env.NewProgress().MarkOf("b"));
    }
}
