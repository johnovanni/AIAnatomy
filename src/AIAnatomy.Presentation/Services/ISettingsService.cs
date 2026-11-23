namespace AIAnatomy.Presentation.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Normalises the settings (body size 16 to 32, heading = body x 1.4, theme Light or Dark), stores them and updates <see cref="Current"/>.</summary>
    void Save(AppSettings settings);
}
