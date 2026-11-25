using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Vision;

namespace AIAnatomy.Models.Tests;

/// <summary>Checks the family networks committed under content/models/family (digits, memory and diffusion). They only load files, so they are fast.</summary>
public class ShippedFamilyModelTests
{
    private const long OneMegabyte = 1024 * 1024;

    private static string ContentPath(params string[] parts) => Path.Combine(new[] { AppContext.BaseDirectory, "content" }.Concat(parts).ToArray());
    private static string FamilyPath(string file) => ContentPath("models", "family", file);

    public static IEnumerable<object[]> DiffusionFiles => new[]
    {
        new object[] { "diffusion-spiral.aimodel", "Spiral" },
        new object[] { "diffusion-circle.aimodel", "Circle" },
        new object[] { "diffusion-two-moons.aimodel", "Two moons" },
        new object[] { "diffusion-letter-s.aimodel", "Letter S" },
    };

    private static (ElmanRnn Rnn, AIAnatomy.Models.Tokenization.CharTokenizer Tokenizer, ModelInfo Info) LoadMemory()
    {
        using var stream = File.OpenRead(FamilyPath("memory.aimodel"));
        return ElmanRnn.Load(stream);
    }

    [Fact]
    public void Memory_LoadsShipped_AndIsUnderOneMegabyte()
    {
        var (_, _, info) = LoadMemory();
        Assert.True(info.IsShipped);
        Assert.Equal(1, info.Seed);
        Assert.True(info.Results!.ContainsKey("trainSeconds"));
        Assert.True(info.Results.ContainsKey("charactersPerSecond"));
        Assert.True(new FileInfo(FamilyPath("memory.aimodel")).Length < OneMegabyte);
        double expected = info.StepsTrained * (double)RnnTrainer.DefaultBatchSize * MemoryTraining.ContextLength / info.Results["trainSeconds"];
        Assert.True(Math.Abs(info.Results["charactersPerSecond"] - expected) <= 1e-9 * expected,
            $"recorded {info.Results["charactersPerSecond"]}, from the steps and seconds {expected}");
    }

    [Fact]
    public void Memory_ValidationLoss_MatchesRecord_AndIsBelow2_3()
    {
        var (rnn, tokenizer, info) = LoadMemory();
        var text = File.ReadAllText(ContentPath("texts", "aesop-fables.txt"));
        var trainer = new RnnTrainer(rnn, MemoryTraining.FablesSource(tokenizer, text), 1, seed: 1);

        double loss = trainer.ValidationLoss();

        Assert.True(Math.Abs(info.Results!["validationLoss"] - loss) < 1e-6, $"recorded {info.Results["validationLoss"]}, measured {loss}");
        Assert.True(loss < 2.3, $"validation loss {loss}");
    }

    [Fact]
    public void Memory_GeneratesAtLeast30PercentKnownWords()
    {
        var (rnn, tokenizer, _) = LoadMemory();
        var text = File.ReadAllText(ContentPath("texts", "aesop-fables.txt"));
        var known = text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var written = RnnSampling.Generate(rnn, tokenizer, "The ", 300, 0.8, new SeededRandom(1));

        var words = written.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        double share = words.Count(known.Contains) / (double)words.Length;
        Assert.True(share >= 0.3, $"only {share:P0} of the words are in the text: {written}");
    }

    [Theory]
    [MemberData(nameof(DiffusionFiles))]
    public void Diffusion_MeanDistance_MatchesRecord_AndIsUnder0_05(string file, string shapeName)
    {
        DiffusionNet net;
        NoiseSchedule schedule;
        string storedShape;
        ModelInfo info;
        using (var stream = File.OpenRead(FamilyPath(file))) (net, schedule, storedShape, info) = DiffusionNet.Load(stream);
        Assert.True(info.IsShipped);
        Assert.Equal(1, info.Seed);
        Assert.Equal(shapeName, storedShape);
        Assert.True(info.Results!.ContainsKey("trainSeconds"));
        Assert.True(info.Results.ContainsKey("coverage"));
        Assert.True(new FileInfo(FamilyPath(file)).Length < OneMegabyte);

        var shape = Shapes.Presets.Single(p => p.Name == shapeName).Points;
        var result = DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(1))[^1];
        var samples = Enumerable.Range(0, result.GetLength(0)).Select(i => (result[i, 0], result[i, 1])).ToList();
        double distance = Shapes.MeanDistanceToShape(samples, shape);

        Assert.True(Math.Abs(info.Results["meanDistance"] - distance) < 1e-9, $"recorded {info.Results["meanDistance"]}, measured {distance}");
        double coverage = Shapes.Coverage(samples, shape, 0.05);
        Assert.True(Math.Abs(info.Results["coverage"] - coverage) < 1e-9, $"recorded {info.Results["coverage"]}, measured {coverage}");
        Assert.True(distance < 0.05, $"mean distance {distance}");
    }

    [Fact]
    public void Digits_LoadsShipped_AccuracyMatchesRecord_AndIsAtLeast0_90()
    {
        DigitNet net;
        ModelInfo info;
        using (var stream = File.OpenRead(FamilyPath("digits.aimodel"))) (net, info) = DigitNet.Load(stream);
        Assert.True(info.IsShipped);
        Assert.Equal(1, info.Seed);
        Assert.True(info.Results!.ContainsKey("trainSeconds"));
        Assert.True(new FileInfo(FamilyPath("digits.aimodel")).Length < OneMegabyte);

        var test = DigitSet.Load(ContentPath("data", "digits", "digits-test.bin.gz"));
        double accuracy = DigitTrainer.Accuracy(net, test);

        Assert.True(Math.Abs(info.Results["testAccuracy"] - accuracy) < 1e-9, $"recorded {info.Results["testAccuracy"]}, measured {accuracy}");
        Assert.True(accuracy >= 0.90, $"test accuracy {accuracy}");
    }
}
