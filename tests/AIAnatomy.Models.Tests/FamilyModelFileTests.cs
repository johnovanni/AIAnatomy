using System.Text;
using System.Text.Json;
using AIAnatomy.Core;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Language;

namespace AIAnatomy.Models.Tests;

public class FamilyModelFileTests
{
    private sealed record TestConfig(int Width, string Activation);

    private static readonly ModelInfo Info = new("Digits", "Handwritten digits", 120, 0.25, 7, true,
        new Dictionary<string, double> { ["testAccuracy"] = 0.9375, ["trainAccuracy"] = 0.96875 });

    private static IReadOnlyList<Parameter> TwoParameters() => new[]
    {
        new Parameter("conv.weight", Tensor.FromArray(new[] { 1.5, -2.25, 0.1, 1e-300, -0.0, 3.0 }, 2, 3)),
        new Parameter("conv.bias", Tensor.FromArray(new[] { 0.5, -0.5 }, 2)),
    };

    private static byte[] Saved(string kind = FamilyKinds.Digits, object? config = null, string? vocabulary = "0123456789",
        ModelInfo? info = null, IReadOnlyList<Parameter>? parameters = null)
    {
        using var ms = new MemoryStream();
        FamilyModelFile.Save(ms, kind, config ?? new TestConfig(8, "tanh"), vocabulary, info ?? Info, parameters ?? TwoParameters());
        return ms.ToArray();
    }

    private static FamilyModelData Open(byte[] bytes) => FamilyModelFile.Load(new MemoryStream(bytes));

    private static byte[] ReplaceInHeader(byte[] bytes, string from, string to)
    {
        string all = Encoding.Latin1.GetString(bytes);
        int index = all.IndexOf(from, StringComparison.Ordinal);
        Assert.True(index >= 0 && index < all.IndexOf('\n'), $"'{from}' not in the header");
        return Encoding.Latin1.GetBytes(all[..index] + to + all[(index + from.Length)..]);
    }

    private static void AssertPlain(InvalidDataException e)
    {
        Assert.False(string.IsNullOrWhiteSpace(e.Message));
        Assert.DoesNotContain("Exception", e.Message);
        Assert.DoesNotContain("$.", e.Message); // no JSON paths
    }

    [Fact]
    public void FamilyKinds_HaveTheAgreedNames()
    {
        Assert.Equal("digit-network", FamilyKinds.Digits);
        Assert.Equal("memory-network", FamilyKinds.Memory);
        Assert.Equal("diffusion-network", FamilyKinds.Diffusion);
        Assert.Equal(2, FamilyModelFile.Version);
    }

    [Fact]
    public void FamilyModelFile_RoundTrip_KeepsEverything()
    {
        var parameters = TwoParameters();
        var loaded = Open(Saved(parameters: parameters));

        Assert.Equal(FamilyKinds.Digits, loaded.Kind);
        Assert.Equal(new TestConfig(8, "tanh"), loaded.Config.Deserialize<TestConfig>(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("0123456789", loaded.Vocabulary);
        Assert.Equal(Info, loaded.Info);
        Assert.NotNull(loaded.Info.Results);
        Assert.Equal(0.9375, loaded.Info.Results!["testAccuracy"]);
        Assert.Equal(0.96875, loaded.Info.Results["trainAccuracy"]);
        Assert.Equal(2, loaded.Parameters.Count);
        for (int i = 0; i < parameters.Count; i++)
        {
            Assert.Equal(parameters[i].Name, loaded.Parameters[i].Name);
            Assert.Equal(parameters[i].Value.Shape, loaded.Parameters[i].Shape);
            Assert.Equal(parameters[i].Value.Data, loaded.Parameters[i].Values); // exact
        }
        Assert.True(double.IsNegative(loaded.Parameters[0].Values[4])); // -0.0 survives
    }

    [Fact]
    public void FamilyModelFile_RoundTrip_NullVocabularyAndNoResults()
    {
        var info = new ModelInfo("Memory", "Patterns", 0, null, 1, false);
        var loaded = Open(Saved(FamilyKinds.Memory, vocabulary: null, info: info));
        Assert.Equal(FamilyKinds.Memory, loaded.Kind);
        Assert.Null(loaded.Vocabulary);
        Assert.Equal(info, loaded.Info);
        Assert.Null(loaded.Info.Results);
    }

    [Fact]
    public void FamilyModelFile_HeaderIsOneJsonLineThenExactlyTheNumbers()
    {
        var bytes = Saved();
        int newline = Array.IndexOf(bytes, (byte)'\n');
        using var header = JsonDocument.Parse(bytes.AsMemory(0, newline));
        var root = header.RootElement;
        Assert.Equal(new[] { "format", "version", "kind", "config", "vocabulary", "info", "parameters" },
            root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("aianatomy-model", root.GetProperty("format").GetString());
        Assert.Equal(2, root.GetProperty("version").GetInt32());
        Assert.Equal("digit-network", root.GetProperty("kind").GetString());
        Assert.Equal(8, root.GetProperty("config").GetProperty("width").GetInt32());
        Assert.Equal(0.9375, root.GetProperty("info").GetProperty("results").GetProperty("testAccuracy").GetDouble());
        Assert.Equal("conv.bias", root.GetProperty("parameters")[1].GetProperty("name").GetString());
        Assert.Equal((6 + 2) * 8, bytes.Length - newline - 1);
        Assert.Equal(1.5, BitConverter.ToDouble(bytes, newline + 1)); // little-endian on every machine we run on
    }

    [Fact]
    public void FamilyModelFile_LargeParameter_CrossesChunksAndRoundTrips()
    {
        var data = Enumerable.Range(0, 10_000).Select(i => Math.Sin(i) * 1e3).ToArray();
        var loaded = Open(Saved(parameters: new[] { new Parameter("big", Tensor.FromArray(data, 100, 100)) }));
        Assert.Equal(data, loaded.Parameters.Single().Values);
    }

    [Fact]
    public void ModelFile_GivenFamilyFile_SaysWhatItHolds()
    {
        var e = Assert.Throws<InvalidDataException>(() => ModelFile.Load(new MemoryStream(Saved(FamilyKinds.Diffusion))));
        Assert.Equal("This file holds a diffusion-network, not a language model.", e.Message);
    }

    [Fact]
    public void FamilyModelFile_GivenLanguageModelFile_SaysSo()
    {
        using var ms = new MemoryStream();
        ModelFile.Save(SamplingTests.MakeModel(), ms);
        var e = Assert.Throws<InvalidDataException>(() => Open(ms.ToArray()));
        Assert.Equal("This is a language model file, not a family network.", e.Message);
    }

    [Fact]
    public void FamilyModelFile_Version3_GivesTheVersionMessage()
    {
        var e = Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"version\":2", "\"version\":3")));
        Assert.Equal("This model file has version 3, but this program reads version 2.", e.Message);
        var v1 = Assert.Throws<InvalidDataException>(() => ModelFile.Load(new MemoryStream(ReplaceInHeader(Saved(), "\"version\":2", "\"version\":3"))));
        Assert.Equal("This model file has version 3, but this program reads version 1.", v1.Message);
    }

    [Fact]
    public void FamilyModelFile_Truncated_ThrowsPlainInvalidData()
    {
        var bytes = Saved();
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(bytes[..^3])));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(bytes[..^16])));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(bytes[..30])));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(Array.Empty<byte>())));
    }

    [Fact]
    public void FamilyModelFile_TrailingBytes_ThrowsPlainInvalidData()
    {
        var e = Assert.Throws<InvalidDataException>(() => Open(Saved().Concat(new byte[] { 0 }).ToArray()));
        AssertPlain(e);
        Assert.Contains("extra data", e.Message);
    }

    [Fact]
    public void FamilyModelFile_NaNOrInfinity_ThrowsPlainInvalidData()
    {
        var bytes = Saved();
        BitConverter.GetBytes(double.NaN).CopyTo(bytes, bytes.Length - 8);
        var e = Assert.Throws<InvalidDataException>(() => Open(bytes));
        AssertPlain(e);
        Assert.Contains("conv.bias", e.Message);

        var inf = Saved();
        BitConverter.GetBytes(double.NegativeInfinity).CopyTo(inf, Array.IndexOf(inf, (byte)'\n') + 1);
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(inf)));
    }

    [Fact]
    public void FamilyModelFile_ShapeThatDoesNotMatchData_ThrowsPlainInvalidData()
    {
        // The header claims more numbers than the file holds.
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[2,4]"))));
        // ... or fewer, so numbers are left over.
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[2,2]"))));
        // Impossible sizes.
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[0,3]"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[-2,-3]"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[]"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[100000,100000]"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[2147483647,2147483647]"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"shape\":[2,3]", "\"shape\":[2.5,3]"))));
    }

    [Fact]
    public void FamilyModelFile_BadHeaders_ThrowPlainInvalidData()
    {
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(Encoding.UTF8.GetBytes("not a model\nat all"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(Encoding.UTF8.GetBytes("null\n"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(Encoding.UTF8.GetBytes("{\"format\":\"other\",\"version\":2}\n"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"kind\":\"digit-network\"", "\"kind\":null"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"kind\":\"digit-network\"", "\"kind\":\"\""))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"config\":{", "\"config\":7,\"x\":{"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"name\":\"Digits\"", "\"name\":null"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"parameters\":[{", "\"parameters\":[null,{"))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"name\":\"conv.bias\"", "\"name\":\"conv.weight\""))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"name\":\"conv.bias\"", "\"name\":\"\""))));
        AssertPlain(Assert.Throws<InvalidDataException>(() => Open(ReplaceInHeader(Saved(), "\"testAccuracy\":0.9375", "\"testAccuracy\":\"high\""))));
    }

    [Fact]
    public void FamilyModelFile_Save_RefusesBadInput_AndWritesNothing()
    {
        var nan = new[] { new Parameter("w", Tensor.FromArray(new[] { 1.0, double.NaN }, 2)) };
        using var ms = new MemoryStream();
        var e = Assert.Throws<InvalidOperationException>(() => FamilyModelFile.Save(ms, FamilyKinds.Digits, new TestConfig(1, "x"), null, Info, nan));
        Assert.Equal(ModelLibrary.NotFiniteMessage, e.Message);
        var badResult = Info with { Results = new Dictionary<string, double> { ["testAccuracy"] = double.PositiveInfinity } };
        Assert.Throws<InvalidOperationException>(() => FamilyModelFile.Save(ms, FamilyKinds.Digits, new TestConfig(1, "x"), null, badResult, TwoParameters()));
        var twins = new[] { new Parameter("w", Tensor.FromArray(new[] { 1.0 }, 1)), new Parameter("w", Tensor.FromArray(new[] { 2.0 }, 1)) };
        Assert.Throws<ArgumentException>(() => FamilyModelFile.Save(ms, FamilyKinds.Digits, new TestConfig(1, "x"), null, Info, twins));
        Assert.Throws<ArgumentException>(() => FamilyModelFile.Save(ms, " ", new TestConfig(1, "x"), null, Info, TwoParameters()));
        Assert.Throws<ArgumentException>(() => FamilyModelFile.Save(ms, FamilyKinds.Digits, 5, null, Info, TwoParameters()));
        Assert.Throws<ArgumentNullException>(() => FamilyModelFile.Save(ms, FamilyKinds.Digits, null!, null, Info, TwoParameters()));
        Assert.Equal(0, ms.Length);
    }

    [Fact]
    public void ModelInfo_Equality_ComparesResultsByContent()
    {
        var a = new ModelInfo("n", "t", 1, null, 1, false, new Dictionary<string, double> { ["x"] = 1, ["y"] = 2 });
        var b = new ModelInfo("n", "t", 1, null, 1, false, new Dictionary<string, double> { ["y"] = 2, ["x"] = 1 });
        var c = new ModelInfo("n", "t", 1, null, 1, false, new Dictionary<string, double> { ["x"] = 1, ["y"] = 3 });
        var none = new ModelInfo("n", "t", 1, null, 1, false);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, none);
        Assert.Equal(none, new ModelInfo("n", "t", 1, null, 1, false, null));
    }
}
