using AIAnatomy.Models.Language;

namespace AIAnatomy.Presentation.Services;

public interface IActiveModelService
{
    LanguageModel Current { get; }
    ModelLibrary Library { get; }

    /// <summary>The file the active model was loaded from.</summary>
    string ActivePath { get; }

    /// <summary>True when the entry is the active model's file (compared by full path, ignoring case), so a model saved
    /// over the active file is recognised as the active one.</summary>
    bool IsActive(ModelEntry entry);

    /// <summary>Loads the entry, remembers its name in the settings and raises <see cref="Changed"/>.
    /// Throws <see cref="InvalidOperationException"/> if the entry is unreadable or fails to load.</summary>
    void SetActive(ModelEntry entry);

    event EventHandler? Changed;
}
