using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public class LessonPageViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private static ConnectionList Connections => ConnectionList.Load(Path.Combine(TestEnvironment.Content, "connections.json"));

    /// <summary>The shipped content with two branches added to the Attention lesson (the shipped lessons have none yet).</summary>
    private MainViewModel NewMainWithBranches()
    {
        string content = Path.Combine(_env.Root, "content-branches");
        CopyFolder(TestEnvironment.Content, content);
        string file = Path.Combine(content, "lessons", "parts", "04-attention.md");
        File.WriteAllText(file, WithoutBranches(TestEnvironment.WithoutCheckYourself(File.ReadAllText(file))).Replace("sources: vaswani2017, olsson2022, textbook",
            "sources: vaswani2017, olsson2022, textbook\nbranches: The mask=mask; More heads=heads"));
        string other = Path.Combine(content, "lessons", "parts", "05-multi-head.md");
        File.WriteAllText(other, WithoutBranches(TestEnvironment.WithoutCheckYourself(File.ReadAllText(other))).Replace("sources: ", "branches: Per head=per-head" + "\nsources: ", StringComparison.Ordinal));
        var settings = _env.NewSettings();
        return new MainViewModel(LessonCatalog.Load(content), SourceList.Load(Path.Combine(content, "sources.json")),
            _env.NewActiveModels(settings), settings, _env.NewProgress(), Connections,
            IdeaMapData.Load(Path.Combine(content, "idea-map.json")), new MsaglGraphLayout(), content);
    }

    // The shipped lessons declare branches of their own; these tests use their own, so the real ones (and the questions that
    // point at them) are taken out first.
    private static string WithoutBranches(string lesson) =>
        System.Text.RegularExpressions.Regex.Replace(lesson, "(?m)^branches:.*$", "");

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(from)) CopyFolder(dir, Path.Combine(to, Path.GetFileName(dir)));
    }

    private static AIAnatomy.Presentation.Contents.ContentsLesson Row(MainViewModel vm, string id) =>
        vm.Contents.Sections.SelectMany(s => s.Lessons).Single(l => l.Lesson.Id == id);

    [Fact]
    public void Page_PathAndNumber()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("attention");
        var page = vm.CurrentPage!;
        Assert.Equal("2 The parts › 2.4", page.Path);
        Assert.Equal("2.4", page.Number);
        Assert.Equal("Attention", page.Title);
        Assert.Same(vm.CurrentLab, page.Lab);
        Assert.Equal(page.Lesson.Idea, page.Idea);
        Assert.Equal(page.Lesson.Part, page.Part);
        Assert.Equal(page.Lesson.TryThis, page.TryThis);
    }

    [Fact]
    public void BackAndNextLabels_ShowNumberAndTitle_AndAreNullAtTheEnds()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("attention");
        var page = vm.CurrentPage!;
        Assert.Equal($"← 2.3 {page.Previous!.Title}", page.PreviousLabel);
        Assert.Equal($"Next: 2.5 {page.Next!.Title} →", page.NextLabel);

        vm.NavigateToLesson("rules-vs-learning");
        Assert.Null(vm.CurrentPage!.PreviousLabel);
    }

    [Fact]
    public void BackAndNextNames_SayWhichLesson_ForScreenReaders()
    {
        // Final review M11: the buttons' names carry the lesson, not only "On to the next lesson".
        var vm = _env.NewMain();
        vm.NavigateToLesson("attention");
        var page = vm.CurrentPage!;
        Assert.Equal($"Back to the previous lesson, 2.3 {page.Previous!.Title}", page.PreviousName);
        Assert.Equal($"On to the next lesson, 2.5 {page.Next!.Title}", page.NextName);

        vm.NavigateToLesson("rules-vs-learning");
        Assert.Null(vm.CurrentPage!.PreviousName);
    }

    [Fact]
    public void Next_FollowsContentsOrder_AcrossSections()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("architecture-map");
        Assert.Equal("tokens", vm.CurrentPage!.Next!.Id);
        vm.NavigateToLesson("learning");
        Assert.Equal("train", vm.CurrentPage!.Next!.Id);
        Assert.Equal("output-sampling", vm.CurrentPage.Previous!.Id);
        vm.NavigateToLesson("tokens");
        Assert.Equal("architecture-map", vm.CurrentPage!.Previous!.Id);
    }

    [Fact]
    public void Next_SkipsPlaceholders_LastBuiltLessonHasNoNext()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("scale");
        Assert.Equal("family-tree", vm.CurrentPage!.Next!.Id);   // 4.1 is built (Stage 2A, Task 5)
        Assert.Equal("step-through", vm.CurrentPage.Previous!.Id);
        // The last built lesson has no Next: the rest are "coming later" pages.
        var lastBuilt = LessonCatalog.Load(TestEnvironment.Content).All.Last(l => !l.IsPlaceholder);
        vm.NavigateToLesson(lastBuilt.Id);
        Assert.Null(vm.CurrentPage!.Next);
        vm.NavigateToLesson("rules-vs-learning");
        Assert.Null(vm.CurrentPage!.Previous);
    }

    [Fact]
    public void PlaceholderPage_HasNoIdeaTryOrQuestions()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("beyond-coming");
        var page = vm.CurrentPage!;
        Assert.True(page.Lesson.IsPlaceholder);
        Assert.Null(page.Idea);
        Assert.Empty(page.TryThis);
        Assert.Empty(page.Lesson.Questions);
        Assert.Equal(LessonCatalog.Load(TestEnvironment.Content).All.Last(l => !l.IsPlaceholder).Id, page.Previous!.Id);   // the nearest built lesson before it
        Assert.Null(page.Next);
    }

    [Fact]
    public void Chips_ListEveryConnectionOfTheLesson_WithTheOtherEnd()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("attention");
        var chips = vm.CurrentPage!.Connections;
        var expected = Connections.Of("attention");
        Assert.Equal(expected.Count, chips.Count);
        Assert.NotEmpty(chips);
        foreach (var c in expected)
        {
            string other = c.From == "attention" ? c.To : c.From;
            var chip = Assert.Single(chips, x => x.OtherId == other);
            Assert.Equal(c.Sentence, chip.Sentence);
            Assert.False(chip.IsWholeLoop);
        }
        var multi = chips.Single(c => c.OtherId == "multi-head");
        Assert.Equal("Multi-head attention", multi.Label);
        Assert.Equal(Row(vm, "multi-head").Lesson.Part, multi.Part);
    }

    [Fact]
    public void Chip_ToTheWholeLoop_IsMarked()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("scale");
        var chip = Assert.Single(vm.CurrentPage!.Connections);
        Assert.True(chip.IsWholeLoop);
        Assert.Equal(ConnectionList.WholeLoop, chip.OtherId);
        Assert.Null(chip.Part);
    }

    [Fact]
    public void ShowingALesson_MarksItOpened()
    {
        var progress = _env.NewProgress();
        var vm = _env.NewMain(progress: progress);
        Assert.Equal(LessonMark.Opened, progress.MarkOf("rules-vs-learning"));   // the first lesson shows at start
        Assert.Equal(LessonMark.NotOpened, progress.MarkOf("attention"));
        vm.NavigateToLesson("attention");
        Assert.Equal(LessonMark.Opened, progress.MarkOf("attention"));
        Assert.Equal(LessonMark.Opened, Row(vm, "attention").Mark);
        Assert.True(Row(vm, "attention").IsSelected);
        Assert.False(Row(vm, "rules-vs-learning").IsSelected);
    }

    [Fact]
    public void ResetProgress_ClearsMarks()
    {
        var progress = _env.NewProgress();
        var vm = _env.NewMain(progress: progress);
        vm.NavigateToLesson("attention");
        vm.SettingsPage.ResetProgressCommand.Execute(null);
        Assert.Equal(LessonMark.Opened, progress.MarkOf("attention"));   // the first click only asks (final review M9)
        vm.SettingsPage.ConfirmResetProgressCommand.Execute(null);
        Assert.Equal(LessonMark.NotOpened, progress.MarkOf("attention"));
        Assert.Equal(LessonMark.NotOpened, progress.MarkOf("rules-vs-learning"));
    }

    [Fact]
    public void OpenBranch_OpensItsLessonAndRequestsTheAnchor()
    {
        var vm = NewMainWithBranches();
        var branch = Row(vm, "attention").Branches[1];
        Assert.Equal("heads", branch.Anchor);
        Assert.Null(vm.AnchorRequest);

        vm.OpenBranchCommand.Execute(branch);
        var first = vm.AnchorRequest!;
        Assert.Equal("attention", vm.CurrentPage!.Lesson.Id);
        Assert.Equal("heads", first.Anchor);

        vm.OpenBranchCommand.Execute(branch);   // the same anchor again still scrolls
        Assert.Equal("heads", vm.AnchorRequest!.Anchor);
        Assert.NotEqual(first.Serial, vm.AnchorRequest.Serial);
        Assert.Equal("attention", vm.CurrentPage.Lesson.Id);
    }

    [Fact]
    public void IdeaMap_HidesTheLesson_AndOpenLessonComesBack()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("attention");
        vm.ShowIdeaMapCommand.Execute(null);
        Assert.True(vm.IsShowingIdeaMap);
        Assert.False(vm.IsShowingLesson);

        vm.IdeaMap.Choose("tokens");
        vm.IdeaMap.OpenChosenLessonCommand.Execute(null);

        Assert.False(vm.IsShowingIdeaMap);
        Assert.True(vm.IsShowingLesson);
        Assert.Equal("tokens", vm.CurrentPage!.Lesson.Id);

        vm.ShowIdeaMapCommand.Execute(null);
        vm.ShowSourcesCommand.Execute(null);   // another page replaces the map
        Assert.False(vm.IsShowingIdeaMap);
        Assert.False(vm.IsShowingLesson);
        vm.ShowIdeaMapCommand.Execute(null);
        Assert.False(vm.IsShowingSources);
        vm.BackToLessonCommand.Execute(null);   // the Contents switch (ShowContents was the same command; final review M10)
        Assert.True(vm.IsShowingLesson);
    }

    [Fact]
    public void GoNextAndGoBack_MoveThroughTheContents()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("architecture-map");
        vm.GoNextCommand.Execute(null);
        Assert.Equal("tokens", vm.CurrentPage!.Lesson.Id);
        vm.GoBackCommand.Execute(null);
        Assert.Equal("architecture-map", vm.CurrentPage!.Lesson.Id);
        vm.NavigateToLesson("rules-vs-learning");
        Assert.False(vm.GoBackCommand.CanExecute(null));
        vm.NavigateToLesson(LessonCatalog.Load(TestEnvironment.Content).All.Last(l => !l.IsPlaceholder).Id);
        Assert.False(vm.GoNextCommand.CanExecute(null));
        Assert.True(vm.GoBackCommand.CanExecute(null));
    }

    [Fact]
    public void OpenConnection_OpensTheOtherLesson_OrTheLoopMap()
    {
        var vm = _env.NewMain();
        vm.NavigateToLesson("attention");
        vm.OpenConnectionCommand.Execute(vm.CurrentPage!.Connections.Single(c => c.OtherId == "multi-head"));
        Assert.Equal("multi-head", vm.CurrentPage!.Lesson.Id);

        vm.NavigateToLesson("scale");
        vm.IdeaMap.Choose("scale");   // something chosen before; the LOOP chip clears it
        vm.OpenConnectionCommand.Execute(vm.CurrentPage!.Connections.Single());
        Assert.True(vm.IsShowingIdeaMap);
        Assert.Equal(MapMode.Loop, vm.IdeaMap.Mode);
        Assert.Null(vm.IdeaMap.ChosenId);
    }

    [Fact]
    public void ShowSection_OpensTheSectionsFirstLesson()
    {
        var vm = _env.NewMain();
        vm.ShowSectionCommand.Execute(Section.Whole);
        Assert.Equal("train", vm.CurrentPage!.Lesson.Id);
        vm.ShowSourcesCommand.Execute(null);
        vm.ShowSectionCommand.Execute(Section.Whole);
        Assert.True(vm.IsShowingLesson);
        Assert.Equal("train", vm.CurrentPage!.Lesson.Id);
    }

    [Fact]
    public void OpenBranch_OfAnotherLesson_OpensThatLesson()
    {
        var vm = NewMainWithBranches();
        vm.NavigateToLesson("attention");
        var branch = Row(vm, "multi-head").Branches.Single();
        vm.OpenBranchCommand.Execute(branch);
        Assert.Equal("multi-head", vm.CurrentPage!.Lesson.Id);
        Assert.Equal("per-head", vm.AnchorRequest!.Anchor);
    }

    [Fact]
    public void ShowingALesson_ClearsAnyAnchorRequest()
    {
        var vm = NewMainWithBranches();
        vm.OpenBranchCommand.Execute(Row(vm, "attention").Branches[0]);
        Assert.NotNull(vm.AnchorRequest);
        vm.NavigateToLesson("tokens");
        Assert.Null(vm.AnchorRequest);
    }

    [Fact]
    public void CoveringPageToCoveringPage_NeverShowsTheLessonInBetween()
    {
        var vm = _env.NewMain();
        var seen = new List<bool>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.IsShowingLesson)) seen.Add(vm.IsShowingLesson); };
        vm.ShowSourcesCommand.Execute(null);
        vm.ShowIdeaMapCommand.Execute(null);
        vm.ShowSettingsCommand.Execute(null);
        vm.ShowSourcesCommand.Execute(null);
        Assert.DoesNotContain(true, seen);
    }

    [Fact]
    public void OpenConnection_ToAMapOnlyIdea_OpensTheMapWithItChosen()
    {
        var vm = _env.NewMain();
        vm.IdeaMap.Mode = MapMode.Radial;
        var chip = new ConnectionChip("your-text", "Your text", null, "A sentence.", IsWholeLoop: false);   // on the Loop map only
        vm.OpenConnectionCommand.Execute(chip);
        Assert.True(vm.IsShowingIdeaMap);
        Assert.Equal(MapMode.Loop, vm.IdeaMap.Mode);
        Assert.Equal("your-text", vm.IdeaMap.ChosenId);
    }

    [Fact]
    public void OpenLesson_WhileTheMapShows_OpensIt_AndTheOpenOneJustComesBack()
    {
        var vm = _env.NewMain();
        vm.ShowIdeaMapCommand.Execute(null);
        var lab = vm.CurrentLab;
        vm.NavigateToLesson("tokens");
        Assert.False(vm.IsShowingIdeaMap);
        Assert.Equal("tokens", vm.CurrentPage!.Lesson.Id);

        lab = vm.CurrentLab;
        vm.ShowIdeaMapCommand.Execute(null);
        vm.NavigateToLesson("tokens");
        Assert.True(vm.IsShowingLesson);
        Assert.Same(lab, vm.CurrentLab);
    }

    [Fact]
    public async Task BackgroundTraining_StatusStaysWhileIdeaMapShows()
    {
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        lab.Settings.Width = 16; lab.Settings.LayersText = "1"; lab.Settings.HeadsText = "2"; lab.Settings.ContextText = "16";
        lab.Settings.BatchText = "2"; lab.Settings.StepsText = "100000";
        var run = lab.StartCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone > 0);
        Assert.Null(vm.BackgroundStatus);

        vm.ShowIdeaMapCommand.Execute(null);
        await WaitUntil(() => vm.BackgroundStatus?.StartsWith("Training in the background: step ") == true);
        Assert.True(lab.IsTraining);

        await vm.NavigateToLesson("train");
        Assert.Same(lab, vm.CurrentLab);
        Assert.Null(vm.BackgroundStatus);

        vm.ResetLabCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromMinutes(1));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }
}
