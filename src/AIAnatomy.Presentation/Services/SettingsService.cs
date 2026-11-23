using System.Text.Json;

namespace AIAnatomy.Presentation.Services;

public sealed class SettingsService : ISettingsService
{
    public const double MinBodyFontSize = 16;
    public const double MaxBodyFontSize = 32;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _filePath;

    /// <summary>A missing, unreadable or corrupt file gives the default settings.</summary>
    public SettingsService(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Current = Normalise(TryRead() ?? new AppSettings());
    }

    public AppSettings Current { get; private set; }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = Normalise(settings);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Current, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience: if the disk refuses, keep them for this run instead of failing the action.
        }
    }

    private AppSettings? TryRead()
    {
        try { return File.Exists(_filePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), Json) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private static AppSettings Normalise(AppSettings s)
    {
        double body = double.IsFinite(s.BodyFontSize) ? Math.Clamp(s.BodyFontSize, MinBodyFontSize, MaxBodyFontSize) : 20;
        string theme = s.Theme == "Dark" ? "Dark" : "Light";
        string? model = string.IsNullOrWhiteSpace(s.ActiveModelName) ? null : s.ActiveModelName;
        return new AppSettings(body, Math.Round(body * 1.4, MidpointRounding.AwayFromZero), theme, model);
    }
}
