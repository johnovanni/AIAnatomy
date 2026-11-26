using System.ComponentModel;
using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>Lesson 4.9's lab: a cloud of 500 random points becoming a shape step by step, made by a shipped diffusion
/// network (four presets) or by one trained here ("train it again", also on a drawn shape). Play is driven by a test
/// clock, so nothing waits on real time. Runs of "train it again" are a few steps long (the test hook), but they train,
/// so the class runs apart from the others.</summary>
[Collection(TrainingCollection.Name)]
public sealed class DiffusionShapesLabTests
{
    /// <summary>A line across the middle of the pad (pad units, 0..1, y down): long and wide enough to be a shape.</summary>
    private static readonly IReadOnlyList<(double X, double Y)> Arc =
        Enumerable.Range(0, 21).Select(i => (0.2 + 0.03 * i, 0.5 - 0.3 * Math.Sin(Math.PI * i / 20))).ToArray();

    /// <summary>A second accepted line: down the left side.</summary>
    private static readonly IReadOnlyList<(double X, double Y)> Upright =
        Enumerable.Range(0, 11).Select(i => (0.3, 0.1 + 0.08 * i)).ToArray();

    /// <summary>Too short: 0.02 of the pad.</summary>
    private static readonly IReadOnlyList<(double X, double Y)> Tiny = [(0.5, 0.5), (0.52, 0.5)];

    /// <summary>Too small: long enough (about 1.4 pads of scribble) but inside a box a tenth of the pad each way.</summary>
    private static readonly IReadOnlyList<(double X, double Y)> Scribble =
        Enumerable.Range(0, 21).Select(i => i % 2 == 0 ? (0.45, 0.45) : (0.55, 0.55)).ToArray();

    /// <summary>A play clock a test ticks by hand.</summary>
    private sealed class ManualClock : IPlayClock
    {
        private Action? _tick;
        public TimeSpan Interval { get; private set; }
        public int Starts { get; private set; }
        public bool Running => _tick != null;

        public IDisposable Start(TimeSpan interval, Action tick)
        {
            Interval = interval;
            Starts++;
            _tick = tick;
            return new Handle(this, tick);
        }

        public void Fire() => _tick?.Invoke();

        private sealed class Handle(ManualClock clock, Action tick) : IDisposable
        {
            public void Dispose() { if (clock._tick == tick) clock._tick = null; }
        }
    }

    /// <summary>A lab with the spiral loaded and its cloud worked out. With no SynchronizationContext every update is
    /// applied where it is made, so nothing needs pumping.</summary>
    private static async Task<(DiffusionShapesLabViewModel Lab, ManualClock Clock)> NewLab(int retrainSteps = 3)
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var clock = new ManualClock();
        var lab = new DiffusionShapesLabViewModel(new FamilyModels(TestEnvironment.Content), clock) { RetrainSteps = retrainSteps };
        lab.Runner.UpdateInterval = TimeSpan.Zero;
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        return (lab, clock);
    }

    private static Task<(DiffusionNet Net, NoiseSchedule Schedule, string ShapeName, ModelInfo Info)> Shipped(string preset) =>
        new FamilyModels(TestEnvironment.Content).DiffusionAsync(preset);

    private static IReadOnlyList<(double X, double Y)> Points(double[,] frame) =>
        Enumerable.Range(0, frame.GetLength(0)).Select(i => (frame[i, 0], frame[i, 1])).ToList();

    private static double[] Flat(double[,] frame) => frame.Cast<double>().ToArray();

    private static void AssertSameFrames(IReadOnlyList<double[,]> expected, IReadOnlyList<double[,]> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int k = 0; k < expected.Count; k++) Assert.Equal(Flat(expected[k]), Flat(actual[k]));
    }

    private static string Quality(double distance) =>
        $"Average distance from the shape after all 100 steps: {distance.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)} (lower is better).";

    // ---- opening and the presets ----

    [Fact]
    public async Task Opens_OnTheSpiral_AtStep0_WithTheShippedNetworksCloud()
    {
        var (lab, _) = await NewLab();
        Assert.Equal("DiffusionShapesLab", DiffusionShapesLabViewModel.Id);
        Assert.Equal(DiffusionShapesLabViewModel.Id, lab.LabId);
        Assert.Equal(new[] { "Spiral", "Circle", "Two moons", "Letter S" }, DiffusionShapesLabViewModel.PresetNames);
        Assert.Equal("Spiral", lab.ShapeName);
        Assert.False(lab.IsDrawnShape);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal(Shapes.Presets[0].Points, lab.TargetPoints);

        var (net, schedule, _, info) = await Shipped("Spiral");
        var expected = DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(1));
        AssertSameFrames(expected, lab.Frames);
        Assert.Equal(0, lab.Step);
        Assert.Equal(100, lab.MaxStep);
        Assert.Equal(Points(expected[0]), lab.CurrentPoints);
        Assert.StartsWith("Step 0 of 100: ", lab.StepText);

        double distance = Shapes.MeanDistanceToShape(Points(expected[^1]), Shapes.Presets[0].Points);
        Assert.Equal(Quality(distance), lab.QualityText);
        Assert.Equal(info.Results!["meanDistance"], lab.RecordedDistance);
        Assert.Contains("0.031", lab.NetworkText);
        Assert.Contains("60,000", lab.NetworkText);
        Assert.False(lab.IsPlaying);
    }

    [Theory]
    [InlineData("Spiral")]
    [InlineData("Circle")]
    [InlineData("Two moons")]
    [InlineData("Letter S")]
    public async Task EachPreset_LoadsItsShippedNetwork_AndItsCloudIsTheSamplers(string preset)
    {
        var (lab, _) = await NewLab();
        lab.Seed = 3;
        await lab.WorkCompleted;
        await lab.ChoosePresetAsync(preset);
        Assert.Equal(preset, lab.ShapeName);
        var points = Shapes.Presets.Single(p => p.Name == preset).Points;
        Assert.Equal(points, lab.TargetPoints);
        var (net, schedule, _, _) = await Shipped(preset);
        var expected = DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(3));
        AssertSameFrames(expected, lab.Frames);
        Assert.Equal(Quality(Shapes.MeanDistanceToShape(Points(expected[^1]), points)), lab.QualityText);
        Assert.Equal(0, lab.Step);
    }

    [Fact]
    public async Task ASeedChange_WorksOutTheCloudAgainWithTheNewSeed()
    {
        var (lab, _) = await NewLab();
        lab.Step = 40;
        lab.Seed = 5;
        await lab.WorkCompleted;
        var (net, schedule, _, _) = await Shipped("Spiral");
        AssertSameFrames(DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(5)), lab.Frames);
        Assert.Equal(0, lab.Step);
    }

    [Fact]
    public async Task ASeedChange_WhileAPresetIsLoading_IsUsedForItsCloud()
    {
        // Review fix 1: the seed is set before the preset's network has loaded and its cloud been worked out.
        var (lab, _) = await NewLab();
        var choosing = lab.ChoosePresetAsync("Circle");
        lab.Seed = 9;
        await choosing;
        await lab.WorkCompleted;
        Assert.Equal("Circle", lab.ShapeName);
        Assert.Equal(9, lab.Seed);
        var (net, schedule, _, _) = await Shipped("Circle");
        var expected = DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(9));
        AssertSameFrames(expected, lab.Frames);
        Assert.Equal(Quality(Shapes.MeanDistanceToShape(Points(expected[^1]), Shapes.Presets[1].Points)), lab.QualityText);
    }

    [Fact]
    public async Task ASeedChange_WhileTheLabFirstLoads_IsUsedForTheSpiralsCloud()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var lab = new DiffusionShapesLabViewModel(new FamilyModels(TestEnvironment.Content), new ManualClock());
        lab.Seed = 2;
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        var (net, schedule, _, _) = await Shipped("Spiral");
        AssertSameFrames(DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(2)), lab.Frames);
    }

    [Fact]
    public async Task StepText_SaysTheStepInWords()
    {
        var (lab, _) = await NewLab();
        Assert.Equal("Step 0 of 100: 500 points of pure random noise. No noise has been taken out yet.", lab.StepText);
        lab.Step = 37;
        Assert.Equal("Step 37 of 100: the network has taken noise out 37 times; 63 steps to go.", lab.StepText);
        Assert.Equal(Points(lab.Frames[37]), lab.CurrentPoints);
        lab.Step = 1;
        Assert.Equal("Step 1 of 100: the network has taken noise out once; 99 steps to go.", lab.StepText);
        lab.Step = 100;
        Assert.Equal("Step 100 of 100: the finished cloud, after taking noise out 100 times.", lab.StepText);
        lab.Step = 500;   // clamped
        Assert.Equal(100, lab.Step);
        lab.Step = -3;
        Assert.Equal(0, lab.Step);
    }

    // ---- drawing ----

    [Fact]
    public async Task ATooShortDrawing_ShowsWhy_AndKeepsTheSpiral()
    {
        var (lab, _) = await NewLab();
        var frames = lab.Frames;
        lab.UseDrawing([Tiny]);
        Assert.Equal(Shapes.TooShortMessage, lab.DrawingMessage);
        Assert.Equal("Spiral", lab.ShapeName);
        Assert.False(lab.IsDrawnShape);
        Assert.Same(frames, lab.Frames);
        Assert.Equal(Shapes.Presets[0].Points, lab.TargetPoints);
    }

    [Fact]
    public async Task ATooSmallDrawing_ShowsWhy_AndKeepsTheEarlierDrawing()
    {
        var (lab, _) = await NewLab();
        lab.UseDrawing([Arc]);
        var drawn = lab.TargetPoints;
        lab.UseDrawing([Arc, Scribble]);
        Assert.Equal(Shapes.TooSmallMessage, lab.DrawingMessage);
        Assert.True(lab.IsDrawnShape);
        Assert.Same(drawn, lab.TargetPoints);
    }

    [Fact]
    public async Task ADrawnShape_HasNoCloudUntilTrained_AndSaysSo()
    {
        var (lab, clock) = await NewLab();
        lab.UseDrawing([Arc]);
        Assert.True(lab.IsDrawnShape);
        Assert.Equal(DiffusionShapesLabViewModel.DrawnShapeName, lab.ShapeName);
        Assert.Equal(Shapes.FromStroke(Arc, 1).Points, lab.TargetPoints);
        Assert.Equal(DiffusionShapesLabViewModel.AcceptedMessage, lab.DrawingMessage);
        Assert.Empty(lab.Frames);
        Assert.Empty(lab.CurrentPoints);
        Assert.Equal(0, lab.MaxStep);
        Assert.Equal("", lab.StepText);
        Assert.Equal("", lab.QualityText);
        Assert.Equal(DiffusionShapesLabViewModel.NeedsTrainingMessage, lab.CloudNote);
        Assert.False(lab.TogglePlayCommand.CanExecute(null));
        lab.Play();
        Assert.False(lab.IsPlaying);
        Assert.False(clock.Running);
        Assert.True(lab.TrainAgainCommand.CanExecute(null));
    }

    [Fact]
    public async Task OnlyTheLastLine_IsUsed()
    {
        var (lab, _) = await NewLab();
        lab.UseDrawing([Arc, Upright]);
        Assert.Equal(Shapes.FromStroke(Upright, 1).Points, lab.TargetPoints);
        lab.UseDrawing([]);   // the pad cleared: the shape stays
        Assert.Equal(Shapes.FromStroke(Upright, 1).Points, lab.TargetPoints);
        Assert.True(lab.IsDrawnShape);
    }

    [Fact]
    public async Task ChoosingAPreset_AfterADrawing_BringsBackItsShippedCloud()
    {
        var (lab, _) = await NewLab();
        lab.UseDrawing([Arc]);
        await lab.ChoosePresetAsync("Circle");
        Assert.False(lab.IsDrawnShape);
        Assert.Equal("", lab.DrawingMessage);
        Assert.Equal(101, lab.Frames.Count);
        Assert.Null(lab.CloudNote);
    }

    [Fact]
    public async Task AQuickSecondChoice_Wins_AndTheChipsStayEnabled()
    {
        var (lab, _) = await NewLab();
        var first = lab.ChoosePresetCommand.ExecuteAsync("Circle");
        Assert.True(lab.ChoosePresetCommand.CanExecute("Letter S"));
        var second = lab.ChoosePresetCommand.ExecuteAsync("Letter S");
        await Task.WhenAll(first, second);
        await lab.WorkCompleted;
        Assert.Equal("Letter S", lab.ShapeName);
        Assert.Equal(Shapes.Presets[3].Points, lab.TargetPoints);
        var (net, schedule, _, _) = await Shipped("Letter S");
        AssertSameFrames(DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(1)), lab.Frames);
    }

    // ---- play ----

    [Fact]
    public async Task Play_StepsEvery50ms_ToTheLastStep_AndStops()
    {
        var (lab, clock) = await NewLab();
        lab.Play();
        Assert.True(lab.IsPlaying);
        Assert.False(lab.IsStepLineLive);   // not announced at every tick
        Assert.Equal(TimeSpan.FromMilliseconds(50), clock.Interval);
        for (int i = 1; i <= 99; i++)
        {
            clock.Fire();
            Assert.Equal(i, lab.Step);
            Assert.True(lab.IsPlaying);
        }
        // The last tick: Play ends before the step line changes, so the line is live again when it says the last step.
        var order = new List<string>();
        PropertyChangedEventHandler record = (_, e) => order.Add(e.PropertyName!);
        lab.PropertyChanged += record;
        clock.Fire();
        lab.PropertyChanged -= record;
        Assert.Equal(100, lab.Step);
        Assert.False(lab.IsPlaying);
        Assert.True(lab.IsStepLineLive);
        Assert.False(clock.Running);
        Assert.True(order.IndexOf(nameof(lab.IsStepLineLive)) < order.IndexOf(nameof(lab.StepText)), string.Join(", ", order));
        Assert.Equal(Points(lab.Frames[100]), lab.CurrentPoints);
        clock.Fire();   // nothing left running
        Assert.Equal(100, lab.Step);
        Assert.Equal(1, clock.Starts);
    }

    [Fact]
    public async Task Play_AtTheEnd_StartsAgainFromStep0()
    {
        var (lab, clock) = await NewLab();
        lab.Step = 100;
        lab.TogglePlayCommand.Execute(null);
        Assert.True(lab.IsPlaying);
        Assert.Equal(0, lab.Step);
        clock.Fire();
        Assert.Equal(1, lab.Step);
    }

    [Fact]
    public async Task Pause_StopsPlay_AndTheToggleSaysWhatItWillDo()
    {
        var (lab, clock) = await NewLab();
        Assert.Equal("Play", lab.PlayButtonText);
        lab.TogglePlayCommand.Execute(null);
        Assert.Equal("Pause", lab.PlayButtonText);
        clock.Fire();
        clock.Fire();
        lab.TogglePlayCommand.Execute(null);
        Assert.False(lab.IsPlaying);
        Assert.Equal("Play", lab.PlayButtonText);
        Assert.False(clock.Running);
        Assert.Equal(2, lab.Step);
        Assert.True(lab.IsStepLineLive);
    }

    [Fact]
    public async Task MovingTheStepByHand_WhilePlaying_Pauses()
    {
        var (lab, clock) = await NewLab();
        lab.Play();
        clock.Fire();
        lab.Step = 60;
        Assert.False(lab.IsPlaying);
        Assert.False(clock.Running);
        Assert.Equal(60, lab.Step);
        Assert.True(lab.IsStepLineLive);
    }

    [Fact]
    public async Task ChoosingAShape_OrASeed_StopsPlay()
    {
        var (lab, clock) = await NewLab();
        lab.Play();
        clock.Fire();
        var choosing = lab.ChoosePresetAsync("Circle");
        Assert.False(lab.IsPlaying);
        Assert.False(clock.Running);
        await choosing;
        lab.Play();
        lab.Seed = 2;
        Assert.False(lab.IsPlaying);
        await lab.WorkCompleted;
    }

    // ---- screens ----

    [Fact]
    public async Task Screens_AreBaselines_AndTheCloudLightsOncePerNewCloud_NotPerStep()
    {
        var (lab, clock) = await NewLab();
        Assert.Equal(new[] { "cloud", "training" }.Order(), lab.ReportedScreens.Order());
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        lab.Step = 50;
        lab.Play();
        for (int i = 0; i < 60; i++) clock.Fire();
        Assert.Empty(raised);
        await lab.ChoosePresetAsync("Two moons");
        Assert.Equal(new[] { "cloud" }, raised);
        raised.Clear();
        lab.UseDrawing([Tiny]);   // refused: nothing changes
        Assert.Empty(raised);
        lab.UseDrawing([Arc]);
        Assert.Equal(new[] { "cloud" }, raised);
    }

    // ---- train it again ----

    [Fact]
    public async Task TrainAgain_OnADrawing_GivesItACloud()
    {
        var (lab, _) = await NewLab(retrainSteps: 3);
        lab.UseDrawing([Arc]);
        var target = lab.TargetPoints;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        await lab.TrainAgainCommand.ExecuteAsync(null);

        Assert.True(lab.UsingTrainedNetwork);
        var trained = lab.CurrentNetwork!;
        Assert.Equal(DiffusionTraining.Retrain, trained.Config);
        var expected = DiffusionSampler.Sample(trained, new NoiseSchedule(), 500, new SeededRandom(1));
        AssertSameFrames(expected, lab.Frames);
        Assert.Equal(Quality(Shapes.MeanDistanceToShape(Points(expected[^1]), target)), lab.QualityText);
        Assert.StartsWith("Trained again with seed 1, for 3 steps", lab.TrainingResult);
        Assert.Null(lab.CloudNote);
        Assert.Equal(0, lab.Step);
        Assert.Contains("the network you trained", lab.NetworkText);
        Assert.Equal(1, raised.Count(s => s == "training"));   // lights once, at the end
        Assert.Equal(1, raised.Count(s => s == "cloud"));
        Assert.False(lab.Runner.IsRunning);
        Assert.False(lab.IsTraining);
    }

    [Fact]
    public async Task TrainAgain_OnAPreset_ReplacesTheShippedNetwork_UntilReset()
    {
        var (lab, _) = await NewLab(retrainSteps: 3);
        var shippedFrames = lab.Frames;
        lab.Seed = 4;
        await lab.WorkCompleted;
        await lab.TrainAgainCommand.ExecuteAsync(null);
        Assert.True(lab.UsingTrainedNetwork);
        AssertSameFrames(DiffusionSampler.Sample(lab.CurrentNetwork!, new NoiseSchedule(), 500, new SeededRandom(4)), lab.Frames);
        Assert.Equal("Spiral", lab.ShapeName);

        // A new seed works out the trained network's cloud again.
        lab.Seed = 6;
        await lab.WorkCompleted;
        Assert.True(lab.UsingTrainedNetwork);
        AssertSameFrames(DiffusionSampler.Sample(lab.CurrentNetwork!, new NoiseSchedule(), 500, new SeededRandom(6)), lab.Frames);

        lab.Reset();
        await lab.WorkCompleted;
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal(1, lab.Seed);
        Assert.Equal("", lab.TrainingResult);
        AssertSameFrames(shippedFrames, lab.Frames);
    }

    [Fact]
    public async Task TrainingNote_SaysItTrainsForFewerSteps_AndIsUsuallyLessTight()
    {
        var (lab, _) = await NewLab();
        Assert.Contains("20,000 steps", lab.TrainingNote);
        Assert.Contains("60,000", lab.TrainingNote);
        Assert.Contains("less tight", lab.TrainingNote);
        Assert.Contains("0.069", lab.TrainingNote);   // the spiral after 20,000 steps, seed 1 (docs/decisions.md, Task 10 review)
        Assert.Equal(DiffusionTraining.RetrainSteps, new DiffusionShapesLabViewModel(new FamilyModels(TestEnvironment.Content)).RetrainSteps);
    }

    [Fact]
    public async Task ASecondClick_WhileTheTrainedNetworkIsMeasured_IsIgnored()
    {
        var (lab, _) = await NewLab(retrainSteps: 2);
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
        Assert.True(lab.UsingTrainedNetwork);

        // No run scope is left open: a direct action lights its screen at once.
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        await lab.ChoosePresetAsync("Circle");
        Assert.Contains("cloud", raised);
    }

    /// <summary>Starts a run whose first step waits until <paramref name="act"/> has been done on this thread, then lets it finish.</summary>
    private static async Task WhileTraining(DiffusionShapesLabViewModel lab, Action act)
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
    public async Task Stop_UsesNothingItTrained()
    {
        var (lab, _) = await NewLab(retrainSteps: 50);
        lab.UseDrawing([Arc]);
        await WhileTraining(lab, lab.StopTraining);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Empty(lab.Frames);
        Assert.Equal("", lab.TrainingResult);
        Assert.Equal(DiffusionShapesLabViewModel.NeedsTrainingMessage, lab.CloudNote);
        Assert.StartsWith("Stopped after", lab.Runner.StatusText);
    }

    [Fact]
    public async Task Reset_MidRun_LeavesNoStaleResult()
    {
        var (lab, _) = await NewLab(retrainSteps: 5);
        lab.UseDrawing([Arc]);
        await WhileTraining(lab, lab.Reset);
        await lab.WorkCompleted;
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal("", lab.TrainingResult);
        Assert.Equal("Spiral", lab.ShapeName);
        Assert.False(lab.IsDrawnShape);
        Assert.Equal(101, lab.Frames.Count);
        Assert.False(lab.Runner.IsRunning);
        Assert.False(lab.IsTraining);
    }

    [Fact]
    public async Task Deactivate_MidRun_LeavesNoStaleResult()
    {
        var (lab, clock) = await NewLab(retrainSteps: 5);
        lab.Play();
        await WhileTraining(lab, lab.Deactivate);
        await lab.WorkCompleted;
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal("", lab.TrainingResult);
        Assert.False(lab.Runner.IsRunning);
        Assert.False(lab.IsPlaying);
        Assert.False(clock.Running);
    }

    [Fact]
    public async Task ASeedChange_MidRun_AbandonsTheRun_AndClearsItsProgress()
    {
        var (lab, _) = await NewLab(retrainSteps: 20);
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
        Assert.Equal("", lab.TrainingResult);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.False(lab.Runner.IsRunning);
        Assert.Equal(7, lab.Seed);
        var (net, schedule, _, _) = await Shipped("Spiral");
        AssertSameFrames(DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(7)), lab.Frames);
    }

    [Fact]
    public async Task ChoosingAnotherShape_MidRun_AbandonsTheRun()
    {
        var (lab, _) = await NewLab(retrainSteps: 5);
        Task? choosing = null;
        await WhileTraining(lab, () => choosing = lab.ChoosePresetAsync("Circle"));
        await choosing!;
        await lab.WorkCompleted;
        Assert.Equal("Circle", lab.ShapeName);
        Assert.False(lab.UsingTrainedNetwork);
        Assert.Equal("", lab.TrainingResult);
        Assert.False(lab.IsTraining);
    }

    [Fact]
    public async Task ABadContentFolder_GivesAPlainMessage()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var folder = Path.Combine(Path.GetTempPath(), "aianatomy-diffusion-" + Guid.NewGuid().ToString("N"));
        var lab = new DiffusionShapesLabViewModel(new FamilyModels(folder), new ManualClock());
        await lab.WorkCompleted;
        Assert.False(lab.IsReady);
        Assert.Contains("missing", lab.LoadMessage);
        Assert.False(lab.TrainAgainCommand.CanExecute(null));
        lab.Play();
        lab.UseDrawing([Arc]);
        lab.Reset();
        await lab.WorkCompleted;
    }
}
