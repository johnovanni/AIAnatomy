using AIAnatomy.Models.Language;

namespace AIAnatomy.Presentation.Services;

public sealed class ActiveModelService : IActiveModelService
{
    public const string DefaultModelName = "Fables model";

    private readonly ISettingsService _settings;

    /// <summary>Starts with the saved model, or the shipped "Fables model" if that one is missing, unreadable or fails to load.</summary>
    public ActiveModelService(ModelLibrary library, ISettingsService settings)
    {
        Library = library ?? throw new ArgumentNullException(nameof(library));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        (Current, ActivePath) = LoadInitial();
    }

    public LanguageModel Current { get; private set; }
    public ModelLibrary Library { get; }
    public string ActivePath { get; private set; }
    public event EventHandler? Changed;

    public bool IsActive(ModelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.Equals(Path.GetFullPath(entry.Path), Path.GetFullPath(ActivePath), StringComparison.OrdinalIgnoreCase);
    }

    public void SetActive(ModelEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsReadable)
            throw new InvalidOperationException($"'{entry.Name}' cannot be used: {entry.Problem}");
        LanguageModel model;
        try { model = Library.Load(entry); }
        catch (InvalidDataException e) { throw new InvalidOperationException($"'{entry.Name}' cannot be used: {e.Message}", e); }

        Current = model;
        ActivePath = entry.Path;
        _settings.Save(_settings.Current with { ActiveModelName = entry.Name });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private (LanguageModel Model, string Path) LoadInitial()
    {
        var entries = Library.List();
        var saved = _settings.Current.ActiveModelName;
        var savedEntry = saved == null ? null : entries.FirstOrDefault(e => e.Name == saved && e.IsReadable);
        if (TryLoad(savedEntry) is { } model) return (model, savedEntry!.Path);

        var fables = entries.FirstOrDefault(e => e.IsShipped && e.Name == DefaultModelName && e.IsReadable);
        return TryLoad(fables) is { } shipped ? (shipped, fables!.Path) : throw new InvalidOperationException(
            $"The model that comes with the app (\"{DefaultModelName}\") could not be loaded. Please reinstall AI Anatomy.");
    }

    private LanguageModel? TryLoad(ModelEntry? entry)
    {
        if (entry == null) return null;
        try { return Library.Load(entry); }
        catch (InvalidDataException) { return null; }
    }
}
