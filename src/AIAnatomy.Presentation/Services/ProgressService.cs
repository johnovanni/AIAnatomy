using System.Text.Json;

namespace AIAnatomy.Presentation.Services;

public sealed class ProgressService : IProgressService
{
    private sealed record Stored(List<string>? Opened, List<string>? Checked);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _filePath;
    private readonly HashSet<string> _opened = new(StringComparer.Ordinal);
    private readonly HashSet<string> _checked = new(StringComparer.Ordinal);

    /// <summary>A missing, unreadable or corrupt file gives empty progress.</summary>
    public ProgressService(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        var stored = TryRead();
        foreach (var id in stored?.Opened ?? []) if (!string.IsNullOrWhiteSpace(id)) _opened.Add(id);
        foreach (var id in stored?.Checked ?? []) if (!string.IsNullOrWhiteSpace(id)) _checked.Add(id);
    }

    public string? LastSaveProblem { get; private set; }
    public event EventHandler? Changed;
    public event EventHandler? SaveFailed;

    public LessonMark MarkOf(string lessonId) =>
        _checked.Contains(lessonId) ? LessonMark.Checked : _opened.Contains(lessonId) ? LessonMark.Opened : LessonMark.NotOpened;

    public void MarkOpened(string lessonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lessonId);
        if (MarkOf(lessonId) != LessonMark.NotOpened) return;
        _opened.Add(lessonId);
        SaveAndRaise();
    }

    public void MarkChecked(string lessonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lessonId);
        if (!_checked.Add(lessonId)) return;
        SaveAndRaise();
    }

    public void Reset()
    {
        if (_opened.Count == 0 && _checked.Count == 0) return;
        _opened.Clear();
        _checked.Clear();
        SaveAndRaise();
    }

    private void SaveAndRaise()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            string temp = _filePath + ".tmp";
            try
            {
                var data = new Stored(_opened.Order(StringComparer.Ordinal).ToList(), _checked.Order(StringComparer.Ordinal).ToList());
                File.WriteAllText(temp, JsonSerializer.Serialize(data, Json));
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
            // Keep the marks for this run, but say so: the window shows the problem to the user (final review M1).
            LastSaveProblem = e.Message;
            SaveFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    private Stored? TryRead()
    {
        try { return File.Exists(_filePath) ? JsonSerializer.Deserialize<Stored>(File.ReadAllText(_filePath), Json) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}
