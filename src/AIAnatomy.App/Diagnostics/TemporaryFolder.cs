using System.IO;

namespace AIAnatomy.App.Diagnostics;

/// <summary>An empty folder under %TEMP% for a diagnostic run's user models, deleted afterwards. Never the user's real folder.</summary>
public sealed class TemporaryFolder : IDisposable
{
    public TemporaryFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aianatomy-diagnostic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* a temp folder; the system clears it eventually */ }
    }
}
