using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class MultiHeadTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private MultiHeadLabViewModel NewLab() => new(_env.NewActiveModels());

    [Fact]
    public void ShowsOneHeatMapPerHead()
    {
        var models = _env.NewActiveModels();
        var lab = new MultiHeadLabViewModel(models);
        var config = models.Current.Transformer.Config;
        Assert.Equal(config.Heads, lab.Heads.Count);
        int tokens = lab.TokenLabels.Count;
        Assert.Equal(LabText.StartingText(models.Current).Length, tokens);
        foreach (var head in lab.Heads)
        {
            Assert.Equal(tokens, head.Weights!.GetLength(0));
            Assert.Equal(tokens, head.Weights.GetLength(1));
            Assert.True(double.IsNaN(head.Weights[0, tokens - 1]), "future squares are blocked");
            Assert.Equal(config.HeadWidth, head.Output!.Length);
        }
    }

    [Fact]
    public void DisablingHead_ChangesPredictions()
    {
        var lab = NewLab();
        var before = lab.Predictions.Select(p => p.Value).ToArray();
        Assert.Equal(5, lab.Predictions.Count);
        lab.Heads[0].IsEnabled = false;
        lab.Heads[1].IsEnabled = false;
        var after = lab.Predictions.Select(p => p.Value).ToArray();
        Assert.NotEqual(before, after);
        Assert.Equal(before, lab.BasePredictions.Select(p => p.Value).ToArray());   // the comparison stays the untouched model
        Assert.Contains((0, 0), lab.DisabledHeads);
        Assert.Contains("SWITCHED OFF", lab.Heads[0].Title);
        Assert.All(lab.Heads[0].Output!, v => Assert.Equal(0, v));
        lab.Heads[0].IsEnabled = true;
        lab.Heads[1].IsEnabled = true;
        Assert.Equal(before, lab.Predictions.Select(p => p.Value).ToArray());
    }

    [Fact]
    public void ConcatenationWidthEqualsModelWidth()
    {
        var models = _env.NewActiveModels();
        var lab = new MultiHeadLabViewModel(models);
        var config = models.Current.Transformer.Config;
        Assert.Equal(config.ModelWidth, lab.ConcatenatedVector!.Length);
        Assert.Equal(config.ModelWidth, lab.ProjectedVector!.Length);
        Assert.Equal(config.ModelWidth, lab.Heads.Sum(h => h.Output!.Length));
        Assert.Equal(lab.Heads.SelectMany(h => h.Output!).ToArray(), lab.ConcatenatedVector);
    }

    [Fact]
    public void ChangingLayer_KeepsPanelsAndMovesDisabledState()
    {
        var lab = NewLab();
        var panels = lab.Heads;
        lab.Heads[0].IsEnabled = false;
        lab.Layer = 2;
        Assert.Same(panels, lab.Heads);   // same objects: a focused box keeps its focus
        Assert.True(lab.Heads[0].IsEnabled);
        lab.Layer = 1;
        Assert.False(lab.Heads[0].IsEnabled);
    }

    [Fact]
    public void EmptyTextAndResetAndUnknownCharacters()
    {
        var lab = NewLab();
        lab.Heads[0].IsEnabled = false;
        lab.Text = "";
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Empty(lab.Heads);
        lab.Reset();
        Assert.Empty(lab.DisabledHeads);
        Assert.Null(lab.Message);
        Assert.All(lab.Heads, h => Assert.True(h.IsEnabled));
        lab.Text = new string('a', 70) + "\U0001F642";
        Assert.Equal("Only the last 64 characters are used: that is this model's context length.", lab.Notice);
        Assert.Contains("�", lab.TokenLabels[^1]);
    }

    [Fact]
    public void InductionDemo_Works()
    {
        var models = _env.NewActiveModels();
        var lab = new MultiHeadLabViewModel(models);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        Assert.Equal(LabText.DefaultLetters, lab.Text);
        Assert.Equal(models.Current.Transformer.Config.Heads, lab.Heads.Count);
        Assert.Equal(models.Current.Transformer.Config.ModelWidth, lab.ConcatenatedVector!.Length);
    }
}
