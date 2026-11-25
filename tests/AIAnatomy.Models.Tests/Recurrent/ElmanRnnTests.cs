using AIAnatomy.Core;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;

namespace AIAnatomy.Models.Tests.Recurrent;

/// <summary>The shipped Fables model and the fables text, as the memory network uses them.</summary>
internal static class Fables
{
    private static readonly Lazy<LanguageModel> LazyModel = new(() =>
    {
        using var stream = File.OpenRead(ContentPath("models", "fables-small.aimodel"));
        return ModelFile.Load(stream);
    });

    private static readonly Lazy<string> LazyText = new(() => File.ReadAllText(ContentPath("texts", "aesop-fables.txt")));

    public static LanguageModel Model => LazyModel.Value;
    public static string Text => LazyText.Value;

    private static string ContentPath(params string[] parts) => Path.Combine(new[] { AppContext.BaseDirectory, "content" }.Concat(parts).ToArray());
}

public class ElmanRnnTests
{
    private static ElmanRnn Make(int vocabulary, int hidden, int seed = 1) => new(new RnnConfig(vocabulary, hidden), new SeededRandom(seed));

    /// <summary>Replaces the starting weights with larger ones so every part visibly matters.</summary>
    private static void Randomise(ElmanRnn rnn, int seed)
    {
        var rng = new SeededRandom(seed);
        foreach (var p in rnn.Parameters())
            for (int i = 0; i < p.Value.Size; i++) p.Value.Data[i] = rng.NextGaussian() * 0.5;
    }

    [Fact]
    public void Parameters_HaveTheAgreedNamesAndShapes()
    {
        var rnn = Make(7, 4);
        var parameters = rnn.Parameters().ToList();
        Assert.Equal(new[] { "input.weight", "recurrent.weight", "hidden.bias", "output.weight", "output.bias" }, parameters.Select(p => p.Name));
        Assert.Equal(new[] { 7, 4 }, parameters[0].Value.Shape);
        Assert.Equal(new[] { 4, 4 }, parameters[1].Value.Shape);
        Assert.Equal(new[] { 4 }, parameters[2].Value.Shape);
        Assert.Equal(new[] { 4, 7 }, parameters[3].Value.Shape);
        Assert.Equal(new[] { 7 }, parameters[4].Value.Shape);
        Assert.Equal(new RnnConfig(7, 4), rnn.Config);
        Assert.Equal(128, new RnnConfig(7).Hidden);
    }

    [Fact]
    public void Forward_ShapesAndPositionMajorRows()
    {
        var rnn = Make(7, 4);
        Randomise(rnn, 2);
        var sequences = new[] { new[] { 1, 2, 3, 4, 5 }, new[] { 6, 0, 2, 2, 1 }, new[] { 3, 3, 3, 1, 6 } };

        var result = rnn.Forward(sequences);

        Assert.Equal(5, result.Hidden.Count);
        Assert.All(result.Hidden, h => Assert.Equal(new[] { 3, 4 }, h.Shape));
        Assert.Equal(new[] { 15, 7 }, result.Logits.Shape);
        // Row t*B + b is sequence b at position t: the same numbers as running sequence b on its own.
        for (int b = 0; b < 3; b++)
        {
            var alone = rnn.Forward(new[] { sequences[b] });
            for (int t = 0; t < 5; t++)
            {
                Assert.Equal(alone.Logits.Row(t), result.Logits.Row(t * 3 + b));
                Assert.Equal(alone.Hidden[t].Row(0), result.Hidden[t].Row(b));
            }
        }
    }

    [Fact]
    public void Forward_FollowsTheElmanEquations()
    {
        var rnn = Make(5, 3);
        Randomise(rnn, 3);
        var p = rnn.Parameters().Select(x => x.Value).ToArray();
        var ids = new[] { 2, 4 };
        var result = rnn.Forward(new[] { ids });

        var h = new double[3];
        for (int t = 0; t < ids.Length; t++)
        {
            var next = new double[3];
            for (int j = 0; j < 3; j++)
            {
                double sum = p[0][ids[t], j] + p[2][j];
                for (int k = 0; k < 3; k++) sum += h[k] * p[1][k, j];
                next[j] = Math.Tanh(sum);
            }
            h = next;
            for (int j = 0; j < 3; j++) Assert.Equal(h[j], result.Hidden[t][0, j], 12);
            for (int v = 0; v < 5; v++)
            {
                double logit = p[4][v];
                for (int k = 0; k < 3; k++) logit += h[k] * p[3][k, v];
                Assert.Equal(logit, result.Logits[t, v], 12);
            }
        }
    }

    [Fact]
    public void Forward_RefusesUnequalLengthsAndOutsideIds()
    {
        var rnn = Make(5, 3);
        Assert.Throws<ArgumentException>(() => rnn.Forward(new[] { new[] { 1, 2 }, new[] { 1 } }));
        Assert.Throws<ArgumentException>(() => rnn.Forward(Array.Empty<int[]>()));
        Assert.Throws<ArgumentException>(() => rnn.Forward(new[] { Array.Empty<int>() }));
        Assert.Throws<ArgumentOutOfRangeException>(() => rnn.Forward(new[] { new[] { 1, 5 } }));
    }

    [Fact]
    public void GradCheck_TrainingLossOfATinyRnn()
    {
        var config = new RnnConfig(5, 4);
        var rnn = new ElmanRnn(config, new SeededRandom(4));
        Randomise(rnn, 4);
        // Two windows of 4 tokens: 3 steps of inputs, then the targets shifted by one.
        var windows = new[] { new[] { 0, 3, 1, 4 }, new[] { 2, 2, 4, 1 } };
        var inputs = windows.Select(w => w[..3]).ToArray();
        var targets = new List<int>();
        for (int t = 0; t < 3; t++)
            foreach (var w in windows) targets.Add(w[t + 1]);

        double error = GradCheck.MaxRelativeError(
            weights => Losses.CrossEntropy(ElmanRnn.FromWeights(config, weights).Forward(inputs).Logits, targets),
            rnn.Parameters().Select(p => p.Value).ToArray());

        Assert.True(error < 1e-6, $"worst relative error {error}");
    }

    [Fact]
    public void FromWeights_RefusesWrongShapes()
    {
        var config = new RnnConfig(5, 4);
        var good = Make(5, 4).Parameters().Select(p => p.Value).ToArray();
        Assert.Throws<ArgumentException>(() => ElmanRnn.FromWeights(config, good.Take(4).ToArray()));
        var bad = (Tensor[])good.Clone();
        bad[1] = Tensor.Zeros(4, 5);
        Assert.Throws<ArgumentException>(() => ElmanRnn.FromWeights(config, bad));
    }

    [Fact]
    public void SameWindows_AsTheFablesModel()
    {
        var fables = Fables.Model;
        // Built exactly as tools/AIAnatomy.Pretrain built the shipped Fables model's trainer.
        string text = TextNormalizer.Normalize(Fables.Text);
        var trainer = new Trainer(fables.Transformer, fables.Tokenizer.Encode(text), new TrainingSettings(Seed: fables.Info.Seed));

        var source = MemoryTraining.FablesSource(fables.Tokenizer, Fables.Text);

        Assert.Equal(32, source.ValidationWindows.Count);
        Assert.Equal(trainer.ValidationWindows.Count, source.ValidationWindows.Count);
        for (int w = 0; w < source.ValidationWindows.Count; w++)
            Assert.Equal(trainer.ValidationWindows[w], source.ValidationWindows[w]);
        Assert.All(source.ValidationWindows, w => Assert.Equal(65, w.Count));
        var rnnTrainer = new RnnTrainer(new ElmanRnn(new RnnConfig(fables.Tokenizer.VocabularySize, 8), new SeededRandom(1)), source, 10, seed: 1);
        Assert.Same(source.ValidationWindows, rnnTrainer.ValidationWindows);
    }

    [Fact]
    public void LossByPosition_BothCurvesHave64Points_AndTheTransformerUsesItsContext()
    {
        var fables = Fables.Model;
        var windows = MemoryTraining.FablesSource(fables.Tokenizer, Fables.Text).ValidationWindows;
        var rnn = new ElmanRnn(new RnnConfig(fables.Tokenizer.VocabularySize, 16), new SeededRandom(1));

        var transformerCurve = ContextLoss.LossByPosition(fables.Transformer, windows);
        var rnnCurve = ContextLoss.LossByPosition(rnn, windows);

        Assert.Equal(64, transformerCurve.Length);
        Assert.Equal(64, rnnCurve.Length);
        Assert.All(transformerCurve.Concat(rnnCurve), v => Assert.True(double.IsFinite(v) && v > 0));
        double late = transformerCurve[32..64].Average();
        Assert.True(late < transformerCurve[0], $"positions 32-63 average {late:F3}, position 0 {transformerCurve[0]:F3}");
    }

    [Fact]
    public void LossByPosition_AveragesTheCrossEntropyAtEachPosition()
    {
        var rnn = Make(5, 3);
        Randomise(rnn, 5);
        var windows = new[] { new[] { 1, 2, 3 }, new[] { 4, 0, 2 } };
        var curve = ContextLoss.LossByPosition(rnn, windows);

        Assert.Equal(2, curve.Length);
        for (int t = 0; t < 2; t++)
        {
            double expected = windows.Average(w => CrossEntropyAt(rnn, w, t));
            Assert.Equal(expected, curve[t], 12);
        }
    }

    private static double CrossEntropyAt(ElmanRnn rnn, int[] window, int t)
    {
        var logits = rnn.Forward(new[] { window[..^1] }).Logits.Row(t);
        double max = logits.Max();
        double logSum = Math.Log(logits.Sum(v => Math.Exp(v - max))) + max;
        return logSum - logits[window[t + 1]];
    }

    [Fact]
    public void LossByPosition_RefusesUnequalOrEmptyWindows()
    {
        var rnn = Make(5, 3);
        Assert.Throws<ArgumentException>(() => ContextLoss.LossByPosition(rnn, Array.Empty<int[]>()));
        Assert.Throws<ArgumentException>(() => ContextLoss.LossByPosition(rnn, new[] { new[] { 1, 2, 3 }, new[] { 1, 2 } }));
        Assert.Throws<ArgumentException>(() => ContextLoss.LossByPosition(rnn, new[] { new[] { 1 } }));
    }

    [Fact]
    public void Sampling_MapsUnknownCharactersToTheUnknownToken()
    {
        var tokenizer = CharTokenizer.FromText("abc de");
        var rnn = new ElmanRnn(new RnnConfig(tokenizer.VocabularySize, 6), new SeededRandom(2));
        Randomise(rnn, 6);

        string a = RnnSampling.Generate(rnn, tokenizer, "abжc", 20, 0.8, new SeededRandom(3));
        string b = RnnSampling.Generate(rnn, tokenizer, "ab§c", 20, 0.8, new SeededRandom(3));
        string known = RnnSampling.Generate(rnn, tokenizer, "abdc", 20, 0.8, new SeededRandom(3));

        Assert.Equal(20, a.Length);
        Assert.Equal(a, b);   // two different unknown characters read as the same token
        Assert.NotEqual(a, known);
    }

    [Fact]
    public void Sampling_RefusesAnEmptyStart()
    {
        var tokenizer = CharTokenizer.FromText("abc");
        var rnn = new ElmanRnn(new RnnConfig(tokenizer.VocabularySize, 4), new SeededRandom(1));
        Assert.Throws<ArgumentException>(() => RnnSampling.Generate(rnn, tokenizer, "", 5, 1.0, new SeededRandom(1)));
        Assert.Throws<ArgumentException>(() => RnnSampling.Generate(rnn, CharTokenizer.FromText("ab"), "a", 5, 1.0, new SeededRandom(1)));
    }

    [Fact]
    public void Sampling_IsRepeatableWithTheSameSeed()
    {
        var tokenizer = CharTokenizer.FromText("the cat sat on the mat");
        var rnn = new ElmanRnn(new RnnConfig(tokenizer.VocabularySize, 8), new SeededRandom(2));
        Randomise(rnn, 7);
        Assert.Equal(
            RnnSampling.Generate(rnn, tokenizer, "the ", 30, 1.0, new SeededRandom(5)),
            RnnSampling.Generate(rnn, tokenizer, "the ", 30, 1.0, new SeededRandom(5)));
        Assert.Equal("", RnnSampling.Generate(rnn, tokenizer, "the ", 0, 1.0, new SeededRandom(5)));
    }

    [Fact]
    public void RecordedResults_MatchTheShippedFablesFile()
    {
        var info = Fables.Model.Info;
        Assert.NotNull(info.ValidationLoss);
        Assert.Equal(RecordedResults.FablesValidationLoss, Math.Round(info.ValidationLoss.Value, 3));
        Assert.Equal(RecordedResults.FablesSteps, info.StepsTrained);
        Assert.Equal(RecordedResults.FablesContextLength, Fables.Model.Transformer.Config.ContextLength);
        Assert.Equal(RecordedResults.FablesBatchSize, new TrainingSettings().BatchSize);
        Assert.Equal(3000.0 * 8 * 64 / 517, RecordedResults.FablesCharactersPerSecond, 9);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var tokenizer = CharTokenizer.FromText("the cat sat on the mat");
        var rnn = new ElmanRnn(new RnnConfig(tokenizer.VocabularySize, 6), new SeededRandom(2));
        Randomise(rnn, 8);
        var info = new ModelInfo("Memory network", "Aesop's Fables (Townsend)", 40, 2.5, 3, true,
            new Dictionary<string, double> { ["seconds"] = 12.5 });

        using var ms = new MemoryStream();
        rnn.Save(ms, tokenizer, info);
        ms.Position = 0;
        var (loaded, loadedTokenizer, loadedInfo) = ElmanRnn.Load(ms);

        Assert.Equal(rnn.Config, loaded.Config);
        Assert.Equal(tokenizer.Vocabulary, loadedTokenizer.Vocabulary);
        Assert.Equal(info, loadedInfo);
        foreach (var (a, b) in rnn.Parameters().Zip(loaded.Parameters()))
        {
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.Value.Data, b.Value.Data);
        }
        var ids = new[] { tokenizer.Encode("the mat").ToArray() };
        Assert.Equal(rnn.Forward(ids).Logits.Data, loaded.Forward(ids).Logits.Data);
    }

    [Fact]
    public void Save_RefusesATokenizerOfAnotherSize()
    {
        var rnn = Make(5, 3);
        using var ms = new MemoryStream();
        var info = new ModelInfo("Memory network", "Test", 0, null, 1, false);
        Assert.Throws<ArgumentException>(() => rnn.Save(ms, CharTokenizer.FromText("abcdefg"), info));
        Assert.Equal(0, ms.Length);
    }

    [Fact]
    public void Load_RefusesAnotherKindOrMismatchedParameters()
    {
        var info = new ModelInfo("Memory network", "Test", 0, null, 1, false);
        var rnn = Make(4, 3);
        var parameters = rnn.Parameters().ToList();

        using (var other = new MemoryStream())
        {
            FamilyModelFile.Save(other, FamilyKinds.Digits, rnn.Config, "abc", info, parameters);
            other.Position = 0;
            var e = Assert.Throws<InvalidDataException>(() => ElmanRnn.Load(other));
            Assert.Contains("an image network", e.Message);
        }
        using (var wrongHidden = new MemoryStream())
        {
            FamilyModelFile.Save(wrongHidden, FamilyKinds.Memory, new RnnConfig(4, 5), "abc", info, parameters);
            wrongHidden.Position = 0;
            Assert.Throws<InvalidDataException>(() => ElmanRnn.Load(wrongHidden));
        }
        using (var wrongVocabulary = new MemoryStream())
        {
            FamilyModelFile.Save(wrongVocabulary, FamilyKinds.Memory, rnn.Config, "abcd", info, parameters);
            wrongVocabulary.Position = 0;
            Assert.Throws<InvalidDataException>(() => ElmanRnn.Load(wrongVocabulary));
        }
        using (var renamed = new MemoryStream())
        {
            var swapped = parameters.Select((p, i) => i == 2 ? new Parameter("hidden.offset", p.Value) : p).ToList();
            FamilyModelFile.Save(renamed, FamilyKinds.Memory, rnn.Config, "abc", info, swapped);
            renamed.Position = 0;
            Assert.Throws<InvalidDataException>(() => ElmanRnn.Load(renamed));
        }
    }

    [Fact]
    public void MemoryTraining_ConfigsUseTheVocabulary()
    {
        Assert.Equal(70, MemoryTraining.Retrain(70).VocabularySize);
        Assert.Equal(70, MemoryTraining.Shipped(70).VocabularySize);
        Assert.True(MemoryTraining.Retrain(70).Hidden <= MemoryTraining.Shipped(70).Hidden);
        Assert.True(MemoryTraining.RetrainSteps >= 100);
        Assert.True(MemoryTraining.ShippedSteps >= MemoryTraining.RetrainSteps);
    }
}

[Collection(TrainingCollection.Name)]
public class ElmanRnnTrainingTests
{
    [Fact]
    public void Trainer_100StepsOnTheFables_LowersValidationLossBelowLnVByHalf()
    {
        var tokenizer = Fables.Model.Tokenizer;
        var source = MemoryTraining.FablesSource(tokenizer, Fables.Text);
        var rnn = new ElmanRnn(new RnnConfig(tokenizer.VocabularySize, 32), new SeededRandom(1));
        var trainer = new RnnTrainer(rnn, source, totalSteps: 100, seed: 1);
        double untrained = Math.Log(tokenizer.VocabularySize);

        double last = double.NaN;
        while (trainer.StepsTaken < trainer.TotalSteps) last = trainer.Step();

        double validation = trainer.ValidationLoss();
        Assert.Equal(100, trainer.StepsTaken);
        Assert.True(double.IsFinite(last));
        Assert.True(validation < untrained - 0.5, $"validation {validation:F3}, ln V {untrained:F3}");
        Assert.Throws<InvalidOperationException>(() => trainer.Step());
    }

    [Fact]
    public void Trainer_IsRepeatableWithTheSameSeed()
    {
        var tokenizer = CharTokenizer.FromText(TestTexts.Fable);
        var tokens = tokenizer.Encode(TestTexts.Fable);
        double Run()
        {
            var source = new CorpusBatchSource(tokens, 16, tokenizer.VocabularySize, 0.1, new SeededRandom(1));
            var trainer = new RnnTrainer(new ElmanRnn(new RnnConfig(tokenizer.VocabularySize, 8), new SeededRandom(1)), source, 5, seed: 2);
            double loss = 0;
            for (int i = 0; i < 5; i++) loss = trainer.Step();
            return loss + trainer.ValidationLoss();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Trainer_RefusesBadSettings()
    {
        var tokenizer = CharTokenizer.FromText(TestTexts.Fable);
        var source = new CorpusBatchSource(tokenizer.Encode(TestTexts.Fable), 16, tokenizer.VocabularySize, 0.1, new SeededRandom(1));
        var rnn = new ElmanRnn(new RnnConfig(tokenizer.VocabularySize, 8), new SeededRandom(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RnnTrainer(rnn, source, 0, seed: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RnnTrainer(rnn, source, 10, seed: 1, batchSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RnnTrainer(rnn, source, 10, seed: 1, learningRate: 0));
        var small = new ElmanRnn(new RnnConfig(3, 8), new SeededRandom(1));
        Assert.Throws<ArgumentException>(() => new RnnTrainer(small, source, 10, seed: 1));
    }
}
