using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation;

/// <summary>
/// The Settings page: text size (whole points, 16 to 32) and Light or Dark. Every change is saved at once and
/// <see cref="Applied"/> is raised so the window can show it; the saved values are in <see cref="ISettingsService.Current"/>.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        bodyFontSize = settings.Current.BodyFontSize;
        isDarkTheme = settings.Current.Theme == "Dark";
    }

    public double MinimumFontSize => SettingsService.MinBodyFontSize;
    public double MaximumFontSize => SettingsService.MaxBodyFontSize;

    [ObservableProperty] private double bodyFontSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightTheme))]
    private bool isDarkTheme;

    public bool IsLightTheme
    {
        get => !IsDarkTheme;
        set { if (value) IsDarkTheme = false; }
    }

    /// <summary>Raised after a change has been saved.</summary>
    public event EventHandler? Applied;

    partial void OnBodyFontSizeChanged(double value)
    {
        double whole = Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), MinimumFontSize, MaximumFontSize);
        if (whole != value) { BodyFontSize = whole; return; }   // re-enters with the whole number, which saves
        Save();
    }

    partial void OnIsDarkThemeChanged(bool value) => Save();

    private void Save()
    {
        _settings.Save(_settings.Current with { BodyFontSize = BodyFontSize, Theme = IsDarkTheme ? "Dark" : "Light" });
        Applied?.Invoke(this, EventArgs.Empty);
    }
}
