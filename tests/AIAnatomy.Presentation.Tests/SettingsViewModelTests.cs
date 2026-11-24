namespace AIAnatomy.Presentation.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void StartsFromSavedSettings()
    {
        var settings = _env.NewSettings();
        settings.Save(settings.Current with { BodyFontSize = 24, Theme = "Dark" });

        var vm = new SettingsViewModel(_env.NewSettings(), _env.NewProgress());

        Assert.Equal(24, vm.BodyFontSize);
        Assert.True(vm.IsDarkTheme);
        Assert.False(vm.IsLightTheme);
        Assert.Equal(10, vm.MinimumFontSize);
        Assert.Equal(24, vm.MaximumFontSize);
    }

    [Fact]
    public void ChangingTextSize_SavesAndRaisesApplied()
    {
        var settings = _env.NewSettings();
        var vm = new SettingsViewModel(settings, _env.NewProgress());
        int applied = 0;
        vm.Applied += (_, _) => applied++;

        vm.BodyFontSize = 17.6;   // a slider drag: kept to whole points

        Assert.Equal(18, vm.BodyFontSize);
        Assert.Equal(18, settings.Current.BodyFontSize);
        Assert.Equal(18, _env.NewSettings().Current.BodyFontSize);   // written to disk
        Assert.Equal(1, applied);
    }

    [Fact]
    public void ChoosingDark_SavesTheme()
    {
        var settings = _env.NewSettings();
        var vm = new SettingsViewModel(settings, _env.NewProgress());
        int applied = 0;
        vm.Applied += (_, _) => applied++;

        vm.IsLightTheme = true;

        Assert.Equal("Light", settings.Current.Theme);
        Assert.False(vm.IsDarkTheme);
        Assert.Equal(1, applied);

        vm.IsDarkTheme = true;
        Assert.Equal("Dark", settings.Current.Theme);
        Assert.Equal(2, applied);
    }

    [Fact]
    public void KeepsTheActiveModelName()
    {
        var settings = _env.NewSettings();
        settings.Save(settings.Current with { ActiveModelName = "Induction demo" });
        var vm = new SettingsViewModel(settings, _env.NewProgress());

        vm.IsLightTheme = true;

        Assert.Equal("Induction demo", settings.Current.ActiveModelName);
    }

    [Fact]
    public void MainViewModel_ExposesSettingsPage()
    {
        var vm = _env.NewMain();
        Assert.NotNull(vm.SettingsPage);
        Assert.Equal(13, vm.SettingsPage.BodyFontSize);
    }

    [Fact]
    public void ResetProgress_AsksFirst_ThenForgets_AndSaysSo()
    {
        // Final review M9: one click only asks; "Yes, forget my progress" forgets; a line says it was done.
        var progress = _env.NewProgress();
        progress.MarkChecked("tokens");
        var vm = new SettingsViewModel(_env.NewSettings(), progress);
        Assert.False(vm.IsConfirmingReset);
        Assert.Null(vm.ProgressMessage);

        vm.ResetProgressCommand.Execute(null);
        Assert.True(vm.IsConfirmingReset);
        Assert.Equal(Services.LessonMark.Checked, progress.MarkOf("tokens"));

        vm.CancelResetProgressCommand.Execute(null);
        Assert.False(vm.IsConfirmingReset);
        Assert.Equal(Services.LessonMark.Checked, progress.MarkOf("tokens"));
        Assert.Null(vm.ProgressMessage);

        vm.ResetProgressCommand.Execute(null);
        vm.ConfirmResetProgressCommand.Execute(null);
        Assert.False(vm.IsConfirmingReset);
        Assert.Equal(Services.LessonMark.NotOpened, progress.MarkOf("tokens"));
        Assert.Equal("Progress forgotten.", vm.ProgressMessage);

        vm.ResetProgressCommand.Execute(null);   // asking again clears the old line
        Assert.Null(vm.ProgressMessage);
    }

    [Fact]
    public void ResetProgress_StaysInPlaceButDisabled_WhileAsking()
    {
        // Re-review N1: the Reset button keeps its place (disabled) while the question shows, so a double-click's second
        // click lands on it, never on "Yes, forget my progress".
        var progress = _env.NewProgress();
        progress.MarkChecked("tokens");
        var vm = new SettingsViewModel(_env.NewSettings(), progress);
        Assert.True(vm.ResetProgressCommand.CanExecute(null));

        vm.ResetProgressCommand.Execute(null);
        Assert.False(vm.ResetProgressCommand.CanExecute(null));
        vm.ResetProgressCommand.Execute(null);   // the second click of a double-click
        Assert.True(vm.IsConfirmingReset);
        Assert.Equal(Services.LessonMark.Checked, progress.MarkOf("tokens"));

        vm.CancelResetProgressCommand.Execute(null);
        Assert.True(vm.ResetProgressCommand.CanExecute(null));
    }
}
