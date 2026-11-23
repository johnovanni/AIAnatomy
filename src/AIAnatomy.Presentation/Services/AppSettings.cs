namespace AIAnatomy.Presentation.Services;

/// <summary>User settings. Theme is "Light" or "Dark"; ActiveModelName is the library name of the chosen model.</summary>
public sealed record AppSettings(double BodyFontSize = 20, double HeadingFontSize = 28, string Theme = "Light", string? ActiveModelName = null);
