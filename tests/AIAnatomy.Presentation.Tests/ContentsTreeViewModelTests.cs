using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Contents;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public sealed class ContentsTreeViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    private readonly LessonCatalog _catalog;

    public ContentsTreeViewModelTests() => _catalog = LessonCatalog.Load(TestEnvironment.Content);
    public void Dispose() => _env.Dispose();

    [Fact]
    public void Tree_HasFiveNumberedSections_FamilyAndBeyondLater()
    {
        var tree = new ContentsTreeViewModel(_catalog, _env.NewProgress());

        Assert.Equal(
            ["1 The big picture", "2 The parts", "3 The whole model", "4 The family of AI", "5 Beyond the model"],
            tree.Sections.Select(s => s.Heading).ToArray());
        Assert.Equal([false, false, false, true, true], tree.Sections.Select(s => s.IsLater).ToArray());
        Assert.All(tree.Sections.Where(s => s.IsLater).SelectMany(s => s.Lessons), l => Assert.True(l.IsLater));
        Assert.All(tree.Sections.Where(s => !s.IsLater).SelectMany(s => s.Lessons), l => Assert.False(l.IsLater));
    }

    [Fact]
    public void Tree_LabelsUseNumbers()
    {
        var tree = new ContentsTreeViewModel(_catalog, _env.NewProgress());
        var parts = tree.Sections.Single(s => s.Section == Section.Parts);

        var fourth = parts.Lessons[3];
        Assert.Equal("2.4", fourth.Number);
        Assert.Equal($"2.4 {fourth.Lesson.Title}", fourth.Label);
        Assert.Equal(fourth.Lesson.Branches, fourth.Branches);
    }

    [Fact]
    public void Tree_MarkFollowsProgress()
    {
        var progress = _env.NewProgress();
        var tree = new ContentsTreeViewModel(_catalog, progress);
        var first = tree.Sections[0].Lessons[0];
        Assert.Equal(LessonMark.NotOpened, first.Mark);

        progress.MarkOpened(first.Lesson.Id);
        Assert.Equal(LessonMark.Opened, first.Mark);

        progress.MarkChecked(first.Lesson.Id);
        Assert.Equal(LessonMark.Checked, first.Mark);

        progress.Reset();
        Assert.Equal(LessonMark.NotOpened, first.Mark);
    }

    [Fact]
    public void Tree_Select_MarksOnlyThatLessonSelected()
    {
        var tree = new ContentsTreeViewModel(_catalog, _env.NewProgress());
        var all = tree.Sections.SelectMany(s => s.Lessons).ToList();

        tree.Select(all[2].Lesson.Id);
        Assert.Equal([all[2]], all.Where(l => l.IsSelected).ToArray());

        tree.Select(all[0].Lesson.Id);
        Assert.Equal([all[0]], all.Where(l => l.IsSelected).ToArray());
    }

    [Fact]
    public void Row_AutomationName_IncludesTheMark_ExceptOnLaterRows()
    {
        var progress = _env.NewProgress();
        var tree = new ContentsTreeViewModel(_catalog, progress);
        var all = tree.Sections.SelectMany(s => s.Lessons).ToList();
        var row = all.First(l => !l.IsLater);
        Assert.Equal(row.Label, row.AutomationName);

        progress.MarkOpened(row.Lesson.Id);
        Assert.Equal($"{row.Label}, opened", row.AutomationName);

        Assert.All(all.Where(l => l.IsLater), l => Assert.Equal(l.Label, l.AutomationName));
    }
}
