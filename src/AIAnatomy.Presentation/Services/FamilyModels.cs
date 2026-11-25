using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Vision;

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
    private const string DigitsFile = "digits.aimodel";
    private const string DigitTestFile = "digits-test.bin.gz";
    private const string DigitTrainFile = "digits-train.bin.gz";

    /// <summary>Each diffusion preset (a <see cref="Shapes.Presets"/> name) and the file holding the network that learned it.</summary>
    private static readonly IReadOnlyDictionary<string, string> DiffusionFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Spiral"] = "diffusion-spiral.aimodel",
        ["Circle"] = "diffusion-circle.aimodel",
        ["Two moons"] = "diffusion-two-moons.aimodel",
        ["Letter S"] = "diffusion-letter-s.aimodel",
    };

    private readonly string _folder;
    private readonly string _digitData;
    private readonly object _gate = new();
    private readonly Dictionary<string, Task<byte[]>> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<DigitSet>> _sets = new(StringComparer.Ordinal);
    private readonly Func<string, byte[]> _readAllBytes;

    public FamilyModels(string contentFolder) : this(contentFolder, File.ReadAllBytes) { }

    /// <summary>For tests: <paramref name="readAllBytes"/> stands in for <see cref="File.ReadAllBytes(string)"/>, so the
    /// number of reads can be counted.</summary>
    internal FamilyModels(string contentFolder, Func<string, byte[]> readAllBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFolder);
        ArgumentNullException.ThrowIfNull(readAllBytes);
        _folder = Path.Combine(contentFolder, "models", "family");
        _digitData = Path.Combine(contentFolder, "data", "digits");
        _readAllBytes = readAllBytes;
    }

    /// <summary>A fresh copy of the shipped digits network (8x8 images) and its info.</summary>
    public async Task<(DigitNet Net, ModelInfo Info)> DigitsAsync()
    {
        var bytes = await ReadOnceAsync(DigitsFile).ConfigureAwait(false);
        return await Task.Run(() => Parse(DigitsFile, bytes, DigitNet.Load)).ConfigureAwait(false);
    }

    /// <summary>The bundled digits test set (1,740 images). A <see cref="DigitSet"/> is read-only, so it is parsed once and
    /// every call returns the same instance.</summary>
    public Task<DigitSet> DigitTestSetAsync() => DigitSetOnceAsync(DigitTestFile);

    /// <summary>The bundled digits training set (3,760 images). Parsed once; every call returns the same read-only instance.</summary>
    public Task<DigitSet> DigitTrainSetAsync() => DigitSetOnceAsync(DigitTrainFile);

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

    private Task<DigitSet> DigitSetOnceAsync(string file)
    {
        lock (_gate)
        {
            if (_sets.TryGetValue(file, out var cached) && !cached.IsFaulted && !cached.IsCanceled) return cached;
            var load = LoadSetAsync(file);
            _sets[file] = load;
            return load;
        }
    }

    private async Task<DigitSet> LoadSetAsync(string file)
    {
        var bytes = await ReadOnceAsync(file, _digitData).ConfigureAwait(false);
        // DigitSet.Load wraps what it expects (and a damaged gzip) in an InvalidDataException that names only the file.
        return await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return DigitSet.Load(stream, file);
        }).ConfigureAwait(false);
    }

    /// <summary>The file's bytes, read on a worker thread the first time and kept. Calls made while the read runs share it.</summary>
    private Task<byte[]> ReadOnceAsync(string file, string? folder = null)
    {
        lock (_gate)
        {
            if (_files.TryGetValue(file, out var cached) && !cached.IsFaulted && !cached.IsCanceled) return cached;
            var read = Task.Run(() => ReadBytes(file, folder ?? _folder));
            _files[file] = read;
            return read;
        }
    }

    private byte[] ReadBytes(string file, string folder)
    {
        try { return _readAllBytes(Path.Combine(folder, file)); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidDataException($"The network file '{file}' is missing from AI Anatomy's content folder. Reinstalling the app puts it back.", e);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            // The system's message may hold the full path; the plain message names only the file.
            throw new InvalidDataException($"The network file '{file}' cannot be read. Check that AI Anatomy's content folder can be opened, or reinstall the app.", e);
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
        catch (Exception e) when (e is IOException or FormatException or OverflowException or ArgumentException
                                   or System.Text.Json.JsonException or InvalidOperationException)
        {
            // The loaders turn what they expect into InvalidDataException; anything else that slips past (a stream that
            // ends early, a number that does not parse) is still a bad file, and the lab shows the same plain message.
            throw new InvalidDataException($"The network file '{file}' cannot be opened: it is damaged or cut short.", e);
        }
    }
}
