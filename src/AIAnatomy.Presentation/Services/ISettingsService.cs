namespace AIAnatomy.Presentation.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Normalises the settings (body size 10 to 24 pt, theme Dark or Light, map layout Radial or Loop, current scale version, a blank last lesson as none), stores them and updates <see cref="Current"/>.</summary>
    void Save(AppSettings settings);

    /// <summary>Why the last <see cref="Save"/> could not write the file, or null if it succeeded. <see cref="Current"/> is updated either way.</summary>
    string? LastSaveProblem { get; }

    /// <summary>Raised when a save could not be written to disk (see <see cref="LastSaveProblem"/>).</summary>
    event EventHandler? SaveFailed;
}
