using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Labs;

public class SamplingTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void TemperatureZero_AllSamplesSame()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Temperature = 0 };
        lab.SampleHundredCommand.Execute(null);
        var only = Assert.Single(lab.SampleCounts);
        Assert.Equal(100, only.Count);
        Assert.Equal(1, lab.Probabilities.Count(p => p > 0));
    }

    [Fact]
    public void SampleFrequencies_SumTo100()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels());
        lab.SampleHundredCommand.Execute(null);
        Assert.Equal(100, lab.SampleTotal);
        Assert.Equal(100, lab.SampleCounts.Sum(c => c.Count));
        Assert.All(lab.Frequencies.Where(f => f.Label != SamplingLabViewModel.OthersLabel),
            f => Assert.Equal((lab.SampleCounts.SingleOrDefault(c => c.Token == f.Label)?.Count ?? 0) / 100.0, f.Value, 12));
        Assert.Equal(1.0, lab.Frequencies.Sum(f => f.Value), 9);   // the rows plus "others" cover all 100 draws
    }

    [Fact]
    public void SampleFrequencies_AreSameForSameSeed_AndDifferWithAnother()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Temperature = 2 };
        lab.SampleHundredCommand.Execute(null);
        var first = lab.SampleCounts.ToList();
        lab.SampleHundredCommand.Execute(null);
        Assert.Equal(first, lab.SampleCounts);
        lab.Seed = 2;
        Assert.Empty(lab.SampleCounts);   // old results are cleared when the seed changes
        lab.SampleHundredCommand.Execute(null);
        Assert.NotEqual(first, lab.SampleCounts);
    }

    [Fact]
    public void TopK1_OnlyOneBarNonZero()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { TopK = 1 };
        Assert.Equal(1, lab.Bars.Count(b => b.Value > 0));
        Assert.Equal(1, lab.Probabilities.Count(p => p > 0));
        Assert.Equal(1.0, lab.Probabilities.Sum(), 9);
    }

    [Fact]
    public void Bars_MatchSamplingOfTheModelsLogits()
    {
        var models = _env.NewActiveModels();
        var lab = new SamplingLabViewModel(models) { Temperature = 0.5, TopP = 0.9 };
        var logits = models.Current.Predict(lab.Text).Forward.Logits;
        var expected = Sampling.Probabilities(logits.Row(logits.Shape[0] - 1), new SamplingSettings(0.5, 0, 0.9));
        Assert.Equal(expected, lab.Probabilities);
        Assert.Equal(expected.Max(), lab.Bars[0].Value, 12);
        Assert.Equal(SamplingLabViewModel.ShownCount, lab.Scores.Count);
        Assert.Equal(NumberText.Short(logits.Row(logits.Shape[0] - 1).Max()), lab.Scores[0].ScoreText);
    }

    [Fact]
    public async Task Generate_WritesTwoHundredCharacters_ForTheSeed()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Seed = 7 };
        await lab.GenerateTextCommand.ExecuteAsync(null);
        string first = lab.Generated;
        Assert.Equal(200, LabText.CountScalars(first));
        await lab.GenerateTextCommand.ExecuteAsync(null);
        Assert.Equal(first, lab.Generated);
    }

    [Fact]
    public async Task ChangingSettings_DuringGeneration_DropsTheOldResult()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels());
        var running = lab.GenerateTextCommand.ExecuteAsync(null);
        lab.Temperature = 0.5;
        await running;
        await lab.WorkCompleted;
        Assert.Equal("", lab.Generated);
    }

    [Fact]
    public void EmptyText_ShowsThePrompt_AndNothingElse()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Text = "" };
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Empty(lab.Bars);
        Assert.Empty(lab.Probabilities);
        lab.SampleHundredCommand.Execute(null);
        Assert.Empty(lab.SampleCounts);
    }

    [Fact]
    public void LongText_ShowsTheContextNote()
    {
        var models = _env.NewActiveModels();
        var lab = new SamplingLabViewModel(models) { Text = new string('a', models.Current.Transformer.Config.ContextLength + 5) };
        Assert.Equal(LabText.TruncationNote(models.Current.Transformer.Config.ContextLength), lab.Notice);
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Temperature = 0.2, TopK = 5, TopP = 0.5, Seed = 9, Text = "x" };
        lab.SampleHundredCommand.Execute(null);
        lab.Reset();
        Assert.Equal(1.0, lab.Temperature);
        Assert.Equal(0, lab.TopK);
        Assert.Equal(1.0, lab.TopP);
        Assert.Equal(1, lab.Seed);
        Assert.Equal(LabText.DefaultText, lab.Text);
        Assert.Empty(lab.SampleCounts);
    }

    [Fact]
    public void Wording_SaysWhatTheSpecRequires()
    {
        Assert.Contains("divides the scores before softmax", SamplingLabViewModel.TemperatureNote);
        Assert.Contains("At temperature 0 the model always takes its top choice", SamplingLabViewModel.TemperatureNote);
        Assert.Contains("same question can get different answers", SamplingLabViewModel.SamplingNote);
        Assert.Contains("not that the choice is correct", SamplingLabViewModel.ConfidenceNote);
        Assert.Contains("not what is true", SamplingLabViewModel.ConfidenceNote);
    }

    [Fact]
    public void FrequencyRows_LineUpWithTheProbabilityRows()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels());
        lab.SampleHundredCommand.Execute(null);
        Assert.Equal(lab.Bars.Select(b => b.Label), lab.Frequencies.Select(f => f.Label));
        Assert.Equal(SamplingLabViewModel.OthersLabel, lab.Bars[^1].Label);
        Assert.Equal(1.0, lab.Bars.Sum(b => b.Value), 9);
    }

    [Fact]
    public void BothCharts_ShareOneScale_ThatFitsTheLongestBar()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Temperature = 2 };
        lab.SampleHundredCommand.Execute(null);
        double longest = lab.Bars.Concat(lab.Frequencies).Max(b => b.Value);
        Assert.True(lab.BarScaleMaximum >= longest - 1e-12);
        Assert.Equal(AxisTicks.NiceCeiling(longest), lab.BarScaleMaximum, 12);
        Assert.Contains($"a full bar is {NumberText.Percent(lab.BarScaleMaximum)}", lab.BarScaleCaption);
        lab.Temperature = 0;   // one bar at 100%
        Assert.Equal(1.0, lab.BarScaleMaximum, 12);
    }

    [Fact]
    public void ShownText_ShowsTheTrailingSpaceAsAVisibleMark()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels());
        Assert.Equal("the␣fox␣and␣the␣", lab.ShownText);
        lab.Text = "a\nb";
        Assert.Equal("a↵b", lab.ShownText);
    }

    [Fact]
    public void Wording_FollowsTheAccuracyRuling()
    {
        Assert.Contains("makes the top choice even more likely", SamplingLabViewModel.TemperatureNote);
        Assert.Contains("built from random draws, one character at a time", SamplingLabViewModel.SamplingNote);
    }

    [Fact]
    public void WorksWithTheLettersDemo()
    {
        var models = _env.NewActiveModels();
        var lab = new SamplingLabViewModel(models);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        Assert.Equal(LabText.DefaultLetters, lab.Text);
        Assert.Null(lab.Message);
        lab.SampleHundredCommand.Execute(null);
        Assert.Equal(100, lab.SampleTotal);
    }

    [Fact]
    public void IsRegisteredInTheFactory()
    {
        var factory = new LabFactory(_env.NewActiveModels(), AIAnatomy.Lessons.LessonCatalog.Load(TestEnvironment.Content), _ => Task.CompletedTask, TestEnvironment.Content);
        Assert.IsType<SamplingLabViewModel>(factory.Create(SamplingLabViewModel.Id));
        Assert.IsType<LearningLabViewModel>(factory.Create(LearningLabViewModel.Id));
    }
}
