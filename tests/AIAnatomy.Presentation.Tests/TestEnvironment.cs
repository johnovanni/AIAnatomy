using AIAnatomy.Lessons;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

/// <summary>A temp user folder plus the shipped content copied beside the tests. Never touches %LOCALAPPDATA%.</summary>
public sealed class TestEnvironment : IDisposable
{
    public static readonly string Content = Path.Combine(AppContext.BaseDirectory, "content");

    public TestEnvironment()
    {
        Root = Path.Combine(Path.GetTempPath(), "aianatomy-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string UserModels => Path.Combine(Root, "models");

    public ModelLibrary NewLibrary() => new(Path.Combine(Content, "models"), UserModels);
    public SettingsService NewSettings() => new(SettingsFile);
    public ActiveModelService NewActiveModels(ISettingsService? settings = null) => new(NewLibrary(), settings ?? NewSettings());

    public MainViewModel NewMain(ActiveModelService? models = null, ISettingsService? settings = null)
    {
        settings ??= NewSettings();
        models ??= NewActiveModels(settings);
        return new MainViewModel(LessonCatalog.Load(Content), SourceList.Load(Path.Combine(Content, "sources.json")), models, settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { /* temp folder; the OS cleans it up eventually */ }
    }
}
