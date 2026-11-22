using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Tests;

public class SamplingTests
{
    internal static LanguageModel MakeModel(string text = "abc abc\nabc", int context = 8, int seed = 1)
    {
        var tokenizer = CharTokenizer.FromText(text);
        var config = new TransformerConfig(tokenizer.VocabularySize, context, ModelWidth: 8, Layers: 1, Heads: 2, FeedForwardWidth: 16);
        var transformer = new Transformer(config, new SeededRandom(seed));
        var rng = new SeededRandom(seed + 100);
        foreach (var p in transformer.Parameters())
            for (int i = 0; i < p.Value.Size; i++) p.Value.Data[i] = rng.NextGaussian() * 0.5;
        return new LanguageModel(transformer, tokenizer, new ModelInfo("test", "text", 0, null, seed, false));
    }

    private static double Entropy(double[] p) => -p.Where(x => x > 0).Sum(x => x * Math.Log(x));

    [Fact]
    public void Sampling_TemperatureZero_IsArgmax()
    {
        var p = Sampling.Probabilities(new[] { 1.0, 3.0, 2.0 }, new SamplingSettings(Temperature: 0));
        Assert.Equal(new[] { 0.0, 1.0, 0.0 }, p);
    }

    [Fact]
    public void Sampling_TopK1_IsArgmax()
    {
        var p = Sampling.Probabilities(new[] { 1.0, 3.0, 2.0 }, new SamplingSettings(TopK: 1));
        Assert.Equal(new[] { 0.0, 1.0, 0.0 }, p);
    }

    [Fact]
    public void Sampling_HighTemperature_FlattensDistribution()
    {
        var logits = new[] { 1.0, 3.0, 2.0, 0.5 };
        double normal = Entropy(Sampling.Probabilities(logits, new SamplingSettings(Temperature: 1)));
        double hot = Entropy(Sampling.Probabilities(logits, new SamplingSettings(Temperature: 5)));
        double cold = Entropy(Sampling.Probabilities(logits, new SamplingSettings(Temperature: 0.2)));
        Assert.True(hot > normal && normal > cold);
        Assert.Equal(1.0, Sampling.Probabilities(logits, new SamplingSettings(Temperature: 5)).Sum(), 12);
    }

    [Fact]
    public void Sampling_TopP_KeepsSmallestSetAboveP()
    {
        var logits = new[] { 0.5, 0.3, 0.2 }.Select(x => Math.Log(x)).ToArray();
        var p = Sampling.Probabilities(logits, new SamplingSettings(TopP: 0.7));
        Assert.Equal(0.625, p[0], 12);
        Assert.Equal(0.375, p[1], 12);
        Assert.Equal(0.0, p[2]);
    }

    [Fact]
    public void Sampling_TopK_KeepsKLargestAndRenormalises()
    {
        var p = Sampling.Probabilities(new[] { 2.0, 1.0, 0.0, -1.0 }, new SamplingSettings(TopK: 2));
        Assert.Equal(0.0, p[2]);
        Assert.Equal(0.0, p[3]);
        Assert.Equal(1.0, p.Sum(), 12);
        Assert.Equal(Math.E, p[0] / p[1], 9);
    }

    [Fact]
    public void Sample_FollowsProbabilitiesAndNeverPicksZeroChoices()
    {
        var rng = new SeededRandom(5);
        var counts = new int[3];
        for (int i = 0; i < 2000; i++) counts[Sampling.Sample(new[] { 0.75, 0.0, 0.25 }, rng)]++;
        Assert.Equal(0, counts[1]);
        Assert.InRange(counts[0], 1400, 1600);
    }

    [Fact]
    public void Generate_EmptyPrompt_Works()
    {
        var model = MakeModel();
        string text = Sampling.Generate(model, "", 10, new SamplingSettings(), new SeededRandom(1));
        Assert.Equal(10, text.Length);
    }

    [Fact]
    public void Generate_EmptyPromptWithoutNewlineInVocabulary_StartsFromUnknown()
    {
        var model = MakeModel("abcabc");
        Assert.Equal(5, Sampling.Generate(model, "", 5, new SamplingSettings(), new SeededRandom(1)).Length);
    }

    [Fact]
    public void Generate_SameSeed_SameText()
    {
        var model = MakeModel();
        string a = Sampling.Generate(model, "ab", 20, new SamplingSettings(), new SeededRandom(3));
        string b = Sampling.Generate(model, "ab", 20, new SamplingSettings(), new SeededRandom(3));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Generate_LongerThanContext_KeepsGoing()
    {
        var model = MakeModel(context: 4);
        Assert.Equal(30, Sampling.Generate(model, "abcabc", 30, new SamplingSettings(), new SeededRandom(2)).Length);
    }

    [Fact]
    public void Predict_LongInput_UsesLastContextTokensAndReportsTruncation()
    {
        var model = MakeModel(context: 8);
        var result = model.Predict("abcabcabcabc"); // 12 tokens
        Assert.True(result.WasTruncated);
        Assert.Equal(4, result.DroppedTokenCount);
        Assert.Equal(model.Tokenizer.Encode("abcabcabcabc").Skip(4).ToArray(), result.UsedTokenIds.ToArray());
        Assert.Equal(8, result.Forward.Logits.Shape[0]);

        var shortResult = model.Predict("abc");
        Assert.False(shortResult.WasTruncated);
        Assert.Equal(0, shortResult.DroppedTokenCount);
    }

    [Fact]
    public void Predict_EmptyText_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => MakeModel().Predict(""));
        Assert.Contains("Type some text to begin", ex.Message);
    }

    [Fact]
    public void Predict_UnknownCharacters_DoNotCrash()
    {
        var result = MakeModel().Predict("a\U0001F600é");
        Assert.Equal(new[] { MakeModel().Tokenizer.Encode("a")[0], 0, 0 }, result.UsedTokenIds.ToArray());
    }
}
