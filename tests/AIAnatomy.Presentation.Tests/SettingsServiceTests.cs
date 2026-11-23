using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void Settings_MissingFile_GivesDefaults()
    {
        var s = new SettingsService(_env.SettingsFile).Current;
        Assert.Equal(new AppSettings(20, 28, "Light", null), s);
    }

    [Fact]
    public void Settings_CorruptFile_GivesDefaults()
    {
        File.WriteAllText(_env.SettingsFile, "{ this is not json");
        Assert.Equal(new AppSettings(20, 28, "Light", null), new SettingsService(_env.SettingsFile).Current);
    }

    [Theory]
    [InlineData(40, 32, 45)]
    [InlineData(10, 16, 22)]
    [InlineData(20, 20, 28)]
    public void Settings_ClampsFontSize(double asked, double body, double heading)
    {
        var service = new SettingsService(_env.SettingsFile);
        service.Save(new AppSettings(asked, 1));
        Assert.Equal(body, service.Current.BodyFontSize);
        Assert.Equal(heading, service.Current.HeadingFontSize);
    }

    [Fact]
    public void Settings_UnknownTheme_BecomesLight()
    {
        var service = new SettingsService(_env.SettingsFile);
        service.Save(new AppSettings(Theme: "Purple"));
        Assert.Equal("Light", service.Current.Theme);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        new SettingsService(_env.SettingsFile).Save(new AppSettings(24, 0, "Dark", "Induction demo"));
        var loaded = new SettingsService(_env.SettingsFile).Current;
        Assert.Equal(new AppSettings(24, 34, "Dark", "Induction demo"), loaded);
    }

    [Fact]
    public void Settings_SaveCreatesMissingFolder()
    {
        var path = Path.Combine(_env.Root, "deeper", "settings.json");
        new SettingsService(path).Save(new AppSettings(Theme: "Dark"));
        Assert.Equal("Dark", new SettingsService(path).Current.Theme);
    }
}
