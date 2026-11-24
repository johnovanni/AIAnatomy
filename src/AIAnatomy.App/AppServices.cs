using System.IO;
using AIAnatomy.Lessons;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App;

/// <summary>Everything the window needs, built from <see cref="AppPaths"/>.</summary>
public sealed record AppServices(LessonCatalog Lessons, SourceList Sources, ConnectionList Connections, IdeaMapData IdeaMap,
    ModelLibrary Library, ISettingsService Settings, IActiveModelService Models, IProgressService Progress)
{
    /// <summary>
    /// Loads the lessons, sources, connections, idea map and models. Throws <see cref="InvalidOperationException"/> (no usable shipped model)
    /// or <see cref="InvalidDataException"/> / <see cref="IOException"/> (missing or broken content), each with a plain message.
    /// </summary>
    public static AppServices Create(ISettingsService settings) => Create(settings, AppPaths.UserModels, AppPaths.Progress);

    /// <summary>The same, with the user's models and the lesson marks in <paramref name="userModelsFolder"/> (diagnostic runs use a temporary folder, so they never touch the real marks).</summary>
    public static AppServices Create(ISettingsService settings, string userModelsFolder) =>
        Create(settings, userModelsFolder, Path.Combine(userModelsFolder, "progress.json"));

    private static AppServices Create(ISettingsService settings, string userModelsFolder, string progressFile)
    {
        var lessons = LessonCatalog.Load(AppPaths.Content);
        var sources = SourceList.Load(Path.Combine(AppPaths.Content, "sources.json"));
        var connections = ConnectionList.Load(Path.Combine(AppPaths.Content, "connections.json"));
        var ideaMap = IdeaMapData.Load(Path.Combine(AppPaths.Content, "idea-map.json"));
        var library = new ModelLibrary(AppPaths.ShippedModels, userModelsFolder);
        var models = new ActiveModelService(library, settings);
        return new AppServices(lessons, sources, connections, ideaMap, library, settings, models, new ProgressService(progressFile));
    }

    public MainViewModel NewMainViewModel() =>
        new(Lessons, Sources, Models, Settings, Progress, Connections, IdeaMap, new MsaglGraphLayout());

    /// <summary>A new idea map over this content, laid out by MSAGL.</summary>
    public IdeaMapViewModel NewIdeaMap(IProgressService progress) =>
        new(Lessons, Connections, IdeaMap, progress, Settings, Models, new MsaglGraphLayout());
}
