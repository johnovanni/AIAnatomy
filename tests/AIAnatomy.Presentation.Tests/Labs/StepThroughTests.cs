using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Labs;

public class StepThroughTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void StageCount_Is4Plus8PerBlockPlus3()
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models);
        var config = models.Current.Transformer.Config;
        Assert.True(config.UseFeedForward);
        Assert.Equal(4 + 8 * config.Layers + 3, lab.Stages.Count);
        Assert.Equal(lab.Stages.Count, lab.StageCount);
    }

    [Fact]
    public void AttentionOnlyModel_HasFourStagesPerBlock()
    {
        var models = _env.NewActiveModels();
        var demo = models.Library.List().Single(e => e.Name == "Induction demo");
        models.SetActive(demo);
        var lab = new StepThroughLabViewModel(models);
        var config = models.Current.Transformer.Config;
        Assert.False(config.UseFeedForward);
        Assert.Equal(4 + 4 * config.Layers + 3, lab.Stages.Count);
    }

    [Fact]
    public void ResidualStageEqualsTraceValue()
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models);
        lab.PositionIndex = 3;
        var trace = models.Current.Predict(lab.Text).Forward.Trace;
        Assert.Equal(trace.Input.Row(3), lab.Stages[3].Values);
        Assert.Equal(trace.Blocks[0].ResidualMid.Row(3), lab.Stages[4 + 3].Values);
        Assert.Equal(trace.Blocks[0].ResidualOut.Row(3), lab.Stages[4 + 7].Values);
        Assert.Equal(trace.Blocks[1].ResidualOut.Row(3), lab.Stages[4 + 8 + 7].Values);
        Assert.Equal(trace.FinalNorm.Row(3), lab.Stages[^3].Values);
        Assert.Equal(trace.Logits.Row(3), lab.Stages[^2].Values);
    }

    [Fact]
    public void ProbabilitiesStageMatchesPredict()
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models);
        var p = Sampling.Probabilities(models.Current.Predict(lab.Text).Forward.Logits.Row(lab.PositionIndex), new SamplingSettings(1.0));
        var items = lab.Stages[^1].Items!;
        Assert.Equal(10, items.Count);
        var top = Enumerable.Range(0, p.Length).OrderByDescending(i => p[i]).ThenBy(i => i).Take(10).ToArray();
        for (int i = 0; i < 10; i++) Assert.Equal(p[top[i]], items[i].Value, 12);
    }

    [Fact]
    public void NextAndPrevious_StepAndStopAtTheEnds()
    {
        var lab = new StepThroughLabViewModel(_env.NewActiveModels());
        Assert.Equal(0, lab.StageIndex);
        Assert.False(lab.PreviousCommand.CanExecute(null));
        lab.NextCommand.Execute(null);
        Assert.Equal(1, lab.StageIndex);
        for (int i = 0; i < 200; i++) lab.NextCommand.Execute(null);
        Assert.Equal(lab.Stages.Count - 1, lab.StageIndex);
        Assert.False(lab.NextCommand.CanExecute(null));
        lab.PreviousCommand.Execute(null);
        Assert.Equal(lab.Stages.Count - 2, lab.StageIndex);
        lab.Reset();
        Assert.Equal(0, lab.StageIndex);
    }

    [Fact]
    public void PositionsAreNumberedFromOne_AndEmptyTextShowsPrompt()
    {
        var lab = new StepThroughLabViewModel(_env.NewActiveModels());
        Assert.StartsWith("1:", lab.PositionChoices[0]);
        Assert.Equal(lab.PositionChoices.Count - 1, lab.PositionIndex);
        lab.Text = "";
        Assert.Equal(LabText.EmptyPrompt, lab.Message);
        Assert.Empty(lab.Stages);
    }

    [Fact]
    public void EveryStageHasADescription()
    {
        var lab = new StepThroughLabViewModel(_env.NewActiveModels());
        Assert.All(lab.Stages, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
        Assert.All(lab.Stages, s => Assert.True(s.Values != null || s.Items is { Count: > 0 }));
    }
}
