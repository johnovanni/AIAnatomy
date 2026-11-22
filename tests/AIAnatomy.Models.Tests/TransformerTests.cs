using AIAnatomy.Core;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Tests;

public class TransformerTests
{
    private static Transformer Make(TransformerConfig config, int seed = 1) => new(config, new SeededRandom(seed));

    private static readonly TransformerConfig Small = new(VocabularySize: 11, ContextLength: 8, ModelWidth: 8, Layers: 2, Heads: 2, FeedForwardWidth: 16);

    /// <summary>Replaces the tiny starting weights so every part visibly matters.</summary>
    private static void Randomise(Transformer model, int seed)
    {
        var rng = new SeededRandom(seed);
        foreach (var p in model.Parameters())
            for (int i = 0; i < p.Value.Size; i++) p.Value.Data[i] = rng.NextGaussian() * 0.5;
    }

    [Fact]
    public void Forward_LogitsShapeIsTByV()
    {
        var result = Make(Small).Forward(new[] { 1, 2, 3, 4, 5 });
        Assert.Equal(new[] { 5, 11 }, result.Logits.Shape);
        Assert.Same(result.Logits, result.Trace.Logits);
        Assert.Equal(2, result.Trace.Blocks.Count);
        Assert.Equal(2, result.Trace.Blocks[0].Heads.Count);
    }

    [Fact]
    public void AttentionWeightRows_SumToOne()
    {
        var model = Make(Small);
        Randomise(model, 3);
        var trace = model.Forward(new[] { 1, 2, 3, 4, 5, 6 }).Trace;
        foreach (var head in trace.Blocks.SelectMany(b => b.Heads))
            for (int r = 0; r < 6; r++)
            {
                Assert.Equal(1.0, head.Weights.Row(r).Sum(), 9);
                for (int c = r + 1; c < 6; c++) Assert.Equal(0.0, head.Weights[r, c]);
            }
    }

    [Fact]
    public void CausalMask_ChangingLaterTokenDoesNotChangeEarlierLogits()
    {
        var model = Make(Small);
        Randomise(model, 4);
        var a = model.Forward(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }).Logits;
        var b = model.Forward(new[] { 1, 2, 3, 4, 5, 9, 7, 8 }).Logits;
        for (int r = 0; r < 5; r++) Assert.Equal(a.Row(r), b.Row(r));
        Assert.NotEqual(a.Row(5), b.Row(5));
    }

    [Fact]
    public void GradCheck_TinyModel()
    {
        var model = Make(new TransformerConfig(5, ContextLength: 3, ModelWidth: 4, Layers: 1, Heads: 2, FeedForwardWidth: 8));
        Randomise(model, 5);
        var ids = new[] { 0, 3, 1 };
        var targets = new[] { 3, 1, 4 };
        Tensor Loss() => Losses.CrossEntropy(model.Forward(ids).Logits, targets);

        Loss().Backward();
        var parameters = model.Parameters().ToList();
        var rng = new SeededRandom(9);
        double worst = 0;
        for (int n = 0; n < 10; n++)
        {
            var p = parameters[rng.NextInt(parameters.Count)].Value;
            int i = rng.NextInt(p.Size);
            double analytic = p.Grad?[i] ?? 0, original = p.Data[i];
            p.Data[i] = original + 1e-5; double up = Loss().Item();
            p.Data[i] = original - 1e-5; double down = Loss().Item();
            p.Data[i] = original;
            double numeric = (up - down) / 2e-5;
            worst = Math.Max(worst, Math.Abs(analytic - numeric) / Math.Max(1, Math.Max(Math.Abs(analytic), Math.Abs(numeric))));
        }
        Assert.True(worst < 1e-5, $"worst relative error {worst}");
    }

    [Fact]
    public void DisabledHead_OutputIsZero()
    {
        var model = Make(Small);
        Randomise(model, 6);
        var ids = new[] { 1, 2, 3, 4 };
        var normal = model.Forward(ids);
        var off = model.Forward(ids, new ForwardOptions { DisabledHeads = new HashSet<(int, int)> { (1, 0) } });
        Assert.All(off.Trace.Blocks[1].Heads[0].Output.Data, v => Assert.Equal(0.0, v));
        Assert.Contains(off.Trace.Blocks[1].Heads[1].Output.Data, v => v != 0.0);
        Assert.Contains(off.Trace.Blocks[0].Heads[0].Output.Data, v => v != 0.0);
        Assert.NotEqual(normal.Logits.Data, off.Logits.Data);
        Assert.Empty(ForwardOptions.None.DisabledHeads);
    }

    [Fact]
    public void RemovePositions_MakesPermutedInputGiveSameLastRowUpToPermutation()
    {
        // One layer only: in deeper models the causal mask makes positions 0 and 1 see different things, so the
        // swapped tokens' later representations are no longer a simple swap.
        var model = Make(Small with { Layers = 1 });
        Randomise(model, 7);
        var options = new ForwardOptions { RemovePositions = true };
        var a = model.Forward(new[] { 4, 7, 2, 9 }, options);
        var b = model.Forward(new[] { 7, 4, 2, 9 }, options);
        for (int j = 0; j < 11; j++) Assert.Equal(a.Logits[3, j], b.Logits[3, j], 9);
        Assert.All(a.Trace.PositionEmbeddings.Data, v => Assert.Equal(0.0, v));
        Assert.Equal(a.Trace.TokenEmbeddings.Data, a.Trace.Input.Data);

        // With positions the same swap does change the last row.
        var c = model.Forward(new[] { 4, 7, 2, 9 });
        var d = model.Forward(new[] { 7, 4, 2, 9 });
        Assert.NotEqual(c.Logits.Row(3), d.Logits.Row(3));
    }

    [Theory]
    [MemberData(nameof(Configs))]
    public void CountParameters_MatchesActualParameters(TransformerConfig config)
    {
        var model = Make(config);
        Assert.Equal(config.CountParameters(), model.Parameters().Sum(p => (long)p.Value.Size));
        Assert.Equal(config.CountParameters(), model.ParameterCountsByPart().Values.Sum());
        Assert.Equal(model.Parameters().Count(), model.Parameters().Select(p => p.Name).Distinct().Count());
    }

    public static IEnumerable<object[]> Configs()
    {
        yield return new object[] { new TransformerConfig(30) };
        yield return new object[] { new TransformerConfig(7, 5, 6, 1, 3, 12, PositionKind.Sinusoidal, false) };
        yield return new object[] { new TransformerConfig(13, 10, 8, 3, 2, 5, PositionKind.Learned, true) };
    }

    [Fact]
    public void NoFeedForward_HasNoFeedForwardOrSecondNorm()
    {
        var model = Make(new TransformerConfig(7, 5, 6, 1, 3, 12, PositionKind.Sinusoidal, false));
        var block = model.Forward(new[] { 1, 2 }).Trace.Blocks[0];
        Assert.Null(block.Norm2Out);
        Assert.Null(block.FeedForwardHidden);
        Assert.Null(block.FeedForwardOut);
        Assert.Same(block.ResidualMid, block.ResidualOut);
        var counts = model.ParameterCountsByPart();
        Assert.Equal(0, counts["Positions"]);
        Assert.Equal(0, counts["Feed-forward"]);
    }

    [Fact]
    public void Forward_TooManyTokens_Throws()
    {
        var model = Make(Small);
        Assert.Throws<ArgumentException>(() => model.Forward(new int[9]));
        Assert.Throws<ArgumentException>(() => model.Forward(Array.Empty<int>()));
        Assert.Throws<ArgumentException>(() => Make(new TransformerConfig(5, ModelWidth: 10, Heads: 3)));
    }

    [Fact]
    public void Sinusoidal_KnownValues()
    {
        var pe = new SinusoidalPositions(4).Forward(3);
        Assert.Equal(Math.Sin(1.0), pe[1, 0], 12);
        Assert.Equal(Math.Cos(1.0), pe[1, 1], 12);
        Assert.Equal(Math.Sin(2.0 / 100.0), pe[2, 2], 12); // 10000^(2/4) = 100
        Assert.Equal(0.0, pe[0, 0]);
        Assert.Equal(1.0, pe[0, 1]);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var model = Make(Small);
        var copy = model.Clone();
        var ids = new[] { 1, 2, 3 };
        Assert.Equal(model.Forward(ids).Logits.Data, copy.Forward(ids).Logits.Data);
        copy.OutputWeights.Value.Data[0] += 1.0;
        Assert.NotEqual(model.OutputWeights.Value.Data[0], copy.OutputWeights.Value.Data[0]);
        Assert.NotEqual(model.Forward(ids).Logits.Data, copy.Forward(ids).Logits.Data);
    }

    [Fact]
    public void ParameterNames_AreHierarchicalAndOrdered()
    {
        var names = Make(Small).Parameters().Select(p => p.Name).ToList();
        Assert.Equal("token_embedding", names[0]);
        Assert.Equal("position_embedding", names[1]);
        Assert.Contains("blocks.0.attn.heads.1.query", names);
        Assert.Contains("blocks.1.ln2.gamma", names);
        Assert.Equal("output.bias", names[^1]);
    }
}
