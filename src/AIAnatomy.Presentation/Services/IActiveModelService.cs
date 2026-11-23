using AIAnatomy.Models.Language;

namespace AIAnatomy.Presentation.Services;

public interface IActiveModelService
{
    LanguageModel Current { get; }
    ModelLibrary Library { get; }

    /// <summary>Loads the entry, remembers its name in the settings and raises <see cref="Changed"/>.
    /// Throws <see cref="InvalidOperationException"/> if the entry is unreadable or fails to load.</summary>
    void SetActive(ModelEntry entry);

    event EventHandler? Changed;
}
