using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class PositionTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void RemovingPositions_ChangesPredictions()
    {
        var models = _env.NewActiveModels();
        var lab = new PositionLabViewModel(models);

        Assert.Equal(5, lab.WithPositions.Count);
        Assert.Equal(5, lab.WithoutPositions.Count);
        Assert.NotEqual(
            lab.WithPositions.Select(i => i.Value).ToList(),
            lab.WithoutPositions.Select(i => i.Value).ToList());
        Assert.Contains("Top choice with positions", lab.Comparison);
    }

    [Fact]
    public void SinusoidalHeatMap_HasContextByWidthCells()
    {
        var models = _env.NewActiveModels();
        var lab = new PositionLabViewModel(models);
        var config = models.Current.Transformer.Config;

        Assert.Equal(config.ContextLength, lab.SinusoidalPositions!.GetLength(0));
        Assert.Equal(config.ModelWidth, lab.SinusoidalPositions.GetLength(1));
        Assert.Equal(config.ContextLength, lab.ModelPositions!.GetLength(0));
        Assert.Equal(config.ModelWidth, lab.ModelPositions.GetLength(1));
        Assert.Equal(0.0, lab.SinusoidalPositions[0, 0]);   // sin(0)
        Assert.Equal(1.0, lab.SinusoidalPositions[0, 1]);   // cos(0)
        Assert.Equal(config.ContextLength, lab.PositionLabels.Count);
    }

    [Fact]
    public void WordingDoesNotClaimTheModelIgnoresOrder()
    {
        Assert.DoesNotContain("ignores word order", PositionLabViewModel.OrderNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("causal mask", PositionLabViewModel.OrderNote);
        Assert.Contains("direct signal of order", PositionLabViewModel.OrderNote);
    }

    [Fact]
    public void EmptyText_ShowsPrompt_NotAnError()
    {
        var lab = new PositionLabViewModel(_env.NewActiveModels()) { Text = "" };
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Empty(lab.WithPositions);
        Assert.NotNull(lab.SinusoidalPositions);   // the heat maps do not depend on the text
    }

    [Fact]
    public void LongText_ShowsTruncationNote_AndUnknownCharactersDoNotThrow()
    {
        var lab = new PositionLabViewModel(_env.NewActiveModels());
        lab.Text = new string('a', 70) + "\U0001F642";
        Assert.Equal("Only the last 64 characters are used: that is this model's context length.", lab.Notice);
        Assert.Equal(5, lab.WithPositions.Count);
    }

    [Fact]
    public void InductionDemo_Works()
    {
        var models = _env.NewActiveModels();
        var lab = new PositionLabViewModel(models);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        lab.Text = "ABCDABC";
        Assert.Equal(5, lab.WithoutPositions.Count);
        Assert.Equal(models.Current.Transformer.Config.ContextLength, lab.SinusoidalPositions!.GetLength(0));
    }
}
