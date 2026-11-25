using AIAnatomy.Core;
using AIAnatomy.Models.Vision;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>Lesson 4.7's lab: the shipped digits network reading a test digit or a drawing, and "train it again". Runs of
/// "train it again" are a few steps long (the test hook), but they train, so the class runs apart from the others.</summary>
[Collection(TrainingCollection.Name)]
public sealed class DigitNetworkLabTests
{
    private static readonly IReadOnlyList<IReadOnlyList<(double X, double Y)>> StraightOne = [[(0.5, 0.1), (0.5, 0.9)]];

    /// <summary>A lab with its network and digits loaded. With no SynchronizationContext every update is applied where it
    /// is made, so nothing needs pumping.</summary>
    private static async Task<DigitNetworkLabViewModel> NewLab(int retrainSteps = 3)
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var lab = new DigitNetworkLabViewModel(new FamilyModels(TestEnvironment.Content)) { RetrainSteps = retrainSteps };
        lab.Runner.UpdateInterval = TimeSpan.Zero;
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        return lab;
    }

    private static Task<DigitSet> TestSet() => new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();

    private static async Task<DigitNet> Shipped() => (await new FamilyModels(TestEnvironment.Content).DigitsAsync()).Net;

    private static double[] Flat(double[,] map)
    {
        var flat = new double[map.Length];
        int cols = map.GetLength(1);
        for (int r = 0; r < map.GetLength(0); r++)
            for (int c = 0; c < cols; c++) flat[r * cols + c] = map[r, c];
        return flat;
    }

    /// <summary>Map <paramref name="map"/> of image 0 of a [1, C, S, S] tensor, row by row.</summary>
    private static double[] MapOf(Tensor t, int map)
    {
        int size = t.Shape[2] * t.Shape[3];
        return t.Data.Skip(map * size).Take(size).ToArray();
    }

    private static int ArgMax(IReadOnlyList<double> p)
    {
        int best = 0;
        for (int j = 1; j < p.Count; j++)
            if (p[j] > p[best]) best = j;
        return best;
    }

    // ---- opening ----

    [Fact]
    public async Task Opens_OnSample0_ReadByTheShippedNetworkAsDigitNetDoes()
    {
        var lab = await NewLab();
        Assert.Equal("DigitNetworkLab", DigitNetworkLabViewModel.Id);
        Assert.Equal(DigitNetworkLabViewModel.Id, lab.LabId);
        var set = await TestSet();
        var net = await Shipped();
        var trace = net.Forward(set.Batch([0]));
        var expected = trace.Probabilities(0);

        Assert.Equal(0, lab.SampleIndex);
        Assert.Equal(1740, lab.SampleCount);
        Assert.Equal(set.Label(0), lab.SampleLabel);
        Assert.False(lab.UsingDrawing);
        Assert.Null(lab.Message);
        Assert.True(lab.HasPrediction);
        Assert.Equal(set.Pixels(0), Flat(lab.Image!));
        Assert.Equal(expected, lab.Probabilities);
        Assert.Equal(ArgMax(expected), lab.PredictedDigit);
        Assert.Equal(DigitNetworkLabViewModel.PredictionFor(ArgMax(expected), expected[ArgMax(expected)]), lab.PredictionText);
        Assert.Equal(Enumerable.Range(0, 10).Select(d => d.ToString()), lab.Bars.Select(b => b.Label));
        Assert.Equal(expected, lab.Bars.Select(b => b.Value));
        Assert.Equal(lab.PredictedDigit.ToString(), lab.HighlightedBar);
        Assert.False(lab.UsingTrainedNetwork);
    }

    [Fact]
    public async Task Maps_Are16Of8By8_And32Of4By4_AfterReLU_FromTheNetworksOwnTrace()
    {
        var lab = await NewLab();
        var set = await TestSet();
        var trace = (await Shipped()).Forward(set.Batch([0]));

        Assert.Equal(16, lab.Layer1Maps.Count);
        Assert.All(lab.Layer1Maps, m => Assert.Equal((8, 8), (m.GetLength(0), m.GetLength(1))));
        Assert.Equal(32, lab.Layer2Maps.Count);
        Assert.All(lab.Layer2Maps, m => Assert.Equal((4, 4), (m.GetLength(0), m.GetLength(1))));
        for (int f = 0; f < 16; f++) Assert.Equal(MapOf(trace.Conv1, f), Flat(lab.Layer1Maps[f]));
        for (int f = 0; f < 32; f++) Assert.Equal(MapOf(trace.Conv2, f), Flat(lab.Layer2Maps[f]));
        // After ReLU: nothing below zero.
        Assert.All(lab.Layer1Maps.Concat(lab.Layer2Maps).SelectMany(Flat), v => Assert.True(v >= 0));
        int active = trace.Hidden.Data.Count(v => v > 0);
        Assert.Equal(active, lab.HiddenActive);
        Assert.Contains($"{active} of the 128", lab.HiddenText);
    }

    [Fact]
    public void PredictionFor_SaysTheDigitAndItsChance_WithoutRoundingTo100OrTo0()
    {
        Assert.Equal("It reads this as a 7 (91%).", DigitNetworkLabViewModel.PredictionFor(7, 0.9104));
        Assert.Equal("It reads this as an 8 (55%).", DigitNetworkLabViewModel.PredictionFor(8, 0.549));
        Assert.Equal("It reads this as a 0 (over 99%).", DigitNetworkLabViewModel.PredictionFor(0, 0.9996));
        Assert.Equal("It reads this as a 1 (100%).", DigitNetworkLabViewModel.PredictionFor(1, 1.0));
    }

    [Fact]
    public async Task RecordedAccuracy_IsTheModelFilesResult_LabelledRecorded()
    {
        var lab = await NewLab();
        var (_, info) = await new FamilyModels(TestEnvironment.Content).DigitsAsync();
        double recorded = info.Results!["testAccuracy"];
        Assert.Equal(recorded, lab.RecordedAccuracy);
        int right = (int)Math.Round(recorded * 1740);
        Assert.Equal($"Recorded when it was built: right on {right:N0} of 1,740 test digits ({recorded * 100:0.0}%).", lab.RecordedAccuracyText);
    }

    // ---- choosing the input ----

    [Fact]
    public async Task NextPreviousAndADigit_ChooseTheTestDigit_AndThePredictionFollows()
    {
        var lab = await NewLab();
        var set = await TestSet();
        var net = await Shipped();
        lab.Previous();
        Assert.Equal(1739, lab.SampleIndex);
        lab.Next();
        lab.Next();
        Assert.Equal(1, lab.SampleIndex);
        Assert.Equal(net.Forward(set.Batch([1])).Probabilities(0), lab.Probabilities);
        lab.ShowDigit(7);
        Assert.Equal(7, lab.SampleLabel);
        Assert.Equal(Enumerable.Range(2, set.Count - 2).First(i => set.Label(i) == 7), lab.SampleIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => lab.ShowDigit(-1));
    }

    [Fact]
    public async Task ATestDigit_SaysWhetherTheReadingIsRight()
    {
        var lab = await NewLab();
        Assert.Contains($"labelled {lab.SampleLabel}", lab.AnswerText);
        Assert.Contains(lab.PredictedDigit == lab.SampleLabel ? "right" : "mistake", lab.AnswerText);
        lab.UseDrawing(StraightOne);
        Assert.Equal("", lab.AnswerText);   // a drawing has no label
    }

    [Fact]
    public async Task ADrawing_IsMadeInto8By8First_AndRead()
    {
        var lab = await NewLab();
        var net = await Shipped();
        lab.UseDrawing(StraightOne);
        var pixels = DigitImage.FromStrokes(StraightOne)!;
        Assert.True(lab.UsingDrawing);
        Assert.Null(lab.Message);
        Assert.True(lab.HasPrediction);
        Assert.Equal(pixels, Flat(lab.Image!));
        Assert.Equal(DigitNetworkLabViewModel.DrawingCaption, lab.ImageCaption);
        var expected = net.Forward(Tensor.FromArray(pixels, 1, 1, 8, 8)).Probabilities(0);
        Assert.Equal(expected, lab.Probabilities);
        Assert.Equal(1, lab.PredictedDigit);   // a straight "1" (Task 8's check: read as 1 at 1.00)
    }

    [Fact]
    public async Task AnEmptyDrawing_GivesDrawADigitFirst_AndNoPrediction()
    {
        var lab = await NewLab();
        lab.UseDrawing([]);
        Assert.Equal("Draw a digit first, or pick a sample.", lab.Message);
        Assert.Equal(DigitNetworkLabViewModel.EmptyMessage, lab.Message);
        Assert.Equal(lab.Message, lab.PredictionText);
        Assert.False(lab.HasPrediction);
        Assert.Null(lab.PredictedDigit);
        Assert.Empty(lab.Probabilities);
        Assert.Empty(lab.Bars);
        Assert.Empty(lab.Layer1Maps);
        Assert.Empty(lab.Layer2Maps);
        Assert.Null(lab.Image);
        Assert.True(lab.UsingDrawing);   // the pad (empty) is the input until a sample is picked

        lab.Next();
        Assert.True(lab.HasPrediction);
        Assert.Null(lab.Message);
        Assert.False(lab.UsingDrawing);
    }

    [Fact]
    public async Task ADot_IsNotADigitYet()
    {
        // The shipped network reads a lone dot as a 4 (0.37), so the lab says it is not a digit instead.
        var lab = await NewLab();
        lab.UseDrawing([[(0.4, 0.6)]]);
        Assert.Equal("That's a dot, not a digit yet. Draw the whole digit, or pick a sample.", lab.Message);
        Assert.Equal(DigitNetworkLabViewModel.DotMessage, lab.PredictionText);
        Assert.False(lab.HasPrediction);
        Assert.Empty(lab.Probabilities);
        Assert.Empty(lab.Layer1Maps);
        Assert.Equal(DigitImage.FromStrokes([[(0.4, 0.6)]]), Flat(lab.Image!));   // what the dot became is still shown

        // A small scribble, all within a tenth of the pad each way, is a dot too.
        lab.UseDrawing([[(0.40, 0.40), (0.45, 0.43), (0.42, 0.49)]]);
        Assert.Equal(DigitNetworkLabViewModel.DotMessage, lab.Message);
        // A dot and a digit together is a drawing to read.
        lab.UseDrawing([[(0.5, 0.1), (0.5, 0.9)], [(0.8, 0.9)]]);
        Assert.True(lab.HasPrediction);
        Assert.Null(lab.Message);
    }

    [Fact]
    public void IsADot_IsTheInksBoxUnderATenthOfThePad()
    {
        Assert.Equal(DigitImage.MinMark, DigitNetworkLabViewModel.MinDigitSize);
        Assert.True(DigitNetworkLabViewModel.IsADot([[(0.3, 0.3)]]));
        Assert.True(DigitNetworkLabViewModel.IsADot([[(0.3, 0.3), (0.39, 0.39)]]));
        Assert.False(DigitNetworkLabViewModel.IsADot([[(0.3, 0.3), (0.3, 0.41)]]));   // a short stroke 0.11 tall
        Assert.False(DigitNetworkLabViewModel.IsADot([[(0.3, 0.3)], [(0.5, 0.3)]]));   // two dots far apart
        Assert.False(DigitNetworkLabViewModel.IsADot([]));   // no ink is empty, not a dot
    }

    // ---- screens ----

    [Fact]
    public async Task Screens_AreBaselines_AndLightOncePerChange()
    {
        var lab = await NewLab();
        Assert.Equal(new[] { "maps", "probabilities", "training" }.Order(), lab.ReportedScreens.Order());
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        lab.Next();
        Assert.Equal(new[] { "maps", "probabilities" }, raised.Order());
        raised.Clear();
        lab.UseDrawing([]);
        Assert.Equal(new[] { "maps", "probabilities" }, raised.Order());
    }

    // ---- train it again ----

    [Fact]
    public async Task TrainAgain_ReplacesTheNetwork_AndShowsTheAccuracyLine()
    {
        var lab = await NewLab(retrainSteps: 3);
        var set = await TestSet();
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        await lab.TrainAgainCommand.ExecuteAsync(null);

        Assert.True(lab.UsingTrainedNetwork);
        var trained = lab.CurrentNetwork!;
        Assert.Equal(DigitTraining.Retrain, trained.Config);
        double accuracy = DigitTrainer.Accuracy(trained, set);
        Assert.Equal(accuracy, lab.TrainedAccuracy);
        int right = (int)Math.Round(accuracy * set.Count);
        Assert.Contains($"Right on {right:N0} of 1,740 test digits ({accuracy * 100:0.0}%)", lab.TrainingResult);
        Assert.StartsWith("Trained again with seed 1, for 3 steps", lab.TrainingResult);
        // The trained network now reads the input.
        Assert.Equal(trained.Forward(set.Batch([0])).Probabilities(0), lab.Probabilities);
        // The recorded line is the shipped network's still.
        Assert.StartsWith("Recorded when it was built", lab.RecordedAccuracyText);

        Assert.Equal(1, raised.Count(s => s == "training"));   // lights once, at the end
        Assert.False(lab.Runner.IsRunning);
        Assert.False(lab.IsTraining);
    }

    [Fact]
    public async Task TrainingNote_SaysItIsTheSameNetwork_TrainedForFewerSteps()
    {
        var lab = await NewLab();
        Assert.Equal(DigitTraining.Shipped, DigitTraining.Retrain);
        Assert.Contains("2,000 steps", lab.TrainingNote);
        Assert.Contains("4,000", lab.TrainingNote);
        Assert.Contains("same", lab.TrainingNote);
        Assert.DoesNotContain("smaller", lab.TrainingNote);
        Assert.Equal(DigitTraining.RetrainSteps, new DigitNetworkLabViewModel(new FamilyModels(TestEnvironment.Content)).RetrainSteps);
    }

    [Fact]
    public async Task ASecondClick_WhileTheTrainedNetworkIsMeasured_IsIgnored()
    {
        var lab = await NewLab(retrainSteps: 2);
        using var reached = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        lab.BeforeMeasuringTrained = () => { reached.Set(); release.Wait(); };
        var first = lab.TrainAgainCommand.ExecuteAsync(null);
        await Task.Run(() => reached.Wait());
        Assert.False(lab.Runner.IsRunning);
        Assert.True(lab.IsTraining);
        Assert.False(lab.TrainAgainCommand.CanExecute(null));
        await lab.TrainAgainCommand.ExecuteAsync(null);
        release.Set();
        await first;
        Assert.False(lab.IsTraining);
        Assert.True(lab.TrainAgainCommand.CanExecute(null));
        Assert.NotNull(lab.TrainedAccuracy);

        // No run scope is left open: a direct action lights its screen at once.
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        lab.Next();
        Assert.Contains("probabilities", raised);
    }

    /// <summary>Starts a run whose first step waits until <paramref name="act"/> has been done on this thread, then lets it finish.</summary>
    private static async Task WhileTraining(DigitNetworkLabViewModel lab, Action act)
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
    public async Task Stop_KeepsTheShippedNetwork()
    {
        var lab = await NewLab(retrainSteps: 50);
        var shipped = lab.Probabilities;
        await WhileTraining(lab, lab.StopTraining);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Null(lab.TrainedAccuracy);
        Assert.Equal("", lab.TrainingResult);
        Assert.Equal(shipped, lab.Probabilities);
        Assert.StartsWith("Stopped after", lab.Runner.StatusText);
    }

    [Fact]
    public async Task Reset_MidRun_LeavesNoStaleResult()
    {
        var lab = await NewLab(retrainSteps: 5);
        lab.ShowDigit(3);
        await WhileTraining(lab, lab.Reset);
        await lab.WorkCompleted;
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Null(lab.TrainedAccuracy);
        Assert.Equal("", lab.TrainingResult);
        Assert.Equal(0, lab.SampleIndex);
        Assert.False(lab.Runner.IsRunning);
        Assert.False(lab.IsTraining);
    }

    [Fact]
    public async Task Deactivate_MidRun_LeavesNoStaleResult()
    {
        var lab = await NewLab(retrainSteps: 5);
        await WhileTraining(lab, lab.Deactivate);
        await lab.WorkCompleted;
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Null(lab.TrainedAccuracy);
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
        Assert.Null(lab.TrainedAccuracy);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.False(lab.Runner.IsRunning);
        Assert.Equal(7, lab.Seed);
    }

    [Fact]
    public async Task Reset_AfterTraining_BringsBackTheShippedNetwork()
    {
        var lab = await NewLab(retrainSteps: 3);
        var shipped = lab.Probabilities;
        lab.Seed = 4;
        await lab.TrainAgainCommand.ExecuteAsync(null);
        Assert.True(lab.UsingTrainedNetwork);
        lab.UseDrawing(StraightOne);
        lab.Reset();
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Null(lab.TrainedAccuracy);
        Assert.Equal("", lab.TrainingResult);
        Assert.Equal(1, lab.Seed);
        Assert.Equal(0, lab.SampleIndex);
        Assert.False(lab.UsingDrawing);
        Assert.Equal(shipped, lab.Probabilities);
    }

    [Fact]
    public async Task ABadContentFolder_GivesAPlainMessage()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var folder = Path.Combine(Path.GetTempPath(), "aianatomy-digitnet-" + Guid.NewGuid().ToString("N"));
        var lab = new DigitNetworkLabViewModel(new FamilyModels(folder));
        await lab.WorkCompleted;
        Assert.False(lab.IsReady);
        Assert.Contains("missing", lab.LoadMessage);
        Assert.False(lab.TrainAgainCommand.CanExecute(null));
        lab.Next();   // nothing to show: no throw
        lab.UseDrawing(StraightOne);
        lab.Reset();
    }
}
