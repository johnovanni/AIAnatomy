using AIAnatomy.Lessons;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.IdeaMap;
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
    public string ProgressFile => Path.Combine(Root, "progress.json");
    public string UserModels => Path.Combine(Root, "models");

    public ModelLibrary NewLibrary() => new(Path.Combine(Content, "models"), UserModels);
    public SettingsService NewSettings() => new(SettingsFile);
    public ProgressService NewProgress() => new(ProgressFile);
    public ActiveModelService NewActiveModels(ISettingsService? settings = null) => new(NewLibrary(), settings ?? NewSettings());

    public MainViewModel NewMain(ActiveModelService? models = null, ISettingsService? settings = null, IProgressService? progress = null)
    {
        settings ??= NewSettings();
        models ??= NewActiveModels(settings);
        return new MainViewModel(LessonCatalog.Load(Content), SourceList.Load(Path.Combine(Content, "sources.json")), models, settings,
            progress ?? NewProgress(), ConnectionList.Load(Path.Combine(Content, "connections.json")),
            IdeaMapData.Load(Path.Combine(Content, "idea-map.json")), new MsaglGraphLayout(), Content);
    }

    /// <summary>A lesson file's text without its "## Check yourself" section, for tests that bring their own questions or
    /// branches (the shipped questions' see: anchors name the shipped branches).</summary>
    public static string WithoutCheckYourself(string lesson) =>
        System.Text.RegularExpressions.Regex.Replace(lesson, @"(?ms)^## Check yourself\s*$.*?(?=^## |\z)", "");

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { /* temp folder; the OS cleans it up eventually */ }
    }
}
