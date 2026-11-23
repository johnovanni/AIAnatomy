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

        var vm = new SettingsViewModel(_env.NewSettings());

        Assert.Equal(24, vm.BodyFontSize);
        Assert.True(vm.IsDarkTheme);
        Assert.False(vm.IsLightTheme);
        Assert.Equal(16, vm.MinimumFontSize);
        Assert.Equal(32, vm.MaximumFontSize);
    }

    [Fact]
    public void ChangingTextSize_SavesAndRaisesApplied()
    {
        var settings = _env.NewSettings();
        var vm = new SettingsViewModel(settings);
        int applied = 0;
        vm.Applied += (_, _) => applied++;

        vm.BodyFontSize = 27.6;   // a slider drag: kept to whole points

        Assert.Equal(28, vm.BodyFontSize);
        Assert.Equal(28, settings.Current.BodyFontSize);
        Assert.Equal(39, settings.Current.HeadingFontSize);
        Assert.Equal(28, _env.NewSettings().Current.BodyFontSize);   // written to disk
        Assert.Equal(1, applied);
    }

    [Fact]
    public void ChoosingDark_SavesTheme()
    {
        var settings = _env.NewSettings();
        var vm = new SettingsViewModel(settings);
        int applied = 0;
        vm.Applied += (_, _) => applied++;

        vm.IsDarkTheme = true;

        Assert.Equal("Dark", settings.Current.Theme);
        Assert.False(vm.IsLightTheme);
        Assert.Equal(1, applied);

        vm.IsLightTheme = true;
        Assert.Equal("Light", settings.Current.Theme);
        Assert.Equal(2, applied);
    }

    [Fact]
    public void KeepsTheActiveModelName()
    {
        var settings = _env.NewSettings();
        settings.Save(settings.Current with { ActiveModelName = "Induction demo" });
        var vm = new SettingsViewModel(settings);

        vm.IsDarkTheme = true;

        Assert.Equal("Induction demo", settings.Current.ActiveModelName);
    }

    [Fact]
    public void MainViewModel_ExposesSettingsPage()
    {
        var vm = _env.NewMain();
        Assert.NotNull(vm.SettingsPage);
        Assert.Equal(20, vm.SettingsPage.BodyFontSize);
    }
}
