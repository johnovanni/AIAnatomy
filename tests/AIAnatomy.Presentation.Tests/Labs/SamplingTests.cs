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
        double longest = lab.Bars.Concat(lab.Frequencies).Where(b => b.Label != SamplingLabViewModel.OthersLabel).Max(b => b.Value);
        Assert.True(lab.BarScaleMaximum >= longest - 1e-12);
        Assert.Equal(AxisTicks.NiceCeiling(longest), lab.BarScaleMaximum, 12);
        Assert.Contains($"a full bar is {NumberText.Percent(lab.BarScaleMaximum)}", lab.BarScaleCaption);
        lab.Temperature = 0;   // one bar at 100%
        Assert.Equal(1.0, lab.BarScaleMaximum, 12);
    }

    [Fact]
    public void Scale_IgnoresTheOthersBar()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Temperature = 2 };
        var others = lab.Bars.Single(b => b.Label == SamplingLabViewModel.OthersLabel).Value;
        var listed = lab.Bars.Where(b => b.Label != SamplingLabViewModel.OthersLabel).Max(b => b.Value);
        Assert.True(others > listed);   // at temperature 2 the rest outweighs any one character
        Assert.Equal(AxisTicks.NiceCeiling(listed), lab.BarScaleMaximum, 12);
        Assert.Contains("cut off at full width", lab.BarScaleCaption);
    }

    [Theory]
    [InlineData(0.3, 0)]   // "others" is small, well inside the scale
    [InlineData(1.0, 15)]  // top-k 15: "others" is exactly 0
    public void CutOffSentence_OnlyWhenTheOthersBarIsReallyCut(double temperature, int topK)
    {
        // Final review I5: the caption says the "others" bar is cut off only when it is longer than a full bar.
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Temperature = temperature, TopK = topK };
        lab.SampleHundredCommand.Execute(null);
        var others = lab.Bars.Concat(lab.Frequencies).Where(b => b.Label == SamplingLabViewModel.OthersLabel).Max(b => b.Value);
        Assert.True(others <= lab.BarScaleMaximum);
        Assert.Contains("a full bar is", lab.BarScaleCaption);
        Assert.DoesNotContain("cut off", lab.BarScaleCaption);

        lab.TopK = 0;
        lab.Temperature = 2;   // now the rest outweighs the scale
        Assert.True(lab.Bars.Single(b => b.Label == SamplingLabViewModel.OthersLabel).Value > lab.BarScaleMaximum);
        Assert.Contains("cut off at full width", lab.BarScaleCaption);
        // Residual batch 3c: the caption names only the break, which is always drawn (the "cut off" tag needs room).
        Assert.Contains("drawn with a break near its end", lab.BarScaleCaption);
        Assert.DoesNotContain("marked", lab.BarScaleCaption);
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
        var factory = new LabFactory(_env.NewActiveModels(), TestEnvironment.Content);
        Assert.IsType<SamplingLabViewModel>(factory.Create(SamplingLabViewModel.Id));
        Assert.IsType<LearningLabViewModel>(factory.Create(LearningLabViewModel.Id));
    }

    // ---- screens (UI rework, Task 15) ----

    private static List<string> Listen(SamplingLabViewModel lab)
    {
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        return raised;
    }

    [Fact]
    public void TheProbabilitiesLightAfterATextChange_NotWhenTheSameTextIsSetAgain()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels());
        var raised = Listen(lab);
        Assert.Empty(raised);   // opening is the baseline
        lab.Text = "The crow sat on the ";
        Assert.Contains("probabilities", raised);
        Assert.Contains("scores", raised);
        raised.Clear();
        lab.Text = "The crow sat on the ";
        Assert.Empty(raised);
        lab.Temperature = 0.5;
        Assert.Contains("probabilities", raised);
    }

    [Fact]
    public void Samples_LightOnlyWhenTheDrawsDiffer_AndAChangedSeedClearsThem()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels()) { Temperature = 2 };
        var raised = Listen(lab);
        lab.SampleHundredCommand.Execute(null);
        Assert.Equal(1, raised.Count(s => s == "samples"));
        raised.Clear();
        lab.SampleHundredCommand.Execute(null);   // the same seed gives the same draws
        Assert.DoesNotContain("samples", raised);
        raised.Clear();
        lab.Seed = 2;   // the old draws are cleared
        Assert.Contains("samples", raised);
        raised.Clear();
        lab.SampleHundredCommand.Execute(null);
        Assert.Contains("samples", raised);
    }

    [Fact]
    public async Task TheWriting_LightsOnceWhenItArrives_AndNotAfterAReset()
    {
        var lab = new SamplingLabViewModel(_env.NewActiveModels());
        var raised = Listen(lab);
        await lab.GenerateTextCommand.ExecuteAsync(null);
        Assert.Equal(1, raised.Count(s => s == "writing"));
        raised.Clear();
        await lab.GenerateTextCommand.ExecuteAsync(null);   // same text, seed and settings: the same writing
        Assert.DoesNotContain("writing", raised);

        var pending = lab.GenerateTextCommand.ExecuteAsync(null);
        lab.Reset();   // the answer that is still on its way belongs to a lab that was reset
        raised.Clear();
        await pending;
        await lab.WorkCompleted;
        Assert.DoesNotContain("writing", raised);
    }

    [Fact]
    public void ResetAndModelSwitch_LightTheScreensThatChanged()
    {
        var models = _env.NewActiveModels();
        var lab = new SamplingLabViewModel(models);
        var raised = Listen(lab);
        lab.Text = "x";
        lab.Temperature = 0.5;
        raised.Clear();
        lab.Reset();
        Assert.Contains("probabilities", raised);
        raised.Clear();
        lab.Reset();   // nothing left to change
        Assert.Empty(raised);

        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        Assert.Contains("probabilities", raised);
    }
}
