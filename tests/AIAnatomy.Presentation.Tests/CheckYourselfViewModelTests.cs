using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public class CheckYourselfViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private static readonly IReadOnlyList<Question> Two =
    [
        new Question("What does attention mix?", ["Colours", "Token information", "Sounds"], 1, "Attention blends information from other tokens.", "explanation", null),
        new Question("Which comes first?", ["Embedding", "Output"], 0, "Tokens become vectors before anything else.", "formula", null),
    ];

    private (CheckYourselfViewModel Vm, ProgressService Progress, List<string> Asked, Lesson Lesson) New()
    {
        var progress = _env.NewProgress();
        var lesson = LessonCatalog.Load(TestEnvironment.Content).All.First(l => !l.IsPlaceholder) with { Questions = Two };
        var asked = new List<string>();
        return (new CheckYourselfViewModel(lesson, progress, asked.Add), progress, asked, lesson);
    }

    [Fact]
    public void WrongAnswer_ShowsWhy_AndCanBeRetried()
    {
        var (vm, _, _, _) = New();
        var q = vm.Questions[0];
        Assert.Null(q.ChosenIndex);
        Assert.Null(q.Feedback);
        q.ChooseCommand.Execute(0);
        Assert.False(q.IsRight);
        Assert.Equal("Not quite. Attention blends information from other tokens.", q.Feedback);
        q.ChooseCommand.Execute(1);
        Assert.True(q.IsRight);
        Assert.Equal(1, q.ChosenIndex);
    }

    [Fact]
    public void ChosenAnswer_IsMarkedInWords()
    {
        var (vm, _, _, _) = New();
        var q = vm.Questions[0];
        Assert.All(q.Options, o => Assert.DoesNotContain("your answer", o.Label));
        q.ChooseCommand.Execute(2);
        // A tick only when the chosen answer is right; a cross and "not quite" when it is wrong (final review I4).
        Assert.Equal("✗ Sounds (your answer: not quite)", q.Options[2].Label);
        q.ChooseCommand.Execute(1);
        Assert.Equal("Sounds", q.Options[2].Label);
        Assert.Equal("✓ Token information (your answer: right)", q.Options[1].Label);
    }

    [Fact]
    public void RightAnswer_ShowsWhy()
    {
        var (vm, _, _, _) = New();
        vm.Questions[1].ChooseCommand.Execute(0);
        Assert.True(vm.Questions[1].IsRight);
        Assert.Equal("Right. Tokens become vectors before anything else.", vm.Questions[1].Feedback);
    }

    [Fact]
    public void AllAnswered_MarksChecked()
    {
        var (vm, progress, _, lesson) = New();
        vm.Questions[0].ChooseCommand.Execute(0);   // a wrong answer still counts as answered
        vm.Questions[1].ChooseCommand.Execute(0);
        Assert.Equal(LessonMark.Checked, progress.MarkOf(lesson.Id));
    }

    [Fact]
    public void OneAnswered_DoesNotMarkChecked()
    {
        var (vm, progress, _, lesson) = New();
        vm.Questions[0].ChooseCommand.Execute(1);
        Assert.NotEqual(LessonMark.Checked, progress.MarkOf(lesson.Id));
    }

    [Fact]
    public void Skip_MarksChecked_AndHidesQuestions()
    {
        var (vm, progress, _, lesson) = New();
        Assert.False(vm.IsSkipped);
        vm.SkipCommand.Execute(null);
        Assert.True(vm.IsSkipped);
        Assert.Equal(LessonMark.Checked, progress.MarkOf(lesson.Id));
        vm.ShowAgainCommand.Execute(null);
        Assert.False(vm.IsSkipped);
    }

    [Fact]
    public void See_RequestsTheAnchor()
    {
        var (vm, _, asked, _) = New();
        vm.Questions[0].SeeCommand.Execute(null);
        vm.Questions[1].SeeCommand.Execute(null);
        Assert.Equal(["explanation", "formula"], asked);
    }

    [Fact]
    public void NoScoreIsKept()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("beyond-coming");
        Assert.Null(vm.CurrentPage!.Check);   // no questions, no section
        vm.NavigateToLesson("attention");

        var progress = _env.NewProgress();
        var lesson = vm.CurrentPage.Lesson with { Questions = Two };
        var first = new CheckYourselfViewModel(lesson, progress, _ => { });
        first.Questions[0].ChooseCommand.Execute(1);
        var reopened = new CheckYourselfViewModel(lesson, progress, _ => { });
        Assert.Null(reopened.Questions[0].ChosenIndex);
        Assert.Null(reopened.Questions[0].Feedback);
        Assert.False(reopened.IsSkipped);
    }

    private (MainViewModel Vm, ProgressService Progress) NewMainWithQuestion()
    {
        string content = Path.Combine(_env.Root, "content-questions");
        CopyFolder(TestEnvironment.Content, content);
        string file = Path.Combine(content, "lessons", "parts", "04-attention.md");
        File.WriteAllText(file, TestEnvironment.WithoutCheckYourself(File.ReadAllText(file)));   // only the one question below
        File.AppendAllText(file,
            """


            ## Check yourself

            ? Do the weights add up to 1?
            - No
            * Yes
            why: They are a weighted average.
            see: formula

            """);
        var settings = _env.NewSettings();
        var progress = _env.NewProgress();
        var vm = new MainViewModel(LessonCatalog.Load(content), SourceList.Load(Path.Combine(content, "sources.json")),
            _env.NewActiveModels(settings), settings, progress, ConnectionList.Load(Path.Combine(content, "connections.json")),
            IdeaMapData.Load(Path.Combine(content, "idea-map.json")),
            new MsaglGraphLayout(), content);
        return (vm, progress);
    }

    [Fact]
    public void OnTheOpenPage_SeeRaisesAnAnchorRequest_AndAnswersMarkTheLessonChecked()
    {
        var (vm, progress) = NewMainWithQuestion();
        vm.NavigateToLesson("attention");
        var check = vm.CurrentPage!.Check!;
        Assert.Single(check.Questions);
        check.Questions[0].SeeCommand.Execute(null);
        Assert.Equal("formula", vm.AnchorRequest!.Anchor);
        check.Questions[0].ChooseCommand.Execute(1);
        Assert.Equal(LessonMark.Checked, progress.MarkOf("attention"));
    }

    [Fact]
    public void LeavingTheLessonAndComingBack_GivesFreshQuestions()
    {
        var (vm, _) = NewMainWithQuestion();
        vm.NavigateToLesson("attention");
        vm.CurrentPage!.Check!.Questions[0].ChooseCommand.Execute(0);
        vm.CurrentPage.Check.SkipCommand.Execute(null);
        vm.NavigateToLesson("position");
        vm.NavigateToLesson("attention");
        var check = vm.CurrentPage!.Check!;
        Assert.Null(check.Questions[0].ChosenIndex);
        Assert.False(check.IsSkipped);
    }

    [Fact]
    public void IdeaMapOrSources_AndBack_KeepsTheAnswers()
    {
        var (vm, _) = NewMainWithQuestion();
        vm.NavigateToLesson("attention");
        vm.CurrentPage!.Check!.Questions[0].ChooseCommand.Execute(0);
        vm.ShowIdeaMapCommand.Execute(null);
        vm.NavigateToLesson("attention");
        Assert.Equal(0, vm.CurrentPage!.Check!.Questions[0].ChosenIndex);
        vm.ShowSourcesCommand.Execute(null);
        vm.BackToLessonCommand.Execute(null);
        Assert.Equal(0, vm.CurrentPage!.Check!.Questions[0].ChosenIndex);
    }

    [Fact]
    public void PlaceholderLesson_HasNoCheck()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("beyond-coming");
        Assert.Null(vm.CurrentPage!.Check);
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(from)) CopyFolder(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
