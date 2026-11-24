using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void Settings_MissingFile_GivesDefaults()
    {
        var s = _env.NewSettings().Current;
        Assert.Equal(13, s.BodyFontSize);
        Assert.Equal("Dark", s.Theme);
        Assert.Equal("Radial", s.IdeaMapLayout);
        Assert.Equal(SettingsService.CurrentScaleVersion, s.ScaleVersion);
    }

    [Fact]
    public void Settings_CorruptFile_GivesDefaults()
    {
        File.WriteAllText(_env.SettingsFile, "{ this is not json");
        Assert.Equal(new AppSettings(13, "Dark", null, "Radial", 2), new SettingsService(_env.SettingsFile).Current);
    }

    [Theory]
    [InlineData(20, 13)]
    [InlineData(16, 13)]
    [InlineData(32, 13)]
    [InlineData(14, 14)]
    public void Settings_OldFile_MovesToNewScaleOnce(double savedBody, double expected)
    {
        File.WriteAllText(_env.SettingsFile, $$"""{ "BodyFontSize": {{savedBody}}, "HeadingFontSize": 28, "Theme": "Light" }""");
        var s = _env.NewSettings().Current;
        Assert.Equal(expected, s.BodyFontSize);
        Assert.Equal("Light", s.Theme);
        Assert.Equal(2, s.ScaleVersion);
    }

    [Fact]
    public void Settings_NewScaleFile_KeepsSixteen()
    {
        File.WriteAllText(_env.SettingsFile, """{ "BodyFontSize": 16, "ScaleVersion": 2 }""");
        Assert.Equal(16, _env.NewSettings().Current.BodyFontSize);
    }

    [Fact]
    public void Settings_MigrationDoesNotRepeat_AfterASaveAtSixteen()
    {
        File.WriteAllText(_env.SettingsFile, """{ "BodyFontSize": 20 }""");
        var service = _env.NewSettings();
        Assert.Equal(13, service.Current.BodyFontSize);
        service.Save(service.Current with { BodyFontSize = 16 });
        Assert.Equal(16, _env.NewSettings().Current.BodyFontSize);
    }

    [Theory]
    [InlineData(4, 10)]
    [InlineData(30, 24)]
    [InlineData(13, 13)]
    public void Settings_ClampsFontSize(double asked, double body)
    {
        var service = new SettingsService(_env.SettingsFile);
        service.Save(new AppSettings(asked, ScaleVersion: 2));
        Assert.Equal(body, service.Current.BodyFontSize);
    }

    [Fact]
    public void Settings_UnknownTheme_BecomesDark()
    {
        var service = new SettingsService(_env.SettingsFile);
        service.Save(new AppSettings(Theme: "Purple"));
        Assert.Equal("Dark", service.Current.Theme);
    }

    [Fact]
    public void Settings_UnknownMapLayout_BecomesRadial()
    {
        var service = new SettingsService(_env.SettingsFile);
        service.Save(new AppSettings(IdeaMapLayout: "Spiral"));
        Assert.Equal("Radial", service.Current.IdeaMapLayout);
        service.Save(new AppSettings(IdeaMapLayout: "Loop"));
        Assert.Equal("Loop", service.Current.IdeaMapLayout);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        new SettingsService(_env.SettingsFile).Save(new AppSettings(24, "Light", "Induction demo", "Loop"));
        var loaded = new SettingsService(_env.SettingsFile).Current;
        Assert.Equal(new AppSettings(24, "Light", "Induction demo", "Loop", 2), loaded);
    }

    [Fact]
    public void Settings_LastLesson_RoundTrips_AndBlankIsNone()
    {
        // Final review M13: the last opened lesson is kept with the other settings.
        new SettingsService(_env.SettingsFile).Save(new AppSettings(LastLessonId: "attention"));
        Assert.Equal("attention", new SettingsService(_env.SettingsFile).Current.LastLessonId);
        new SettingsService(_env.SettingsFile).Save(new AppSettings(LastLessonId: "  "));
        Assert.Null(new SettingsService(_env.SettingsFile).Current.LastLessonId);
    }

    [Fact]
    public void Settings_OldFiles_WithoutALastLesson_StillLoadAndMigrate()
    {
        // A file from before the last lesson was kept (old scale too) loads as before, with no last lesson.
        File.WriteAllText(_env.SettingsFile, """{ "BodyFontSize": 20, "Theme": "Light", "IdeaMapLayout": "Loop" }""");
        var s = _env.NewSettings().Current;
        Assert.Equal(new AppSettings(13, "Light", null, "Loop", 2, null), s);

        File.WriteAllText(_env.SettingsFile, """{ "BodyFontSize": 16, "ScaleVersion": 2, "LastLessonId": "tokens" }""");
        Assert.Equal(new AppSettings(16, "Dark", null, "Radial", 2, "tokens"), _env.NewSettings().Current);
    }

    [Fact]
    public void Settings_SaveFails_KeepsCurrent_ReportsProblem_AndRaisesEvent()
    {
        Directory.CreateDirectory(_env.SettingsFile);   // a folder where the file should be
        var service = new SettingsService(_env.SettingsFile);
        int raised = 0;
        service.SaveFailed += (_, _) => raised++;

        service.Save(new AppSettings(Theme: "Dark"));

        Assert.Equal("Dark", service.Current.Theme);
        Assert.False(string.IsNullOrWhiteSpace(service.LastSaveProblem));
        Assert.Equal(1, raised);
        Assert.False(File.Exists(_env.SettingsFile + ".tmp"));
    }

    [Fact]
    public void Settings_SaveSucceeds_ClearsProblem_AndLeavesNoTempFile()
    {
        var service = new SettingsService(_env.SettingsFile);
        service.Save(new AppSettings(Theme: "Dark"));
        Assert.Null(service.LastSaveProblem);
        Assert.False(File.Exists(_env.SettingsFile + ".tmp"));
    }

    [Fact]
    public void Settings_SaveCreatesMissingFolder()
    {
        var path = Path.Combine(_env.Root, "deeper", "settings.json");
        new SettingsService(path).Save(new AppSettings(Theme: "Dark"));
        Assert.Equal("Dark", new SettingsService(path).Current.Theme);
    }
}
