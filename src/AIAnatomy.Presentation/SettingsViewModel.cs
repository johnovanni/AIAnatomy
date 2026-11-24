using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation;

/// <summary>
/// The Settings page: text size (whole points, 10 to 24) and Light or Dark. Every change is saved at once and
/// <see cref="Applied"/> is raised so the window can show it; the saved values are in <see cref="ISettingsService.Current"/>.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IProgressService _progress;

    public SettingsViewModel(ISettingsService settings, IProgressService progress)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
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

    /// <summary>True while "Reset progress" waits for "Yes, forget my progress" or "Keep it" (final review M9).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetProgressCommand))]
    private bool isConfirmingReset;

    /// <summary>"Progress forgotten." after a confirmed reset; null otherwise.</summary>
    [ObservableProperty] private string? progressMessage;

    /// <summary>The first step: asks before anything is forgotten. Disabled (but kept in its place) while the question
    /// shows, so the second click of a double-click lands on it and confirms nothing (re-review N1).</summary>
    [RelayCommand(CanExecute = nameof(CanResetProgress))]
    private void ResetProgress()
    {
        if (IsConfirmingReset) return;
        ProgressMessage = null;
        IsConfirmingReset = true;
    }

    /// <summary>The second step: forgets which lessons have been opened or checked, and says so.</summary>
    [RelayCommand]
    private void ConfirmResetProgress()
    {
        _progress.Reset();
        IsConfirmingReset = false;
        ProgressMessage = "Progress forgotten.";
    }

    private bool CanResetProgress() => !IsConfirmingReset;

    [RelayCommand]
    private void CancelResetProgress() => IsConfirmingReset = false;

    /// <summary>Leaving the Settings page drops an unanswered question (re-review N2).</summary>
    public void Leave() => IsConfirmingReset = false;

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
