namespace AIAnatomy.Models.Language;

/// <summary>One file in the library. Unreadable files are still listed, with the reason in <c>Problem</c>.</summary>
public sealed record ModelEntry(string Name, string Path, bool IsShipped, bool IsReadable, string? Problem);

/// <summary>The models that ship with the app (read-only) and the ones the user saved. Files are <c>*.aimodel</c>.</summary>
public sealed class ModelLibrary
{
    public const string Extension = ".aimodel";
    private static readonly string[] ReservedNames =
        { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    private readonly string _shippedFolder;
    private readonly string _userFolder;

    public ModelLibrary(string shippedFolder, string userFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shippedFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(userFolder);
        _shippedFolder = shippedFolder;
        _userFolder = userFolder;
        try { Directory.CreateDirectory(_userFolder); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* List then shows nothing; Save reports the real error. */ }
    }

    /// <summary>Shipped models first, then the user's, each group by name.</summary>
    public IReadOnlyList<ModelEntry> List() => ListFolder(_shippedFolder, true).Concat(ListFolder(_userFolder, false)).ToList();

    public LanguageModel Load(ModelEntry e)
    {
        ArgumentNullException.ThrowIfNull(e);
        try
        {
            using var stream = File.OpenRead(e.Path);
            var model = ModelFile.Load(stream);
            return e.IsShipped ? new LanguageModel(model.Transformer, model.Tokenizer, model.Info with { IsShipped = true }) : model;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The model file could not be opened: " + ex.Message);
        }
    }

    /// <summary>Saves into the user folder, replacing any model with the same file name (the caller confirms overwrites).</summary>
    public ModelEntry Save(LanguageModel m, string name)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(name);
        name = name.Trim();
        if (name.Length == 0) throw new ArgumentException("Give the model a name.", nameof(name));

        Directory.CreateDirectory(_userFolder);
        string path = Path.Combine(_userFolder, SafeFileName(name) + Extension);
        string temp = path + ".tmp";
        try
        {
            using (var stream = File.Create(temp))
                ModelFile.Save(new LanguageModel(m.Transformer, m.Tokenizer, m.Info with { Name = name, IsShipped = false }), stream);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return new ModelEntry(name, path, false, true, null);
    }

    /// <summary>Deletes a user's model file. Shipped models cannot be deleted.</summary>
    public void Delete(ModelEntry e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.IsShipped || IsInside(_shippedFolder, e.Path))
            throw new InvalidOperationException("Models that come with the app cannot be deleted.");
        File.Delete(e.Path);
    }

    private static IEnumerable<ModelEntry> ListFolder(string folder, bool shipped)
    {
        string[] files;
        try
        {
            files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*" + Extension) : Array.Empty<string>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<ModelEntry>();
        }

        return files.Where(f => f.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)).Select(path => Inspect(path, shipped))
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ModelEntry Inspect(string path, bool shipped)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        try
        {
            using var stream = File.OpenRead(path);
            var info = ModelFile.Load(stream).Info;
            return new ModelEntry(string.IsNullOrWhiteSpace(info.Name) ? stem : info.Name, path, shipped, true, null);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new ModelEntry(stem, path, shipped, false, e.Message);
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray();
        string stem = new string(chars).TrimEnd('.', ' ');
        if (stem.Length == 0) stem = "_";
        if (stem.Length > 100) stem = stem[..100].TrimEnd('.', ' ');
        if (ReservedNames.Contains(stem.Split('.')[0], StringComparer.OrdinalIgnoreCase)) stem += "_";
        return stem;
    }

    private static bool IsInside(string folder, string path)
    {
        string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
