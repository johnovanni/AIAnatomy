using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Labs;

public class ResidualTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void LensRowsEqualLayersPlusOne()
    {
        var models = _env.NewActiveModels();
        var lab = new ResidualLabViewModel(models);
        int layers = models.Current.Transformer.Config.Layers;
        Assert.Equal(layers + 1, lab.LensRows.Count);
        Assert.Equal(layers + 1, lab.Sizes.Count);
        Assert.Equal("after the embeddings", lab.LensRows[0].Label);
        Assert.Equal("after block 1", lab.LensRows[1].Label);
        Assert.All(lab.LensRows, r => Assert.Equal(5, r.Guesses.Count));
    }

    [Fact]
    public void LastLensRowMatchesModelOutput()
    {
        var models = _env.NewActiveModels();
        var lab = new ResidualLabViewModel(models);
        foreach (int position in new[] { 0, 5, lab.PositionChoices.Count - 1 })
        {
            lab.PositionIndex = position;
            var logits = models.Current.Predict(lab.Text).Forward.Logits;
            var expected = Sampling.Probabilities(logits.Row(position), new SamplingSettings(1.0));
            var top = Enumerable.Range(0, expected.Length).OrderByDescending(i => expected[i]).ThenBy(i => i).Take(5).ToArray();
            var last = lab.LensRows[^1].Guesses;
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(expected[top[i]], last[i].Probability, 9);
                Assert.Equal(LabText.Visible(models.Current.Tokenizer.TokenText(top[i])), last[i].Token);
            }
        }
    }

    [Fact]
    public void Sizes_AreTheLengthOfTheResidualVector()
    {
        var models = _env.NewActiveModels();
        var lab = new ResidualLabViewModel(models);
        var trace = models.Current.Predict(lab.Text).Forward.Trace;
        int p = lab.PositionIndex;
        double expected = Math.Sqrt(trace.Input.Row(p).Sum(v => v * v));
        Assert.Equal(expected, lab.Sizes[0].Value, 9);
        Assert.Contains("square root of the sum of squares", ResidualLabViewModel.SizeNote);
        Assert.Contains("position vector", ResidualLabViewModel.SizeNote);
        Assert.Equal(NumberText.Short(lab.Sizes[0].Value), lab.Sizes[0].ValueText);
        double last = Math.Sqrt(trace.Blocks[^1].ResidualOut.Row(p).Sum(v => v * v));
        Assert.Equal(last, lab.Sizes[^1].Value, 9);
    }

    [Fact]
    public void LayerNormDemo_MeanZero()
    {
        var models = _env.NewActiveModels();
        var lab = new ResidualLabViewModel(models);
        var after = lab.NormAfter!;
        Assert.Equal(lab.NormBefore!.Length, after.Length);
        Assert.Equal(0, after.Average(), 9);
        double spread = Math.Sqrt(after.Sum(v => v * v) / after.Length);
        Assert.Equal(1, spread, 3);
        Assert.Equal(after.Average(), lab.NormAfterMean, 12);
        Assert.Equal(spread, lab.NormAfterSpread, 12);
        Assert.Equal(lab.NormBefore.Average(), lab.NormBeforeMean, 12);
        Assert.NotNull(lab.NormScaled);
        Assert.Contains("0 (to rounding)", lab.NormAfterText);
        Assert.Contains("gamma", ResidualLabViewModel.NormNote);
        Assert.Contains("beta", ResidualLabViewModel.NormNote);
    }

    [Fact]
    public void LayerNormDemo_ScaledVersionIsWhatTheModelUses()
    {
        var models = _env.NewActiveModels();
        var lab = new ResidualLabViewModel(models);
        var trace = models.Current.Predict(lab.Text).Forward.Trace;
        lab.NormStage = 1;   // the layer norm at the start of block 1
        Assert.Equal(trace.Blocks[0].Norm1Out.Row(lab.PositionIndex), lab.NormScaled);
        lab.NormStage = models.Current.Transformer.Config.Layers + 1;   // the final one
        Assert.Equal(trace.FinalNorm.Row(lab.PositionIndex), lab.NormScaled);
    }

    [Fact]
    public void InductionDemo_HasNoFeedForward_AndStillWorks()
    {
        var models = _env.NewActiveModels();
        var lab = new ResidualLabViewModel(models);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        Assert.Equal(models.Current.Transformer.Config.Layers + 1, lab.LensRows.Count);
        Assert.NotNull(lab.NormAfter);
    }

    [Fact]
    public void EmptyText_ShowsPrompt_ResetRestores()
    {
        var lab = new ResidualLabViewModel(_env.NewActiveModels());
        lab.Text = "";
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Empty(lab.LensRows);
        lab.Reset();
        Assert.Null(lab.Message);
        Assert.Equal(LabText.DefaultText, lab.Text);
        Assert.NotEmpty(lab.LensRows);
    }
}
