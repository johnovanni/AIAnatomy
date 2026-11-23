using AIAnatomy.Lessons;
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
        Assert.Equal(Section.Map, vm.SelectedSection);
        Assert.Equal("rules-vs-learning", vm.SelectedLesson!.Id);
        Assert.NotNull(vm.Explanation);
        Assert.Equal(new[] { Section.Map, Section.Parts, Section.Whole, Section.Family, Section.Beyond }, vm.Sections);
    }

    [Fact]
    public void Main_SelectingSection_ShowsItsLessons()
    {
        var vm = _env.NewMain();
        vm.SelectedSection = Section.Parts;
        Assert.Equal(9, vm.Lessons.Count);
        Assert.All(vm.Lessons, l => Assert.Equal(Section.Parts, l.Section));
        Assert.Equal("tokens", vm.SelectedLesson!.Id);
    }

    [Fact]
    public void Main_SelectingLesson_CreatesItsLab_AndExposesItsText()
    {
        var vm = _env.NewMain();
        vm.Labs.Register("AttentionLab", () => new FakeLab());
        vm.SelectedSection = Section.Parts;
        vm.SelectedLesson = vm.Lessons.Single(l => l.Id == "attention");

        Assert.IsType<FakeLab>(vm.CurrentLab);
        Assert.Same(vm.SelectedLesson.Explanation, vm.Explanation);
        Assert.Same(vm.SelectedLesson.Formula, vm.Formula);
        Assert.Same(vm.SelectedLesson.BrainsAndModels, vm.BrainsAndModels);
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
        var vm = new MainViewModel(LessonCatalog.Load(content), SourceList.Load(Path.Combine(content, "sources.json")), _env.NewActiveModels(settings), settings, content);
        vm.SelectedSection = Section.Parts;
        vm.SelectedLesson = vm.Lessons.Single(l => l.Id == "never-built");

        var lab = Assert.IsType<ComingLaterLabViewModel>(vm.CurrentLab);
        Assert.Equal("ComingLaterLab", lab.LabId);
        Assert.Equal("This lab is being built.", lab.Message);
    }

    [Fact]
    public void UseStackedLayout_SwitchesAtTwentySixPoints()
    {
        var vm = _env.NewMain();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.False(vm.UseStackedLayout);          // the default is 20 pt
        vm.SettingsPage.BodyFontSize = 25;
        Assert.False(vm.UseStackedLayout);
        vm.SettingsPage.BodyFontSize = 26;
        Assert.True(vm.UseStackedLayout);
        Assert.Contains(nameof(MainViewModel.UseStackedLayout), raised);
        vm.SettingsPage.BodyFontSize = 32;
        Assert.True(vm.UseStackedLayout);
        vm.SettingsPage.BodyFontSize = 16;
        Assert.False(vm.UseStackedLayout);
        Assert.Equal(26, MainViewModel.StackedLayoutFromSize);
    }

    [Fact]
    public void StackedLayout_PanePositionsComeOnlyFromTheirStyles()
    {
        // A local Grid.Row/Column/Span on a pane would beat the style trigger that moves it (the explanation once drew over the lab).
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "AIAnatomy.slnx"))) dir = Path.GetDirectoryName(dir);
        string xaml = File.ReadAllText(Path.Combine(dir!, "src", "AIAnatomy.App", "MainWindow.xaml"));
        foreach (string name in new[] { "Lab pane", "Explanation pane" })
        {
            int at = xaml.IndexOf($"AutomationProperties.Name=\"{name}\"", StringComparison.Ordinal);
            Assert.True(at > 0, name);
            string tag = xaml[xaml.LastIndexOf("<Border", at, StringComparison.Ordinal)..at];
            Assert.DoesNotContain("Grid.", tag);
            string style = xaml[at..xaml.IndexOf("</Border.Style>", at, StringComparison.Ordinal)];
            foreach (string property in new[] { "Grid.Row", "Grid.RowSpan", "Grid.Column", "Grid.ColumnSpan" })
                Assert.Contains($"Property=\"{property}\"", style);
        }
        Assert.Contains("<RowDefinition Height=\"3*\" />", xaml);
        Assert.Contains("<RowDefinition Height=\"2*\" />", xaml);
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(from)) CopyFolder(dir, Path.Combine(to, Path.GetFileName(dir)));
    }

    [Fact]
    public void Main_NavigateToLesson_SwitchesSection()
    {
        var vm = _env.NewMain();
        vm.NavigateToLessonCommand.Execute("scale");
        Assert.Equal(Section.Whole, vm.SelectedSection);
        Assert.Equal("scale", vm.SelectedLesson!.Id);
        Assert.Equal(3, vm.Lessons.Count);
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

        vm.SelectedLesson = vm.Lessons[1];

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

        vm.OpenLessonCommand.Execute(vm.SelectedLesson);

        Assert.False(vm.IsShowingSources);
        Assert.Same(lab, vm.CurrentLab);
    }

    [Fact]
    public void Main_OpeningAnotherLesson_ShowsIt()
    {
        var vm = _env.NewMain();
        vm.ShowSettingsCommand.Execute(null);

        vm.OpenLessonCommand.Execute(vm.Lessons[1]);

        Assert.Same(vm.Lessons[1], vm.SelectedLesson);
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
    public void Main_ShowSection_OtherSection_SwitchesSection()
    {
        var vm = _env.NewMain();
        vm.ShowSectionCommand.Execute(Section.Whole);
        Assert.Equal(Section.Whole, vm.SelectedSection);
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

    [Fact]
    public async Task Main_TrainLabSave_ShowsTheNewModelInThePicker()
    {
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        lab.Width = 16; lab.LayersText = "1"; lab.HeadsText = "1"; lab.ContextText = "16"; lab.BatchText = "1"; lab.StepsText = "2";
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(2));
        lab.SaveName = "Picker test";
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.Contains(vm.ModelEntries, e => e.Name == "Picker test");
        Assert.True(vm.SelectedModelEntry!.IsShipped);   // saving alone does not change the active model

        await lab.UseInLabsCommand.ExecuteAsync(null);
        Assert.Equal("Picker test", vm.SelectedModelEntry!.Name);
    }
}
