using System.Text;
using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Tests;

public class ModelFileTests
{
    private static byte[] Saved(LanguageModel model)
    {
        using var ms = new MemoryStream();
        ModelFile.Save(model, ms);
        return ms.ToArray();
    }

    private static LanguageModel Open(byte[] bytes) => ModelFile.Load(new MemoryStream(bytes));

    private static byte[] ReplaceInHeader(byte[] bytes, string from, string to)
    {
        string all = Encoding.Latin1.GetString(bytes);
        int index = all.IndexOf(from, StringComparison.Ordinal);
        Assert.True(index >= 0 && index < all.IndexOf('\n'), $"'{from}' not in the header");
        return Encoding.Latin1.GetBytes(all[..index] + to + all[(index + from.Length)..]);
    }

    [Fact]
    public void ModelFile_RoundTrip_GivesIdenticalLogits()
    {
        var model = SamplingTests.MakeModel();
        var loaded = Open(Saved(model));
        Assert.Equal(model.Transformer.Config, loaded.Transformer.Config);
        Assert.Equal(model.Info, loaded.Info);
        Assert.Equal(model.Tokenizer.Vocabulary, loaded.Tokenizer.Vocabulary);
        var a = model.Predict("abc ab").Forward.Logits.Data;
        var b = loaded.Predict("abc ab").Forward.Logits.Data;
        Assert.Equal(a, b); // exact, not approximate
    }

    [Fact]
    public void ModelFile_RoundTrip_KeepsInfoVocabularyAndSinusoidalConfig()
    {
        var tokenizer = CharTokenizer.FromText("ab\n\"\\é");
        var config = new TransformerConfig(tokenizer.VocabularySize, 6, 8, 2, 2, 16, PositionKind.Sinusoidal, UseFeedForward: false);
        var model = new LanguageModel(new Transformer(config, new SeededRandom(4)), tokenizer, new ModelInfo("n \"q\"", "t", 12, 1.5, 4, true));
        var loaded = Open(Saved(model));
        Assert.Equal(config, loaded.Transformer.Config);
        Assert.Equal(model.Info, loaded.Info);
        Assert.Equal(tokenizer.Vocabulary, loaded.Tokenizer.Vocabulary);
    }

    [Fact]
    public void ModelFile_HeaderIsOneJsonLineThenExactlyTheNumbers()
    {
        var model = SamplingTests.MakeModel();
        var bytes = Saved(model);
        int newline = Array.IndexOf(bytes, (byte)'\n');
        var header = System.Text.Json.JsonDocument.Parse(bytes.AsMemory(0, newline));
        Assert.Equal("aianatomy-model", header.RootElement.GetProperty("format").GetString());
        Assert.Equal(1, header.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(model.Transformer.Config.CountParameters() * 8, bytes.Length - newline - 1);
    }

    [Fact]
    public void ModelFile_Truncated_ThrowsInvalidData()
    {
        var bytes = Saved(SamplingTests.MakeModel());
        Assert.Throws<InvalidDataException>(() => Open(bytes[..^3]));
        Assert.Throws<InvalidDataException>(() => Open(bytes[..30]));  // inside the header
        Assert.Throws<InvalidDataException>(() => Open(Array.Empty<byte>()));
    }

    [Fact]
    public void ModelFile_ExtraBytes_ThrowsInvalidData()
    {
        var bytes = Saved(SamplingTests.MakeModel()).Concat(new byte[] { 1 }).ToArray();
        Assert.Throws<InvalidDataException>(() => Open(bytes));
    }

    [Fact]
    public void ModelFile_Garbage_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => Open(Encoding.UTF8.GetBytes("not a model\nat all")));
        Assert.Throws<InvalidDataException>(() => Open(Encoding.UTF8.GetBytes("{\"format\":\"other\",\"version\":1}\n")));
        Assert.Throws<InvalidDataException>(() => Open(Encoding.UTF8.GetBytes("null\n")));
        Assert.Throws<InvalidDataException>(() => Open(new byte[] { 0xFF, 0xFE, 0x00, 0x0A }));
    }

    [Fact]
    public void ModelFile_WrongFormatOrVersion_ThrowsInvalidData()
    {
        var bytes = Saved(SamplingTests.MakeModel());
        var wrongFormat = Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "aianatomy-model", "aianatomy-other!")));
        Assert.Contains("not an AI Anatomy model", wrongFormat.Message);
        var wrongVersion = Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"version\":1", "\"version\":2")));
        Assert.Contains("version 2", wrongVersion.Message);
    }

    [Fact]
    public void ModelFile_ParameterNameOrShapeMismatch_ThrowsInvalidData()
    {
        var bytes = Saved(SamplingTests.MakeModel());
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "token_embedding", "token_embeddinX")));
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"name\":\"output.weight\",\"shape\":[8,", "\"name\":\"output.weight\",\"shape\":[9,")));
    }

    [Fact]
    public void ModelFile_ImpossibleConfig_ThrowsInvalidData()
    {
        var bytes = Saved(SamplingTests.MakeModel());
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"heads\":2", "\"heads\":3")));
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"layers\":1", "\"layers\":-1")));
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"heads\":2", "\"heads\":0")));
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"modelWidth\":8", "\"modelWidth\":0")));
    }

    [Fact]
    public void ModelFile_NullParameterEntry_ThrowsInvalidData()
    {
        var bytes = Saved(SamplingTests.MakeModel());
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"parameters\":[{", "\"parameters\":[null,{")));
        Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(bytes, "\"name\":\"token_embedding\"", "\"name\":null")));
    }

    [Fact]
    public void ModelFile_NonFiniteNumber_ThrowsInvalidData()
    {
        var bytes = Saved(SamplingTests.MakeModel());
        BitConverter.GetBytes(double.NaN).CopyTo(bytes, bytes.Length - 8);
        Assert.Throws<InvalidDataException>(() => Open(bytes));
    }
}

public class ModelLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aianatomy-tests-" + Guid.NewGuid().ToString("N"));
    private string Shipped => Path.Combine(_root, "shipped");
    private string User => Path.Combine(_root, "user");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private ModelLibrary Library() => new(Shipped, User);

    private void WriteShipped(string name, LanguageModel model)
    {
        Directory.CreateDirectory(Shipped);
        using var stream = File.Create(Path.Combine(Shipped, name + ".aimodel"));
        ModelFile.Save(model, stream);
    }

    [Fact]
    public void Library_MissingShippedFolder_GivesEmptyListAndCreatesUserFolder()
    {
        var library = Library();
        Assert.Empty(library.List());
        Assert.True(Directory.Exists(User));
    }

    [Fact]
    public void Library_SaveThenList_ShowsUserModelWithInfo()
    {
        var library = Library();
        var model = SamplingTests.MakeModel();
        var entry = library.Save(model, "My model");
        Assert.False(entry.IsShipped);
        Assert.True(entry.IsReadable);
        Assert.Equal("My model", entry.Name);
        var listed = Assert.Single(library.List());
        Assert.Equal(entry, listed);
        var loaded = library.Load(listed);
        Assert.Equal("My model", loaded.Info.Name);
        Assert.False(loaded.Info.IsShipped);
        Assert.Equal(model.Predict("abc").Forward.Logits.Data, loaded.Predict("abc").Forward.Logits.Data);
    }

    [Fact]
    public void Library_Save_MakesFileNameSafe()
    {
        var library = Library();
        var entry = library.Save(SamplingTests.MakeModel(), "a/b:c*d?");
        Assert.Equal("a_b_c_d_.aimodel", Path.GetFileName(entry.Path));
        Assert.Equal("a/b:c*d?", entry.Name);
        Assert.Equal("_.aimodel", Path.GetFileName(library.Save(SamplingTests.MakeModel(), "...").Path));
        Assert.Equal("CON_.aimodel", Path.GetFileName(library.Save(SamplingTests.MakeModel(), "CON").Path));
        Assert.Throws<ArgumentException>(() => library.Save(SamplingTests.MakeModel(), "   "));
    }

    [Fact]
    public void Library_PathFor_IsTheFileSaveUses_AndSharedByNamesThatDifferOnlyInUnsafeCharacters()
    {
        var library = Library();
        Assert.Equal(library.PathFor("a/b"), library.PathFor("a_b"));
        Assert.Equal(library.PathFor("x"), library.PathFor("  x "));
        Assert.Equal(Path.Combine(User, "a_b.aimodel"), library.PathFor("a/b"));
        Assert.Equal(library.PathFor("a/b"), library.Save(SamplingTests.MakeModel(), "a/b").Path);
        Assert.Throws<ArgumentException>(() => library.PathFor(" "));
    }

    [Fact]
    public void Library_Save_RefusesNonFiniteWeights()
    {
        var library = Library();
        var model = SamplingTests.MakeModel();
        model.Transformer.Parameters().Last().Value.Data[0] = double.NaN;
        var e = Assert.Throws<InvalidOperationException>(() => library.Save(model, "broken"));
        Assert.Equal(ModelLibrary.NotFiniteMessage, e.Message);
        Assert.Equal("This model has numbers that are not finite, so it can't be saved.", ModelLibrary.NotFiniteMessage);
        Assert.Empty(Directory.GetFiles(User));

        model.Transformer.Parameters().Last().Value.Data[0] = double.PositiveInfinity;
        Assert.Throws<InvalidOperationException>(() => library.Save(model, "broken"));
        Assert.Empty(Directory.GetFiles(User));
    }

    [Fact]
    public void Library_SaveSameNameTwice_Overwrites()
    {
        var library = Library();
        library.Save(SamplingTests.MakeModel(seed: 1), "same");
        library.Save(SamplingTests.MakeModel(seed: 2), "same");
        Assert.Single(library.List());
        Assert.Empty(Directory.GetFiles(User, "*.tmp"));
    }

    [Fact]
    public void Library_ListsShippedFirstThenUserByName_AndLoadsShippedAsShipped()
    {
        WriteShipped("zeta", SamplingTests.MakeModel());
        var library = Library();
        library.Save(SamplingTests.MakeModel(), "beta");
        library.Save(SamplingTests.MakeModel(), "alpha");
        var list = library.List();
        Assert.Equal(new[] { true, false, false }, list.Select(e => e.IsShipped).ToArray());
        Assert.Equal(new[] { "test", "alpha", "beta" }, list.Select(e => e.Name).ToArray());
        Assert.True(library.Load(list[0]).Info.IsShipped);
    }

    [Fact]
    public void Library_CorruptUserFile_ListedUnreadable()
    {
        var library = Library();
        library.Save(SamplingTests.MakeModel(), "good");
        File.WriteAllBytes(Path.Combine(User, "broken.aimodel"), new byte[] { 1, 2, 3, 250, 251 });
        File.WriteAllBytes(Path.Combine(User, "empty.aimodel"), Array.Empty<byte>());
        var list = library.List();
        Assert.Equal(3, list.Count);
        var broken = list.Single(e => e.Name == "broken");
        Assert.False(broken.IsReadable);
        Assert.False(string.IsNullOrWhiteSpace(broken.Problem));
        Assert.False(list.Single(e => e.Name == "empty").IsReadable);
        Assert.True(list.Single(e => e.Name == "good").IsReadable);
        Assert.Throws<InvalidDataException>(() => library.Load(broken));
    }

    [Fact]
    public void Library_CraftedHeaders_ListedUnreadableWithoutThrowing()
    {
        var library = Library();
        var good = SamplingTests.MakeModel();
        using var ms = new MemoryStream();
        ModelFile.Save(good, ms);
        string all = System.Text.Encoding.Latin1.GetString(ms.ToArray());
        File.WriteAllBytes(Path.Combine(User, "zero-heads.aimodel"), System.Text.Encoding.Latin1.GetBytes(all.Replace("\"heads\":2", "\"heads\":0")));
        File.WriteAllBytes(Path.Combine(User, "null-entry.aimodel"), System.Text.Encoding.Latin1.GetBytes(all.Replace("\"parameters\":[{", "\"parameters\":[null,{")));
        var list = library.List();
        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.False(e.IsReadable));
    }

    [Fact]
    public void Library_Delete_RemovesUserFile()
    {
        var library = Library();
        var entry = library.Save(SamplingTests.MakeModel(), "gone");
        library.Delete(entry);
        Assert.Empty(library.List());
    }

    [Fact]
    public void Library_DeleteShipped_Throws()
    {
        WriteShipped("fixed", SamplingTests.MakeModel());
        var library = Library();
        var shipped = Assert.Single(library.List());
        Assert.Throws<InvalidOperationException>(() => library.Delete(shipped));
        Assert.True(File.Exists(shipped.Path));
        // A forged entry that claims to be a user file but points into the shipped folder is refused too.
        Assert.Throws<InvalidOperationException>(() => library.Delete(shipped with { IsShipped = false }));
    }
}
