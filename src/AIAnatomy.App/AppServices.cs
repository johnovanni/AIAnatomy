using System.IO;
using AIAnatomy.Lessons;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App;

/// <summary>Everything the window needs, built from <see cref="AppPaths"/>.</summary>
public sealed record AppServices(LessonCatalog Lessons, SourceList Sources, ModelLibrary Library, ISettingsService Settings, IActiveModelService Models)
{
    /// <summary>
    /// Loads the lessons, sources and models. Throws <see cref="InvalidOperationException"/> (no usable shipped model)
    /// or <see cref="InvalidDataException"/> / <see cref="IOException"/> (missing or broken content), each with a plain message.
    /// </summary>
    public static AppServices Create(ISettingsService settings)
    {
        var lessons = LessonCatalog.Load(AppPaths.Content);
        var sources = SourceList.Load(Path.Combine(AppPaths.Content, "sources.json"));
        var library = new ModelLibrary(AppPaths.ShippedModels, AppPaths.UserModels);
        var models = new ActiveModelService(library, settings);
        return new AppServices(lessons, sources, library, settings, models);
    }

    public MainViewModel NewMainViewModel() => new(Lessons, Sources, Models, Settings);
}
