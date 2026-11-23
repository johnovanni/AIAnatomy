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

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void ResidualStageEqualsTraceValue_EveryVectorStageMatchesItsTensor(int position)
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models) { PositionIndex = position };
        var trace = models.Current.Predict(lab.Text).Forward.Trace;
        var vectors = new List<(int Stage, double[] Row)>
        {
            (1, trace.TokenEmbeddings.Row(position)), (2, trace.PositionEmbeddings.Row(position)), (3, trace.Input.Row(position)),
        };
        int at = 4;
        foreach (var block in trace.Blocks)
        {
            vectors.Add((at, block.Norm1Out.Row(position)));
            vectors.Add((at + 2, block.AttentionOut.Row(position)));
            vectors.Add((at + 3, block.ResidualMid.Row(position)));
            vectors.Add((at + 4, block.Norm2Out!.Row(position)));
            vectors.Add((at + 6, block.FeedForwardOut!.Row(position)));
            vectors.Add((at + 7, block.ResidualOut.Row(position)));
            at += 8;
        }
        vectors.Add((at, trace.FinalNorm.Row(position)));
        foreach (var (stage, row) in vectors) Assert.Equal(row, lab.Stages[stage].Values);
        Assert.Equal(at + 3, lab.Stages.Count);
    }

    [Fact]
    public void LogitsStage_IsOneLabelledItemPerToken_InVocabularyOrder()
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models);
        var logits = models.Current.Predict(lab.Text).Forward.Trace.Logits.Row(lab.PositionIndex);
        var stage = lab.Stages[^2];
        Assert.Equal("Logits", stage.Title);
        Assert.Null(stage.Values);
        Assert.Equal(logits.Length, stage.Items!.Count);
        for (int i = 0; i < logits.Length; i++)
        {
            Assert.Equal(logits[i], stage.Items[i].Value);
            Assert.Equal(LabText.Visible(models.Current.Tokenizer.TokenText(i)), stage.Items[i].Label);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void ProbabilitiesStageMatchesPredict(int position)
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models);
        if (position >= 0) lab.PositionIndex = position;
        var p = Sampling.Probabilities(models.Current.Predict(lab.Text).Forward.Logits.Row(lab.PositionIndex), new SamplingSettings(1.0));
        var items = lab.Stages[^1].Items!;
        Assert.Equal(10, items.Count);
        var top = Enumerable.Range(0, p.Length).OrderByDescending(i => p[i]).ThenBy(i => i).Take(10).ToArray();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(p[top[i]], items[i].Value, 12);
            Assert.Equal(LabText.Visible(models.Current.Tokenizer.TokenText(top[i])), items[i].Label);
        }
    }

    [Fact]
    public void HeadsStage_ListsOnlyEarlierTokens_UpToFivePerHead()
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models);
        foreach (int p in new[] { 0, 2, lab.PositionChoices.Count - 1 })
        {
            lab.PositionIndex = p;
            var trace = models.Current.Predict(lab.Text).Forward.Trace;
            var items = lab.Stages[5].Items!;
            int heads = models.Current.Transformer.Config.Heads;
            Assert.Equal(heads * Math.Min(5, p + 1), items.Count);
            for (int h = 0; h < heads; h++)
            {
                var row = trace.Blocks[0].Heads[h].Weights.Row(p);
                var mine = items.Where(i => i.Label.StartsWith($"head {h + 1} ")).ToList();
                Assert.Equal(Math.Min(5, p + 1), mine.Count);
                Assert.Equal(Enumerable.Range(0, p + 1).Select(k => row[k]).OrderByDescending(x => x).Take(mine.Count), mine.Select(i => i.Value));
            }
        }
    }

    [Fact]
    public void FeedForwardHiddenStage_IsTheTenLargestByMagnitude_NumberedFromOne()
    {
        var models = _env.NewActiveModels();
        var lab = new StepThroughLabViewModel(models);
        var hidden = models.Current.Predict(lab.Text).Forward.Trace.Blocks[0].FeedForwardHidden!.Row(lab.PositionIndex);
        var items = lab.Stages[4 + 5].Items!;
        var top = Enumerable.Range(0, hidden.Length).OrderByDescending(i => Math.Abs(hidden[i])).ThenBy(i => i).Take(10).ToArray();
        Assert.Equal(10, items.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"neuron {top[i] + 1}", items[i].Label);
            Assert.Equal(hidden[top[i]], items[i].Value);
        }
    }

    [Fact]
    public void PositionEmbeddingDescription_DoesNotClaimZeros()
    {
        var lab = new StepThroughLabViewModel(_env.NewActiveModels());
        Assert.DoesNotContain("zeros", lab.Stages[2].Description);
        Assert.Contains("sine and cosine", lab.Stages[2].Description);
    }

    [Fact]
    public void PositionChoicesAreNotReplacedWhenUnchanged()
    {
        var lab = new StepThroughLabViewModel(_env.NewActiveModels());
        var before = lab.PositionChoices;
        lab.PositionIndex = 1;
        Assert.Same(before, lab.PositionChoices);
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
