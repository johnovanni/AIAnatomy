using AIAnatomy.Core;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;

namespace AIAnatomy.Models.Tests;

/// <summary>Checks the two models committed under content/models. They only load files, so they are fast.</summary>
public class ShippedModelTests
{
    private static string ContentPath(params string[] parts) => Path.Combine(new[] { AppContext.BaseDirectory, "content" }.Concat(parts).ToArray());

    private static LanguageModel Load(string file)
    {
        using var stream = File.OpenRead(ContentPath("models", file));
        return ModelFile.Load(stream);
    }

    [Fact]
    public void FablesModel_Loads_AndIsMarkedShipped()
    {
        var model = Load("fables-small.aimodel");
        Assert.True(model.Info.IsShipped);
        Assert.Equal("Fables model", model.Info.Name);
        Assert.Equal("Aesop's Fables (Townsend)", model.Info.TrainingTextName);
        Assert.True(model.Info.StepsTrained > 0);
    }

    [Fact]
    public void FablesModel_ValidationLossBelow1_9()
    {
        var model = Load("fables-small.aimodel");
        Assert.NotNull(model.Info.ValidationLoss);
        Assert.True(model.Info.ValidationLoss < 1.9, $"validation loss {model.Info.ValidationLoss}");
    }

    [Fact]
    public void FablesModel_GeneratesMostlyKnownWords()
    {
        var model = Load("fables-small.aimodel");
        var text = File.ReadAllText(ContentPath("texts", "aesop-fables.txt"));
        var known = text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var written = Sampling.Generate(model, "The ", 300, new SamplingSettings(Temperature: 0.8), new SeededRandom(1));

        var words = written.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        double share = words.Count(known.Contains) / (double)words.Length;
        Assert.True(share >= 0.4, $"only {share:P0} of the words are in the text: {written}");
    }

    [Fact]
    public void InductionModel_HasHeadAbove0_6()
    {
        var model = Load("induction-demo.aimodel");
        Assert.Equal("Induction demo", model.Info.Name);
        Assert.Equal("Repeated letter sequences", model.Info.TrainingTextName);
        double best = Enumerable.Range(0, InductionExperiment.Config.Heads)
            .Max(head => InductionExperiment.InductionScore(model.Transformer, 1, head, seed: 9));
        Assert.True(best > 0.6, $"best layer-2 induction score {best}");
    }
}
