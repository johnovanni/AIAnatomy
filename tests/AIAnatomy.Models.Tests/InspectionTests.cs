using AIAnatomy.Core;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Transformers;
using Xunit.Abstractions;

namespace AIAnatomy.Models.Tests;

[Collection(TrainingCollection.Name)]
public class InspectionTests
{
    private readonly ITestOutputHelper _output;
    public InspectionTests(ITestOutputHelper output) { _output = output; }

    private static LanguageModel TextModel(bool feedForward = true, int layers = 2)
    {
        var tokenizer = CharTokenizer.FromText("the cat sat on the mat. ");
        var config = new TransformerConfig(tokenizer.VocabularySize, ContextLength: 8, ModelWidth: 16, Layers: layers, Heads: 2,
            FeedForwardWidth: 32, UseFeedForward: feedForward);
        return new LanguageModel(new Transformer(config, new SeededRandom(3)), tokenizer, new ModelInfo("t", "t", 0, null, 3, false));
    }

    [Fact]
    public void Pca_PointsOnALine_ExplainNearlyAll()
    {
        var rows = Enumerable.Range(0, 20).Select(i => new[] { i * 1.0, i * 2.0 + 1, -i * 0.5, i * 3.0 }).ToArray();
        var points = Pca.Project2D(rows, out double explained);
        Assert.True(explained > 0.999, $"explained {explained}");
        Assert.All(points, p => Assert.True(Math.Abs(p.Y) < 1e-9));
        Assert.True(points[19].X > points[0].X);   // the direction's largest-magnitude entry is positive
    }

    [Fact]
    public void Pca_SeparatesTwoDirections()
    {
        var rows = Enumerable.Range(0, 40).Select(i => new[] { 10.0 * Math.Sin(i), 3.0 * Math.Cos(2.3 * i), 0, 0, 0 }).ToArray();
        var points = Pca.Project2D(rows, out double explained);
        Assert.True(explained > 0.999, $"explained {explained}");
        Assert.True(points.Max(p => Math.Abs(p.X)) > 5 && points.Max(p => Math.Abs(p.Y)) > 1);
    }

    [Fact]
    public void Pca_IsDeterministic()
    {
        var rng = new SeededRandom(5);
        var rows = Enumerable.Range(0, 30).Select(_ => Enumerable.Range(0, 6).Select(__ => rng.NextGaussian()).ToArray()).ToArray();
        var a = Pca.Project2D(rows, out double fa);
        var b = Pca.Project2D(rows, out double fb);
        Assert.Equal(a, b);
        Assert.Equal(fa, fb);
        Assert.InRange(fa, 0, 1);
    }

    [Fact]
    public void Pca_IdenticalRows_ExplainNothing()
    {
        var points = Pca.Project2D(new[] { new[] { 1.0, 2.0 }, new[] { 1.0, 2.0 } }, out double explained);
        Assert.Equal(0, explained);
        Assert.All(points, p => Assert.Equal((0.0, 0.0), p));
    }

    [Fact]
    public void LogitLens_LastEntryEqualsModelOutput()
    {
        var lm = TextModel();
        var result = lm.Transformer.Forward(lm.Tokenizer.Encode("the cat "));
        var expected = Ops.SoftmaxRows(result.Logits);
        for (int position = 0; position < 8; position++)
        {
            var lens = LogitLens.PerLayerProbabilities(lm.Transformer, result.Trace, position);
            var row = expected.Row(position);
            for (int v = 0; v < row.Length; v++) Assert.InRange(Math.Abs(row[v] - lens[^1][v]), 0, 1e-12);
            Assert.All(lens, p => Assert.InRange(Math.Abs(p.Sum() - 1.0), 0, 1e-9));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void LogitLens_HasLayersPlusOneEntries(int layers)
    {
        var lm = TextModel(layers: layers);
        var result = lm.Transformer.Forward(lm.Tokenizer.Encode("the cat"));
        var lens = LogitLens.PerLayerProbabilities(lm.Transformer, result.Trace, 2);
        Assert.Equal(layers + 1, lens.Count);
        Assert.All(lens, p => Assert.Equal(lm.Transformer.Config.VocabularySize, p.Length));
    }

    [Fact]
    public void LogitLens_EarlyEntryDiffersFromLast()
    {
        var lm = TextModel();
        var result = lm.Transformer.Forward(lm.Tokenizer.Encode("the cat"));
        var lens = LogitLens.PerLayerProbabilities(lm.Transformer, result.Trace, 2);
        Assert.NotEqual(lens[0], lens[^1]);
    }

    [Fact]
    public void NeuronProbe_ReturnsSortedDescending()
    {
        var lm = TextModel();
        const string text = "the cat sat on the mat. the cat";
        var top = NeuronProbe.TopContexts(lm, text, layer: 1, neuron: 5, count: 6);
        Assert.Equal(6, top.Count);
        for (int i = 1; i < top.Count; i++) Assert.True(top[i - 1].Value >= top[i].Value);
        Assert.All(top, a =>
        {
            Assert.InRange(a.Position, 0, text.Length - 1);
            Assert.True(a.Context.Length <= 12);
            Assert.Equal(text.Substring(Math.Max(0, a.Position - 11), Math.Min(12, a.Position + 1)), a.Context);
        });
        // The best value really is the largest over all positions.
        var all = NeuronProbe.TopContexts(lm, text, 1, 5, 1000);
        Assert.Equal(text.Length, all.Count);
        Assert.Equal(all[0].Value, top[0].Value);
    }

    [Fact]
    public void NeuronProbe_ModelWithoutFeedForward_Throws()
        => Assert.Throws<ArgumentException>(() => NeuronProbe.TopContexts(TextModel(feedForward: false), "the cat", 0, 0, 3));

    [Fact]
    public void Induction_MakeSequence_RepeatsASegmentWithVaryingGap()
    {
        var rng = new SeededRandom(1);
        var lengths = new HashSet<int>();
        var starts = new HashSet<int>();
        for (int n = 0; n < 300; n++)
        {
            var s = InductionExperiment.MakeSequence(rng);
            Assert.Equal(17, s.Tokens.Count);
            Assert.InRange(s.Length, 4, 8);
            Assert.Equal(s.FirstStart + s.Length, s.SecondStart);
            Assert.True(s.SecondStart + s.Length <= 17);
            var first = s.Tokens.Skip(s.FirstStart).Take(s.Length).ToArray();
            Assert.Equal(first, s.Tokens.Skip(s.SecondStart).Take(s.Length));
            Assert.Equal(s.Length, first.Distinct().Count());
            Assert.All(s.Tokens, t => Assert.InRange(t, 1, 15));
            // Prefix and filler never contain a letter of the segment.
            for (int i = 0; i < 17; i++)
                if (i < s.FirstStart || i >= s.SecondStart + s.Length) Assert.DoesNotContain(s.Tokens[i], first);
            lengths.Add(s.Length);
            starts.Add(s.FirstStart);
        }
        Assert.Equal(5, lengths.Count);
        Assert.True(starts.Count > 5);
        Assert.Equal("AB", InductionExperiment.Tokenizer.Decode(new[] { 1, 2 }));
        Assert.Equal(16, InductionExperiment.Tokenizer.VocabularySize);
    }

    [Fact]
    public void Induction_UntrainedScoreIsLow()
    {
        var model = new Transformer(InductionExperiment.Config, new SeededRandom(1));
        double best = 0;
        for (int layer = 0; layer < 2; layer++)
            for (int head = 0; head < InductionExperiment.Config.Heads; head++)
                best = Math.Max(best, InductionExperiment.InductionScore(model, layer, head, seed: 9));
        Assert.True(best < 0.3, $"best untrained score {best}");
    }

#if DEBUG
    [Fact(Skip = "Slow: run with dotnet test -c Release --filter Category=Slow")]
#else
    [Fact]
#endif
    [Trait("Category", "Slow")]
    public async Task Induction_TrainedModelHasInductionHead()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var losses = new List<string>();
        var progress = new Progress<TrainingStepReport>(r => { if (r.Step % 250 == 0) lock (losses) losses.Add($"{r.Step}:{r.TrainLoss:F3}"); });
        var model = await InductionExperiment.TrainAsync(InductionExperiment.DefaultSteps, 1, progress, CancellationToken.None);
        watch.Stop();
        _output.WriteLine($"trained in {watch.Elapsed.TotalSeconds:F1} s; loss {string.Join(" ", losses)}");
        double best = 0;
        for (int layer = 0; layer < 2; layer++)
            for (int head = 0; head < InductionExperiment.Config.Heads; head++)
            {
                double induction = InductionExperiment.InductionScore(model, layer, head, seed: 9);
                double previous = InductionExperiment.PreviousTokenScore(model, layer, head, seed: 9);
                _output.WriteLine($"layer {layer + 1} head {head + 1}: induction {induction:F3}, previous-token {previous:F3}");
                if (layer == 1) best = Math.Max(best, induction);
            }
        Assert.True(best > 0.5, $"best layer-2 induction score {best}");
        Assert.True(watch.Elapsed.TotalSeconds < 120, $"took {watch.Elapsed.TotalSeconds:F1} s");
    }
}
