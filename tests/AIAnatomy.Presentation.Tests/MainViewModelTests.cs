using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private sealed class FakeLab : LabViewModel
    {
        public int Resets, ModelChanges;
        public override string LabId => "AttentionLab";
        public override void Reset() => Resets++;
        public override void OnActiveModelChanged() => ModelChanges++;
    }

    [Fact]
    public void Main_StartsOnFirstLessonOfMap()
    {
        var vm = _env.NewMain();
        Assert.Equal("rules-vs-learning", vm.SelectedLesson!.Id);
        Assert.Equal("rules-vs-learning", vm.CurrentPage!.Lesson.Id);
        Assert.NotNull(vm.CurrentPage.Lesson.Explanation);
        Assert.Equal(new[] { Section.Map, Section.Parts, Section.Whole, Section.Family, Section.Beyond }, vm.Contents.Sections.Select(s => s.Section));
    }

    [Fact]
    public void Main_ShowingASection_ShowsItsFirstLesson()
    {
        var vm = _env.NewMain();
        vm.ShowSectionCommand.Execute(Section.Parts);
        Assert.Equal(9, vm.Contents.Sections.Single(s => s.Section == Section.Parts).Lessons.Count);
        Assert.Equal("tokens", vm.SelectedLesson!.Id);
    }

    [Fact]
    public void Main_SelectingLesson_CreatesItsLab_AndExposesItsText()
    {
        var vm = _env.NewMain();
        vm.Labs.Register("AttentionLab", () => new FakeLab());
        vm.NavigateToLesson("attention");

        Assert.IsType<FakeLab>(vm.CurrentLab);
        // The page shows the lesson's own texts (CurrentPage.Lesson); MainViewModel keeps no copies (final review M10).
        Assert.Same(vm.SelectedLesson, vm.CurrentPage!.Lesson);
        Assert.NotNull(vm.CurrentPage.Lesson.Explanation);
        Assert.NotNull(vm.CurrentPage.Lesson.Formula);
    }

    [Fact]
    public void Main_LabNotRegisteredYet_ShowsComingLater()
    {
        // A copy of the content with one extra lesson whose lab will never be registered, so this test does not depend on which labs exist.
        string content = Path.Combine(_env.Root, "content-copy");
        CopyFolder(TestEnvironment.Content, content);
        File.WriteAllText(Path.Combine(content, "lessons", "parts", "99-never.md"),
            "---\nid: never-built\nsection: parts\norder: 99\ntitle: Never built\nlab: NeverRegisteredLab\nsources: textbook\n---\nA lesson for a test.\n");
        var settings = _env.NewSettings();
        var vm = new MainViewModel(LessonCatalog.Load(content), SourceList.Load(Path.Combine(content, "sources.json")), _env.NewActiveModels(settings), settings,
            _env.NewProgress(), ConnectionList.Load(Path.Combine(content, "connections.json")),
            WithBox(IdeaMapData.Load(Path.Combine(content, "idea-map.json")), "never-built"), new MsaglGraphLayout(), content);
        vm.NavigateToLesson("never-built");

        var lab = Assert.IsType<ComingLaterLabViewModel>(vm.CurrentLab);
        Assert.Equal("ComingLaterLab", lab.LabId);
        Assert.Equal("This lab is being built.", lab.Message);
    }

    [Fact]
    public void UseStackedLayout_SwitchesAtEighteenPoints()
    {
        var vm = _env.NewMain();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.False(vm.UseStackedLayout);          // the default is 13 pt
        vm.SettingsPage.BodyFontSize = 17;
        Assert.False(vm.UseStackedLayout);
        vm.SettingsPage.BodyFontSize = 18;
        Assert.True(vm.UseStackedLayout);
        Assert.Contains(nameof(MainViewModel.UseStackedLayout), raised);
        vm.SettingsPage.BodyFontSize = 24;
        Assert.True(vm.UseStackedLayout);
        vm.SettingsPage.BodyFontSize = 10;
        Assert.False(vm.UseStackedLayout);
        Assert.Equal(18, MainViewModel.StackedLayoutFromSize);
    }

    [Fact]
    public void StackedLayout_PanePositionsComeOnlyFromTheirStyles()
    {
        // A local Grid.Row/Column/Span on a pane would beat the style trigger that moves it (the explanation once drew over the lab).
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "AIAnatomy.slnx"))) dir = Path.GetDirectoryName(dir);
        string xaml = File.ReadAllText(Path.Combine(dir!, "src", "AIAnatomy.App", "Views", "LessonPageView.xaml"));
        foreach (string name in new[] { "Lab pane", "Explanation pane" })
        {
            int at = xaml.IndexOf($"AutomationProperties.Name=\"{name}\"", StringComparison.Ordinal);
            Assert.True(at > 0, name);
            string tag = xaml[xaml.LastIndexOf("<Border", at, StringComparison.Ordinal)..at];
            Assert.DoesNotContain("Grid.", tag);
            string style = xaml[at..xaml.IndexOf("</Border.Style>", at, StringComparison.Ordinal)];
            foreach (string property in new[] { "Grid.Row", "Grid.Column", "Grid.ColumnSpan" })
                Assert.Contains($"Property=\"{property}\"", style);
        }
        // Two rows that size to their content: the explanation's row is empty beside the lab and holds it under the lab.
        string grid = xaml[xaml.LastIndexOf("<Grid ", xaml.IndexOf("<Grid.RowDefinitions>", StringComparison.Ordinal), StringComparison.Ordinal)..xaml.IndexOf("</Grid.RowDefinitions>", StringComparison.Ordinal)];
        Assert.Equal(2, grid.Split("<RowDefinition Height=\"Auto\" />").Length - 1);
    }

    // The idea map needs a box for every lesson, so the extra lesson gets one.
    private static IdeaMapData WithBox(IdeaMapData map, string lessonId) =>
        IdeaMapData.Create(map.Nodes.Append(new IdeaNode(lessonId, "Never built", "A lesson for a test.", IdeaMapData.Flow)), map.FlowEdges, map.GroupMembers);

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(from)) CopyFolder(dir, Path.Combine(to, Path.GetFileName(dir)));
    }

    [Fact]
    public void Main_NavigateToLesson_ShowsThatLesson()
    {
        var vm = _env.NewMain();
        vm.NavigateToLessonCommand.Execute("scale");
        Assert.Equal("scale", vm.SelectedLesson!.Id);
        Assert.Equal("scale", vm.CurrentPage!.Lesson.Id);
        Assert.Equal("3.3", vm.CurrentPage.Number);
    }

    [Fact]
    public void Main_NavigateToLesson_CreatesLabOnce()
    {
        var vm = _env.NewMain();
        int created = 0;
        vm.Labs.Register("AttentionLab", () => { created++; return new FakeLab(); });
        vm.NavigateToLesson("attention");
        Assert.Equal(1, created);
    }

    [Fact]
    public void Main_ModelChange_NotifiesCurrentLab()
    {
        var models = _env.NewActiveModels();
        var vm = _env.NewMain(models);
        vm.Labs.Register("AttentionLab", () => new FakeLab());
        vm.NavigateToLesson("attention");
        var lab = (FakeLab)vm.CurrentLab!;

        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));

        Assert.Equal(1, lab.ModelChanges);
    }

    [Fact]
    public void Main_SelectingModelEntry_ActivatesIt()
    {
        var models = _env.NewActiveModels();
        var vm = _env.NewMain(models);
        Assert.Equal("Fables model", vm.SelectedModelEntry!.Name);

        vm.SelectedModelEntry = vm.ModelEntries.Single(e => e.Name == "Induction demo");

        Assert.Equal("Induction demo", models.Current.Info.Name);
    }

    [Fact]
    public void Main_SelectingUnreadableModel_IsRefused_AndShowsProblem()
    {
        Directory.CreateDirectory(_env.UserModels);
        File.WriteAllText(Path.Combine(_env.UserModels, "broken.aimodel"), "not a model");
        var models = _env.NewActiveModels();
        var vm = _env.NewMain(models);
        var broken = vm.ModelEntries.Single(e => !e.IsReadable);

        vm.SelectedModelEntry = broken;

        Assert.Equal("Fables model", vm.SelectedModelEntry!.Name);
        Assert.Equal("Fables model", models.Current.Info.Name);
        Assert.Contains(broken.Problem!, vm.StatusMessage);
    }

    [Fact]
    public void Main_ShowSources_ThenSelectingLesson_HidesThem()
    {
        var vm = _env.NewMain();
        vm.ShowSourcesCommand.Execute(null);
        Assert.True(vm.IsShowingSources);
        Assert.NotEmpty(vm.Sources.All);

        vm.NavigateToLesson("architecture-map");

        Assert.False(vm.IsShowingSources);
    }

    [Fact]
    public void Main_ResetLab_ResetsCurrentLab()
    {
        var vm = _env.NewMain();
        vm.Labs.Register("AttentionLab", () => new FakeLab());
        vm.NavigateToLesson("attention");
        vm.ResetLabCommand.Execute(null);
        Assert.Equal(1, ((FakeLab)vm.CurrentLab!).Resets);
    }

    [Fact]
    public void Main_SettingsSaveFails_ShowsPlainMessage()
    {
        Directory.CreateDirectory(_env.SettingsFile);   // a folder where the file should be
        var settings = _env.NewSettings();
        var vm = _env.NewMain(settings: settings);

        settings.Save(settings.Current with { Theme = "Dark" });

        Assert.StartsWith("Your settings couldn't be saved: ", vm.StatusMessage);
        Assert.EndsWith(". They'll apply until you close the app.", vm.StatusMessage);
        Assert.Contains(settings.LastSaveProblem!, vm.StatusMessage);
    }

    [Fact]
    public void Main_ResetLab_OnlyWhileALessonShows()
    {
        // Final review M8: the top-bar Reset would otherwise reset a lab hidden under the map, Sources or Settings.
        var vm = _env.NewMain();
        vm.Labs.Register("AttentionLab", () => new FakeLab());
        vm.NavigateToLesson("attention");
        Assert.True(vm.ResetLabCommand.CanExecute(null));
        int changes = 0;
        vm.ResetLabCommand.CanExecuteChanged += (_, _) => changes++;

        foreach (var cover in new[] { vm.ShowIdeaMapCommand, vm.ShowSourcesCommand, vm.ShowSettingsCommand })
        {
            cover.Execute(null);
            Assert.False(vm.ResetLabCommand.CanExecute(null));
            vm.ResetLabCommand.Execute(null);   // a stray call does nothing either
            vm.BackToLessonCommand.Execute(null);
            Assert.True(vm.ResetLabCommand.CanExecute(null));
        }
        Assert.Equal(0, ((FakeLab)vm.CurrentLab!).Resets);
        Assert.True(changes >= 6);
    }

    [Fact]
    public void Main_ProgressSaveFails_ShowsAPlainMessageOnce()
    {
        // Final review M1: the marks are kept for this run; the learner is told once, not on every lesson.
        string folder = Path.Combine(_env.Root, "blocked");
        File.WriteAllText(folder, "a file where the folder should be");
        var progress = new AIAnatomy.Presentation.Services.ProgressService(Path.Combine(folder, "progress.json"));
        var vm = _env.NewMain(progress: progress);   // opening the first lesson already tries to save

        Assert.StartsWith("Your progress marks couldn't be saved: ", vm.StatusMessage);
        Assert.EndsWith(". They'll be kept until you close the app.", vm.StatusMessage);
        Assert.Contains(progress.LastSaveProblem!, vm.StatusMessage);

        vm.StatusMessage = null;
        vm.NavigateToLesson("attention");   // fails again, but has been said
        Assert.Null(vm.StatusMessage);
    }

    [Fact]
    public void Main_RemembersTheLastLesson_AndOpensItAtStart()
    {
        // Final review M13: the "you are here" place is kept in the settings.
        var vm = _env.NewMain();
        vm.NavigateToLesson("attention");
        Assert.Equal("attention", _env.NewSettings().Current.LastLessonId);

        var again = _env.NewMain();
        Assert.Equal("attention", again.SelectedLesson!.Id);
        Assert.Equal("attention", again.CurrentPage!.Lesson.Id);
    }

    [Theory]
    [InlineData("no-such-lesson")]
    [InlineData(null)]
    [InlineData("PLACEHOLDER")]
    public void Main_LastLessonThatCannotBeShown_StartsAtTheFirstLesson(string? saved)
    {
        if (saved == "PLACEHOLDER") saved = LessonCatalog.Load(TestEnvironment.Content).All.First(l => l.IsPlaceholder).Id;
        var settings = _env.NewSettings();
        settings.Save(settings.Current with { LastLessonId = saved });

        var vm = _env.NewMain(settings: _env.NewSettings());

        Assert.Equal("rules-vs-learning", vm.SelectedLesson!.Id);
    }

    [Fact]
    public void Main_LeavingSettings_ClearsAnOpenResetQuestion()
    {
        // Re-review N2: the question does not wait for the learner's return.
        var vm = _env.NewMain();
        vm.ShowSettingsCommand.Execute(null);
        vm.SettingsPage.ResetProgressCommand.Execute(null);
        Assert.True(vm.SettingsPage.IsConfirmingReset);

        vm.BackToLessonCommand.Execute(null);

        Assert.False(vm.SettingsPage.IsConfirmingReset);
    }

    [Fact]
    public void Main_LinkThatCannotBeOpened_GivesTheAddressInTheStatusLine()
    {
        var vm = _env.NewMain();
        vm.ShowLinkNotOpened("https://arxiv.org/abs/1706.03762");
        Assert.Equal("The web page could not be opened from here. You can type its address into your web browser: https://arxiv.org/abs/1706.03762",
            vm.StatusMessage);
    }

    /// <summary>Ruling X: the status line is live text, which is read only when its words change, so the same message
    /// again is cleared first (a change) and is read again.</summary>
    [Fact]
    public void Main_SameStatusMessageTwice_IsClearedInBetween_SoItIsReadAgain()
    {
        var vm = _env.NewMain();
        var seen = new List<string?>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.StatusMessage)) seen.Add(vm.StatusMessage); };
        vm.ShowLinkNotOpened("https://arxiv.org/abs/1706.03762");
        vm.ShowLinkNotOpened("https://arxiv.org/abs/1706.03762");
        string said = "The web page could not be opened from here. You can type its address into your web browser: https://arxiv.org/abs/1706.03762";
        Assert.Equal(new[] { said, null, said }, seen.SkipWhile(s => s == null));
        Assert.Equal(said, vm.StatusMessage);
    }

    [Fact]
    public void LabFactory_UnknownId_Throws()
    {
        var vm = _env.NewMain();
        Assert.False(vm.Labs.IsRegistered("NopeLab"));
        Assert.Throws<ArgumentException>(() => vm.Labs.Create("NopeLab"));
        Assert.True(vm.Labs.IsRegistered("ComingLaterLab"));
    }

    [Fact]
    public void Main_BackToLesson_LeavesSources()
    {
        var vm = _env.NewMain();
        vm.ShowSourcesCommand.Execute(null);
        Assert.False(vm.IsShowingLesson);

        vm.BackToLessonCommand.Execute(null);

        Assert.False(vm.IsShowingSources);
        Assert.True(vm.IsShowingLesson);
    }

    [Fact]
    public void Main_OpeningTheCurrentLesson_LeavesSources_WithoutRecreatingTheLab()
    {
        var vm = _env.NewMain();
        var lab = vm.CurrentLab;
        vm.ShowSourcesCommand.Execute(null);

        vm.NavigateToLesson(vm.SelectedLesson!.Id);

        Assert.False(vm.IsShowingSources);
        Assert.Same(lab, vm.CurrentLab);
    }

    [Fact]
    public void Main_OpeningAnotherLesson_ShowsIt()
    {
        var vm = _env.NewMain();
        vm.ShowSettingsCommand.Execute(null);

        vm.NavigateToLesson("architecture-map");

        Assert.Equal("architecture-map", vm.SelectedLesson!.Id);
        Assert.False(vm.IsShowingSettings);
        Assert.True(vm.IsShowingLesson);
    }

    [Fact]
    public void Main_ShowSettings_AndShowSources_AreExclusive()
    {
        var vm = _env.NewMain();
        vm.ShowSettingsCommand.Execute(null);
        Assert.True(vm.IsShowingSettings);

        vm.ShowSourcesCommand.Execute(null);

        Assert.True(vm.IsShowingSources);
        Assert.False(vm.IsShowingSettings);
        Assert.False(vm.IsShowingLesson);
    }

    [Fact]
    public void Main_ShowSection_CurrentSection_LeavesSources()
    {
        var vm = _env.NewMain();
        vm.ShowSourcesCommand.Execute(null);

        vm.ShowSectionCommand.Execute(Section.Map);

        Assert.True(vm.IsShowingLesson);
        Assert.Equal("rules-vs-learning", vm.SelectedLesson!.Id);
    }

    [Fact]
    public void Main_ShowSection_OtherSection_ShowsItsFirstLesson()
    {
        var vm = _env.NewMain();
        vm.ShowSectionCommand.Execute(Section.Whole);
        Assert.Equal("train", vm.SelectedLesson!.Id);
    }

    [Fact]
    public void Main_ModelChangedElsewhere_ResyncsPicker()
    {
        var models = _env.NewActiveModels();
        var vm = _env.NewMain(models);

        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));

        Assert.Equal("Induction demo", vm.SelectedModelEntry!.Name);
    }

    [Fact]
    public void Main_ModelSavedAndActivatedElsewhere_AppearsInPicker()
    {
        var models = _env.NewActiveModels();
        var vm = _env.NewMain(models);
        int before = vm.ModelEntries.Count;

        models.SetActive(models.Library.Save(models.Current, "My copy"));   // as the Train lab's "Use in the labs" will

        Assert.Equal(before + 1, vm.ModelEntries.Count);
        Assert.False(vm.SelectedModelEntry!.IsShipped);
    }

    [Fact]
    public async Task Main_EveryRegisteredLab_ResetsAndDeactivatesWithoutThrowing()
    {
        // What --selftest all-labs does, without the window.
        var vm = _env.NewMain();
        foreach (var lesson in LessonCatalog.Load(TestEnvironment.Content).All.Where(l => vm.Labs.IsRegistered(l.LabId)))
        {
            await vm.NavigateToLesson(lesson.Id);
            var lab = vm.CurrentLab!;
            Assert.Equal(lesson.LabId, lab.LabId);
            lab.Reset();
            await lab.WorkCompleted.WaitAsync(TimeSpan.FromMinutes(2));
            lab.Deactivate();
        }
    }
}
