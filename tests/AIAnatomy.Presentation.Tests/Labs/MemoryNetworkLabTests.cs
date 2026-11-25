using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>Lesson 4.8's lab. Runs of "train it again" are a few steps long (the test hook), but they train, so the class
/// runs apart from the others.</summary>
[Collection(TrainingCollection.Name)]
public sealed class MemoryNetworkLabTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private static string Fables() => File.ReadAllText(Path.Combine(TestEnvironment.Content, "texts", "aesop-fables.txt"));

    /// <summary>A lab with its networks loaded and its measurement done. With no SynchronizationContext (xUnit gives each
    /// test one that queues posts) every update is applied where it is made, so nothing needs pumping.</summary>
    private async Task<MemoryNetworkLabViewModel> NewLab(int retrainSteps = 3)
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var lab = new MemoryNetworkLabViewModel(new FamilyModels(TestEnvironment.Content), _env.NewActiveModels(), Fables)
        {
            RetrainSteps = retrainSteps,
        };
        lab.Runner.UpdateInterval = TimeSpan.Zero;
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        return lab;
    }

    private static LanguageModel ShippedFables(ActiveModelService models)
    {
        var entry = models.Library.List().Single(e => e.IsShipped && e.Name == ActiveModelService.DefaultModelName);
        return models.Library.Load(entry);
    }

    private static async Task<(ElmanRnn Rnn, CharTokenizer Tokenizer, ModelInfo Info)> ShippedMemory() =>
        await new FamilyModels(TestEnvironment.Content).MemoryAsync();

    private static IReadOnlyList<IReadOnlyList<int>> SharedWindows(LanguageModel fables) =>
        MemoryTraining.FablesSource(fables.Tokenizer, Fables()).ValidationWindows;

    private static double[] Row(Tensor t, int row)
    {
        int h = t.Shape[1];
        return t.Data.Skip(row * h).Take(h).ToArray();
    }

    // ---- opening ----

    [Fact]
    public async Task Opens_WithTheRecordedNumbers_LabelledAsRecorded()
    {
        var lab = await NewLab();
        var (_, _, info) = await ShippedMemory();
        Assert.Equal("MemoryNetworkLab", MemoryNetworkLabViewModel.Id);
        Assert.Equal(MemoryNetworkLabViewModel.Id, lab.LabId);

        Assert.Equal(RecordedResults.FablesValidationLoss, lab.TransformerRecordedLoss);
        Assert.Equal(RecordedResults.FablesCharactersPerSecond, lab.TransformerRecordedSpeed);
        Assert.Equal(info.Results!["validationLoss"], lab.RnnRecordedLoss);
        Assert.Equal(info.Results["charactersPerSecond"], lab.RnnRecordedSpeed);
        Assert.Null(lab.RnnLiveLoss);
        Assert.Null(lab.RnnLiveSpeed);

        Assert.Equal(2, lab.Comparison.Count);
        var transformer = lab.Comparison[0];
        Assert.Contains("transformer", transformer.Network);
        Assert.Equal(MemoryNetworkLabViewModel.RecordedKind, transformer.Kind);
        Assert.Equal("1.711", transformer.Loss);
        Assert.Equal("2,971", transformer.Speed);
        var memory = lab.Comparison[1];
        Assert.Contains("Memory network", memory.Network);
        Assert.Equal(MemoryNetworkLabViewModel.RecordedKind, memory.Kind);
        Assert.Equal("1.678", memory.Loss);
        Assert.Equal("5,309", memory.Speed);
        Assert.Contains("about the same", MemoryNetworkLabViewModel.LossNote);
    }

    [Fact]
    public async Task Opens_ReportingEveryScreen()
    {
        var lab = await NewLab();
        Assert.Equal(new[] { "compare", "memory", "hidden", "samples", "training" }.Order(), lab.ReportedScreens.Order());
    }

    // ---- how far back it remembers ----

    [Fact]
    public async Task MemoryCurves_AreContextLoss_OnTheSharedValidationWindows()
    {
        var lab = await NewLab();
        var fables = ShippedFables(_env.NewActiveModels());
        var (rnn, _, _) = await ShippedMemory();
        var windows = SharedWindows(fables);
        Assert.Equal(32, windows.Count);

        Assert.Equal(ContextLoss.LossByPosition(fables.Transformer, windows), lab.TransformerCurve);
        Assert.Equal(ContextLoss.LossByPosition(rnn, windows), lab.RnnCurve);

        Assert.Equal(2, lab.MemorySeries.Count);
        Assert.Equal(lab.TransformerCurve, lab.MemorySeries[0].Points.Select(p => p.Y));
        Assert.Equal(lab.RnnCurve, lab.MemorySeries[1].Points.Select(p => p.Y));
        // x is the number of characters read: position t predicts character t+1 having read t+1 characters.
        Assert.Equal(Enumerable.Range(1, 64).Select(i => (double)i), lab.MemorySeries[0].Points.Select(p => p.X));
    }

    [Fact]
    public async Task MemoryBands_AverageTheCurvesOverRangesOfCharactersRead()
    {
        var lab = await NewLab();
        Assert.Equal(new[] { "1", "2 to 4", "5 to 8", "9 to 16", "17 to 32", "33 to 64" }, lab.MemoryBands.Select(b => b.CharactersRead));
        var last = lab.MemoryBands[^1];
        Assert.Equal(lab.TransformerCurve[32..64].Average(), last.Transformer, 1e-12);
        Assert.Equal(lab.RnnCurve[32..64].Average(), last.Memory, 1e-12);
        Assert.Equal(lab.TransformerCurve[0], lab.MemoryBands[0].Transformer, 1e-12);
        Assert.Equal(lab.RnnCurve[4..8].Average(), lab.MemoryBands[2].Memory, 1e-12);
        Assert.Contains(NumberText.Short(lab.MemoryBands[0].Transformer), lab.MemoryFinding);
        Assert.Contains(NumberText.Short(last.Memory), lab.MemoryFinding);
    }

    [Fact]
    public void MeasuredNotes_MatchARecomputation()
    {
        // The paired gap on the 32 shared windows, and on all 292 held-back windows, as LossNote quotes them.
        var shared = MemoryMeasurements.MeanAndError(MemoryMeasurements.PairedGaps(MemoryMeasurements.Shared));
        Assert.Equal(MemoryNetworkLabViewModel.SharedLossGap, Math.Round(shared.Mean, 3));
        Assert.Equal(MemoryNetworkLabViewModel.SharedLossGapError, Math.Round(shared.Error, 3));
        Assert.Equal(MemoryNetworkLabViewModel.HeldBackWindowCount, MemoryMeasurements.HeldBack.Transformer.Length);
        var held = MemoryMeasurements.MeanAndError(MemoryMeasurements.PairedGaps(MemoryMeasurements.HeldBack));
        Assert.Equal(MemoryNetworkLabViewModel.HeldBackLossGap, Math.Round(held.Mean, 3));
        Assert.Equal(MemoryNetworkLabViewModel.HeldBackLossGapError, Math.Round(held.Error, 3));
        Assert.Contains("lower by 0.033 ± 0.012", MemoryNetworkLabViewModel.LossNote);
        Assert.Contains("all 292 windows of the held-back text the gap is only 0.005 ± 0.007", MemoryNetworkLabViewModel.LossNote);

        // The gain from 9-16 to 33-64 characters read on the held-back windows, as MemoryNote quotes it.
        var transformer = MemoryMeasurements.MeanAndError(MemoryMeasurements.Gains(MemoryMeasurements.HeldBack.Transformer));
        var memory = MemoryMeasurements.MeanAndError(MemoryMeasurements.Gains(MemoryMeasurements.HeldBack.Memory));
        Assert.Equal(MemoryNetworkLabViewModel.HeldBackTransformerGain, Math.Round(transformer.Mean, 3));
        Assert.Equal(MemoryNetworkLabViewModel.HeldBackTransformerGainError, Math.Round(transformer.Error, 3));
        Assert.Equal(MemoryNetworkLabViewModel.HeldBackMemoryGain, Math.Round(memory.Mean, 3));
        Assert.Equal(MemoryNetworkLabViewModel.HeldBackMemoryGainError, Math.Round(memory.Error, 3));
        Assert.Contains("−0.006 ± 0.037 for the transformer and −0.008 ± 0.042 for the memory network", MemoryNetworkLabViewModel.MemoryNote);
        // "No gain past about 8 characters": neither gain is as much as two standard errors above zero.
        Assert.True(transformer.Mean < 2 * transformer.Error && memory.Mean < 2 * memory.Error);
    }

    // ---- the hidden state ----

    [Fact]
    public async Task HiddenRows_AreTheNetworksMemoryAfterEachCharacter()
    {
        var lab = await NewLab();
        var (rnn, tokenizer, _) = await ShippedMemory();
        Assert.Equal("The Fox", lab.Text);
        var forward = rnn.Forward(new[] { tokenizer.Encode("The Fox") });

        var state = lab.HiddenState!;
        Assert.Equal(7, state.GetLength(0));
        Assert.Equal(rnn.Config.Hidden, state.GetLength(1));
        for (int t = 0; t < 7; t++)
            Assert.Equal(Row(forward.Hidden[t], 0), Enumerable.Range(0, state.GetLength(1)).Select(u => state[t, u]));
        Assert.Equal(new[] { "T", "h", "e", "␣", "F", "o", "x" }, lab.HiddenRowLabels);

        // Shown in blocks of 32 units, each block the same rows and its own columns, numbered from 1; one block at a time.
        Assert.Equal(rnn.Config.Hidden / MemoryNetworkLabViewModel.UnitsPerBlock, lab.HiddenBlocks.Count);
        var second = lab.HiddenBlocks[1];
        Assert.Equal("Units 33 to 64", second.Title);
        Assert.Equal("33", second.ColumnLabels[0]);
        Assert.Equal("Units 33 to 64", second.ToString());   // what the picker shows
        Assert.Equal(lab.HiddenRowLabels, second.RowLabels);
        Assert.Equal(state[3, 32], second.Values[3, 0]);
        Assert.Equal(state[6, 63], second.Values[6, 31]);
        Assert.Same(lab.HiddenBlocks[0], lab.SelectedBlock);
        lab.SelectedBlockIndex = 7;
        Assert.Equal("Units 225 to 256", lab.SelectedBlock!.Title);
        lab.Text = "The Wolf";   // the chosen block is kept when the text changes
        Assert.Equal("Units 225 to 256", lab.SelectedBlock!.Title);
        Assert.Null(lab.Message);
    }

    [Fact]
    public async Task HiddenRows_ForLongText_ShowTheLastCharacters_AfterReadingAll()
    {
        var lab = await NewLab();
        var (rnn, tokenizer, _) = await ShippedMemory();
        string text = "The Fox and the Grapes. A hungry Fox saw some fine bunches";
        lab.Text = text;
        var forward = rnn.Forward(new[] { tokenizer.Encode(text) });
        int shown = MemoryNetworkLabViewModel.MaxHiddenRows;
        Assert.Equal(shown, lab.HiddenState!.GetLength(0));
        Assert.Equal(Row(forward.Hidden[^1], 0), Enumerable.Range(0, rnn.Config.Hidden).Select(u => lab.HiddenState[shown - 1, u]));
        Assert.Equal(Row(forward.Hidden[text.Length - shown], 0), Enumerable.Range(0, rnn.Config.Hidden).Select(u => lab.HiddenState[0, u]));
        Assert.Contains($"last {shown}", lab.HiddenCaption);
    }

    [Fact]
    public async Task ChangingTheFirstLetter_ChangesEveryLaterRow()
    {
        // Try this step 3: the memory carries the first letter on, so every row after it differs.
        var lab = await NewLab();
        var before = lab.HiddenState!;
        lab.Text = "Xhe Fox";
        var after = lab.HiddenState!;
        for (int t = 0; t < 7; t++)
        {
            double largest = Enumerable.Range(0, before.GetLength(1)).Max(u => Math.Abs(before[t, u] - after[t, u]));
            Assert.True(largest > 1e-3, $"row {t} changed by at most {largest}");
        }
    }

    [Fact]
    public async Task Text_IsCappedAt200Characters()
    {
        var lab = await NewLab();
        lab.Text = string.Concat(Enumerable.Repeat("The Fox and the Crow. ", 20));   // 440 characters
        Assert.Equal(MemoryNetworkLabViewModel.MaxTextLength, lab.Text.Length);
        Assert.Equal(200, MemoryNetworkLabViewModel.MaxTextLength);
        Assert.Contains("read all 200 characters", lab.HiddenCaption);
        lab.Text = new string('a', 199) + "\U0001F98A";   // a fox emoji: two chars, so the cut must not split it
        Assert.Equal(199, lab.Text.Length);
    }

    [Fact]
    public async Task UnknownCharacters_ShowAsTheReplacementMark()
    {
        var lab = await NewLab();
        lab.Text = "Fox€";
        Assert.Equal("�", lab.HiddenRowLabels[^1]);
        Assert.Equal(4, lab.HiddenState!.GetLength(0));
    }

    [Fact]
    public async Task EmptyText_GivesTheMessage_AndNoMemoryOrSamples()
    {
        var lab = await NewLab();
        lab.Text = "";
        Assert.Equal("Type some text first.", lab.Message);
        Assert.Null(lab.HiddenState);
        Assert.Empty(lab.HiddenBlocks);
        await lab.GenerateSamplesCommand.ExecuteAsync(null);
        Assert.Equal("", lab.TransformerSample);
        Assert.Equal("", lab.RnnSample);
        lab.Text = "The";
        Assert.Null(lab.Message);
        Assert.Equal(3, lab.HiddenState!.GetLength(0));
    }

    // ---- samples ----

    [Fact]
    public async Task Samples_AreBothModelsFromTheSameStart_AtTemperature0_8_WithTheSeed()
    {
        var lab = await NewLab();
        lab.Seed = 3;
        await lab.GenerateSamplesCommand.ExecuteAsync(null);
        var fables = ShippedFables(_env.NewActiveModels());
        var (rnn, tokenizer, _) = await ShippedMemory();

        Assert.Equal(Sampling.Generate(fables, "The Fox", 200, new SamplingSettings(0.8), new SeededRandom(3)), lab.TransformerSample);
        Assert.Equal(RnnSampling.Generate(rnn, tokenizer, "The Fox", 200, 0.8, new SeededRandom(3)), lab.RnnSample);
        Assert.Equal(200, lab.TransformerSample.Length);
        Assert.Equal(200, lab.RnnSample.Length);
        Assert.Contains("seed 3", lab.SampleStatus);
        // Equal to a separate generation with a fresh generator seeded 3, so the same seed always gives the same writing.
    }

    [Fact]
    public async Task ChangingTheSeed_ClearsTheSamples()
    {
        var lab = await NewLab();
        await lab.GenerateSamplesCommand.ExecuteAsync(null);
        Assert.NotEqual("", lab.RnnSample);
        lab.Seed = 2;
        Assert.Equal("", lab.RnnSample);
        Assert.Equal("", lab.TransformerSample);
    }

    // ---- train it again ----

    [Fact]
    public async Task TrainAgain_ShowsLiveValues_AndKeepsTheRecordedOnes()
    {
        var lab = await NewLab(retrainSteps: 3);
        double recordedLoss = lab.RnnRecordedLoss;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        await lab.TrainAgainCommand.ExecuteAsync(null);

        Assert.NotNull(lab.RnnLiveLoss);
        Assert.NotNull(lab.RnnLiveSpeed);
        Assert.True(lab.RnnLiveSpeed > 0);
        Assert.Equal(recordedLoss, lab.RnnRecordedLoss);
        Assert.Equal(RecordedResults.FablesValidationLoss, lab.TransformerRecordedLoss);
        Assert.Equal(3, lab.Comparison.Count);
        Assert.Equal(MemoryNetworkLabViewModel.LiveKind, lab.Comparison[2].Kind);
        Assert.Equal(NumberText.Short(lab.RnnLiveLoss!.Value), lab.Comparison[2].Loss);
        Assert.Equal(MemoryNetworkLabViewModel.RecordedKind, lab.Comparison[0].Kind);
        Assert.Equal(MemoryNetworkLabViewModel.RecordedKind, lab.Comparison[1].Kind);

        // The trained network replaces the shipped one in the lab: its curve is measured on the same windows, and its
        // mean is the live validation loss.
        Assert.True(lab.UsingTrainedNetwork);
        Assert.Equal(lab.RnnLiveLoss.Value, lab.RnnCurve.Average(), 1e-9);
        Assert.Equal(MemoryTraining.RetrainHidden, lab.HiddenState!.GetLength(1));
        Assert.Contains("you trained", lab.MemorySeries[1].Name);
        Assert.StartsWith("Trained again with seed 1, for 3 steps", lab.TrainingResult);

        // Each changed screen lights once, at the end of the run.
        Assert.Equal(1, raised.Count(s => s == "training"));
        Assert.Equal(1, raised.Count(s => s == "compare"));
        Assert.Equal(1, raised.Count(s => s == "memory"));
        Assert.False(lab.Runner.IsRunning);
    }

    [Fact]
    public async Task SamplesAfterTraining_UseTheTrainedNetwork()
    {
        var lab = await NewLab(retrainSteps: 3);
        lab.Seed = 2;
        await lab.TrainAgainCommand.ExecuteAsync(null);
        await lab.GenerateSamplesCommand.ExecuteAsync(null);
        var trained = lab.CurrentNetwork!;
        Assert.Equal(MemoryTraining.RetrainHidden, trained.Config.Hidden);
        var (_, tokenizer, _) = await ShippedMemory();
        Assert.Equal(RnnSampling.Generate(trained, tokenizer, "The Fox", 200, 0.8, new SeededRandom(2)), lab.RnnSample);
        Assert.Contains("with seed 2", lab.TrainingResult);
    }

    [Fact]
    public async Task ASecondClick_WhileTheTrainedNetworkIsMeasured_IsIgnored_AndScreensStillLight()
    {
        var lab = await NewLab(retrainSteps: 2);
        using var reached = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        lab.BeforeMeasuringTrained = () => { reached.Set(); release.Wait(); };
        var first = lab.TrainAgainCommand.ExecuteAsync(null);
        await Task.Run(() => reached.Wait());

        // The run has ended but its network is still being measured: Train it again is off, and a click does nothing.
        Assert.False(lab.Runner.IsRunning);
        Assert.True(lab.IsTraining);
        Assert.False(lab.TrainAgainCommand.CanExecute(null));
        await lab.TrainAgainCommand.ExecuteAsync(null);
        release.Set();
        await first;
        Assert.False(lab.IsTraining);
        Assert.True(lab.TrainAgainCommand.CanExecute(null));
        Assert.NotNull(lab.RnnLiveLoss);

        // No run scope is left open: a direct action lights its screen at once.
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        lab.Text = "The Wolf";
        Assert.Contains("hidden", raised);
    }

    [Fact]
    public async Task TrainingNote_SaysTheRetrainedNetworkIsSmaller_AndTrainedForLess()
    {
        var lab = await NewLab();
        Assert.True(MemoryTraining.RetrainHidden < MemoryTraining.ShippedHidden);
        Assert.Contains($"hidden size {MemoryTraining.RetrainHidden}", lab.TrainingNote);
        Assert.Contains($"hidden size {MemoryTraining.ShippedHidden}", lab.TrainingNote);
        Assert.Contains("2,500 steps", lab.TrainingNote);
        Assert.Contains("3,000 steps", lab.TrainingNote);
        Assert.Equal(MemoryTraining.RetrainSteps, new MemoryNetworkLabViewModel(new FamilyModels(TestEnvironment.Content), _env.NewActiveModels(), Fables).RetrainSteps);
    }

    /// <summary>Starts a run whose first step waits until <paramref name="act"/> has been done on this thread, then lets it finish.</summary>
    private static async Task WhileTraining(MemoryNetworkLabViewModel lab, Action act)
    {
        using var reached = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        lab.AfterTrainingStep = step => { if (step == 1) { reached.Set(); release.Wait(); } };
        var run = lab.TrainAgainCommand.ExecuteAsync(null);
        await Task.Run(() => reached.Wait());
        act();
        release.Set();
        await run;
    }

    [Fact]
    public async Task Stop_KeepsTheShippedNetwork_AndShowsNoLiveValues()
    {
        var lab = await NewLab(retrainSteps: 50);
        var shippedCurve = lab.RnnCurve;
        await WhileTraining(lab, lab.StopTraining);
        Assert.Null(lab.RnnLiveLoss);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal(shippedCurve, lab.RnnCurve);
        Assert.StartsWith("Stopped after", lab.Runner.StatusText);
        Assert.Equal(2, lab.Comparison.Count);
    }

    [Fact]
    public async Task Reset_MidRun_LeavesNoStaleResult()
    {
        var lab = await NewLab(retrainSteps: 5);
        var shippedCurve = lab.RnnCurve;
        lab.Text = "Wolf";
        await WhileTraining(lab, lab.Reset);
        await lab.WorkCompleted;
        Assert.Null(lab.RnnLiveLoss);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal(shippedCurve, lab.RnnCurve);
        Assert.Equal("The Fox", lab.Text);
        Assert.False(lab.Runner.IsRunning);
    }

    [Fact]
    public async Task Deactivate_MidRun_LeavesNoStaleResult()
    {
        var lab = await NewLab(retrainSteps: 5);
        await WhileTraining(lab, lab.Deactivate);
        await lab.WorkCompleted;
        Assert.Null(lab.RnnLiveLoss);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.False(lab.Runner.IsRunning);
    }

    [Fact]
    public async Task ASeedChange_MidRun_AbandonsTheRun_AndClearsItsProgress()
    {
        var lab = await NewLab(retrainSteps: 20);
        using var reached = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        lab.AfterTrainingStep = step => { if (step == 12) { reached.Set(); release.Wait(); } };   // after the report at step 10
        var run = lab.TrainAgainCommand.ExecuteAsync(null);
        await Task.Run(() => reached.Wait());
        Assert.StartsWith("Step 10 of 20", lab.TrainingStatus);
        Assert.NotEmpty(lab.TrainingSeries);
        lab.Seed = 7;
        Assert.Equal("", lab.TrainingStatus);
        Assert.Empty(lab.TrainingSeries);
        release.Set();
        await run;
        await lab.WorkCompleted;
        Assert.Equal("", lab.TrainingStatus);
        Assert.Empty(lab.TrainingSeries);
        Assert.Null(lab.RnnLiveLoss);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.False(lab.Runner.IsRunning);
        Assert.Equal(7, lab.Seed);
    }

    [Fact]
    public async Task Reset_AfterTraining_BringsBackTheShippedNetwork()
    {
        var lab = await NewLab(retrainSteps: 3);
        var shippedCurve = lab.RnnCurve;
        await lab.TrainAgainCommand.ExecuteAsync(null);
        await lab.GenerateSamplesCommand.ExecuteAsync(null);
        lab.Seed = 4;
        lab.SelectedBlockIndex = 3;
        lab.Reset();
        Assert.Equal(0, lab.SelectedBlockIndex);
        Assert.Null(lab.RnnLiveLoss);
        Assert.Null(lab.RnnLiveSpeed);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal(shippedCurve, lab.RnnCurve);
        Assert.Equal(MemoryTraining.ShippedHidden, lab.HiddenState!.GetLength(1));
        Assert.Equal(1, lab.Seed);
        Assert.Equal("", lab.RnnSample);
        Assert.Equal(2, lab.Comparison.Count);
    }
}
