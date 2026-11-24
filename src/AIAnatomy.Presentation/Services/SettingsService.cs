using System.Text.Json;

namespace AIAnatomy.Presentation.Services;

public sealed class SettingsService : ISettingsService
{
    public const double MinBodyFontSize = 10;
    public const double MaxBodyFontSize = 24;
    public const double DefaultBodyFontSize = 13;

    /// <summary>The text scale now in use. Files saved under an older scale (version below this) are moved once, on loading.</summary>
    public const int CurrentScaleVersion = 2;

    // Under the old scale (16 to 32 pt, default 20) a saved 16 or more meant "a comfortable size"; it becomes the new default.
    private const double OldScaleMinimum = 16;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _filePath;

    /// <summary>A missing, unreadable or corrupt file gives the default settings.</summary>
    public SettingsService(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        Current = Normalise(Migrate(TryRead() ?? new AppSettings { ScaleVersion = CurrentScaleVersion }));
    }

    public AppSettings Current { get; private set; }
    public string? LastSaveProblem { get; private set; }
    public event EventHandler? SaveFailed;

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = Normalise(settings);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            string temp = _filePath + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(Current, Json));
                File.Move(temp, _filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            LastSaveProblem = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Keep the settings for this run, but say so: the caller shows the problem to the user.
            LastSaveProblem = e.Message;
            SaveFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    private AppSettings? TryRead()
    {
        try { return File.Exists(_filePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), Json) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Moves a file saved under an older text scale to the current one. Applied on loading only, so a later save is never moved.</summary>
    private static AppSettings Migrate(AppSettings s)
    {
        if (s.ScaleVersion >= CurrentScaleVersion) return s;
        bool oldLarge = double.IsFinite(s.BodyFontSize) && s.BodyFontSize >= OldScaleMinimum;
        return s with { BodyFontSize = oldLarge ? DefaultBodyFontSize : s.BodyFontSize };
    }

    private static AppSettings Normalise(AppSettings s)
    {
        double body = double.IsFinite(s.BodyFontSize) ? Math.Clamp(s.BodyFontSize, MinBodyFontSize, MaxBodyFontSize) : DefaultBodyFontSize;
        string theme = s.Theme == "Light" ? "Light" : "Dark";
        string layout = s.IdeaMapLayout == "Loop" ? "Loop" : "Radial";
        string? model = string.IsNullOrWhiteSpace(s.ActiveModelName) ? null : s.ActiveModelName;
        string? lastLesson = string.IsNullOrWhiteSpace(s.LastLessonId) ? null : s.LastLessonId;
        return new AppSettings(body, theme, model, layout, CurrentScaleVersion, lastLesson);
    }
}
