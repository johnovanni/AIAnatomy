using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Models.Tokenization;

namespace AIAnatomy.Presentation.Services;

/// <summary>
/// The shipped family networks (content/models/family), for the Part B labs. Each file is read from disk once per
/// instance (so once per window: <see cref="LabFactory"/> builds one), lazily and off the UI thread, and its bytes are
/// kept. Every call then builds a fresh network from those bytes, so a lab that trains or changes its copy never touches
/// another lab's copy. A missing or unreadable file gives an <see cref="InvalidDataException"/> with a plain message the
/// lab can show (a bad file every call, from the kept bytes); a failed read is not kept, so a later call tries the disk again.
/// </summary>
public sealed class FamilyModels
{
    private const string MemoryFile = "memory.aimodel";

    /// <summary>Each diffusion preset (a <see cref="Shapes.Presets"/> name) and the file holding the network that learned it.</summary>
    private static readonly IReadOnlyDictionary<string, string> DiffusionFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Spiral"] = "diffusion-spiral.aimodel",
        ["Circle"] = "diffusion-circle.aimodel",
        ["Two moons"] = "diffusion-two-moons.aimodel",
        ["Letter S"] = "diffusion-letter-s.aimodel",
    };

    private readonly string _folder;
    private readonly object _gate = new();
    private readonly Dictionary<string, Task<byte[]>> _files = new(StringComparer.Ordinal);

    public FamilyModels(string contentFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFolder);
        _folder = Path.Combine(contentFolder, "models", "family");
    }

    /// <summary>The preset names that have a shipped diffusion network.</summary>
    public static IReadOnlyCollection<string> DiffusionPresets => (IReadOnlyCollection<string>)DiffusionFiles.Keys;

    /// <summary>A fresh copy of the shipped memory network (an Elman network trained on Aesop's fables), its tokenizer and info.</summary>
    public async Task<(ElmanRnn Rnn, CharTokenizer Tokenizer, ModelInfo Info)> MemoryAsync()
    {
        var bytes = await ReadOnceAsync(MemoryFile).ConfigureAwait(false);
        return await Task.Run(() => Parse(MemoryFile, bytes, ElmanRnn.Load)).ConfigureAwait(false);
    }

    /// <summary>A fresh copy of the shipped diffusion network that learned the named preset shape ("Spiral", "Circle",
    /// "Two moons" or "Letter S"). Any other name throws <see cref="ArgumentException"/>.</summary>
    public async Task<(DiffusionNet Net, NoiseSchedule Schedule, string ShapeName, ModelInfo Info)> DiffusionAsync(string presetName)
    {
        ArgumentNullException.ThrowIfNull(presetName);
        if (!DiffusionFiles.TryGetValue(presetName, out var file))
            throw new ArgumentException($"There is no shipped diffusion network for the shape '{presetName}'.", nameof(presetName));
        var bytes = await ReadOnceAsync(file).ConfigureAwait(false);
        return await Task.Run(() => Parse(file, bytes, DiffusionNet.Load)).ConfigureAwait(false);
    }

    /// <summary>The file's bytes, read on a worker thread the first time and kept. Calls made while the read runs share it.</summary>
    private Task<byte[]> ReadOnceAsync(string file)
    {
        lock (_gate)
        {
            if (_files.TryGetValue(file, out var cached) && !cached.IsFaulted && !cached.IsCanceled) return cached;
            var read = Task.Run(() => ReadBytes(file));
            _files[file] = read;
            return read;
        }
    }

    private byte[] ReadBytes(string file)
    {
        try { return File.ReadAllBytes(Path.Combine(_folder, file)); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidDataException($"The network file '{file}' is missing from AI Anatomy's content folder. Reinstalling the app puts it back.", e);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"The network file '{file}' cannot be read: {e.Message}", e);
        }
    }

    private static T Parse<T>(string file, byte[] bytes, Func<Stream, T> load)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        try { return load(stream); }
        catch (InvalidDataException e)
        {
            throw new InvalidDataException($"The network file '{file}' cannot be opened. {e.Message}", e);
        }
    }
}
