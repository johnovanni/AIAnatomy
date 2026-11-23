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

    private static AppSettings Normalise(AppSettings s)
    {
        double body = double.IsFinite(s.BodyFontSize) ? Math.Clamp(s.BodyFontSize, MinBodyFontSize, MaxBodyFontSize) : 20;
        string theme = s.Theme == "Dark" ? "Dark" : "Light";
        string? model = string.IsNullOrWhiteSpace(s.ActiveModelName) ? null : s.ActiveModelName;
        return new AppSettings(body, Math.Round(body * 1.4, MidpointRounding.AwayFromZero), theme, model);
    }
}
