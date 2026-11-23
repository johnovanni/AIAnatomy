namespace AIAnatomy.Presentation.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Normalises the settings (body size 16 to 32, heading = body x 1.4, theme Light or Dark), stores them and updates <see cref="Current"/>.</summary>
    void Save(AppSettings settings);

    /// <summary>Why the last <see cref="Save"/> could not write the file, or null if it succeeded. <see cref="Current"/> is updated either way.</summary>
    string? LastSaveProblem { get; }

    /// <summary>Raised when a save could not be written to disk (see <see cref="LastSaveProblem"/>).</summary>
    event EventHandler? SaveFailed;
}
