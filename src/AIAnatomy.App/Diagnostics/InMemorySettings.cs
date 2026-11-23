using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App.Diagnostics;

/// <summary>Settings for a diagnostic run: kept in memory only, so a render or self-test never changes the user's file.</summary>
public sealed class InMemorySettings(AppSettings settings) : ISettingsService
{
    public AppSettings Current { get; private set; } = settings;
    public string? LastSaveProblem => null;
    public event EventHandler? SaveFailed { add { } remove { } }

    public void Save(AppSettings settings) => Current = settings;
}
