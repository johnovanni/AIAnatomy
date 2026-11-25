using AIAnatomy.Models.Training;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class RetrainRunnerTests
{
    // xUnit gives each test its own SynchronizationContext, which would queue the runner's UI posts; with none (as in
    // a plain test run) each report is applied right where it is made, so the tests need no pumping and no waiting.
    private static Task Start(RetrainRunner runner, int seed)
    {
        SynchronizationContext.SetSynchronizationContext(null);
        return runner.StartAsync(seed);
    }

    /// <summary>A trainer whose steps wait on a gate when one is set, so no test depends on timing.</summary>
    private sealed class Gated(int total, int gateAfter = int.MaxValue) : ISteppedTrainer
    {
        public ManualResetEventSlim InStep { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public int TotalSteps => total;
        public int StepsTaken { get; private set; }
        public int Seed { get; init; }
        public Action<int>? OnStep { get; init; }
        public double Step()
        {
            StepsTaken++;
            OnStep?.Invoke(StepsTaken);
            if (StepsTaken == gateAfter) { InStep.Set(); Release.Wait(); }
            return 1.0 / StepsTaken;
        }
    }

    private sealed class Clock { public TimeSpan Now; }

    private static RetrainRunner Runner(Func<int, ISteppedTrainer> make, Clock? clock = null)
    {
        clock ??= new Clock();
        return new RetrainRunner(make, () => clock.Now) { UpdateInterval = TimeSpan.Zero };
    }

    [Fact]
    public async Task CompletedRun_SetsFinishedAndRaisesCompletedOnce()
    {
        var runner = Runner(_ => new Gated(30));
        int completed = 0;
        runner.Completed += (_, _) => { completed++; Assert.NotNull(runner.Finished); Assert.False(runner.IsRunning); };
        await Start(runner, 1);
        Assert.NotNull(runner.Finished);
        Assert.Equal(30, runner.Finished!.StepsTaken);
        Assert.Equal(1, completed);
        Assert.False(runner.IsRunning);
        var series = Assert.Single(runner.LossSeries);
        Assert.Equal("training loss", series.Name);
        Assert.Equal(30, series.Points[^1].X);
        Assert.StartsWith("Step 30 of 30 · loss 0.033 · ", runner.StatusText);
    }

    [Fact]
    public async Task StatusText_ShowsStepLossAndSeconds()
    {
        var clock = new Clock();
        var trainer = new Gated(600, gateAfter: 121) { OnStep = s => { if (s == 115) clock.Now = TimeSpan.FromSeconds(38.4); } };
        var runner = Runner(_ => trainer, clock);
        var run = Start(runner, 1);
        trainer.InStep.Wait();
        Assert.Equal("Step 120 of 600 · loss 0.008 · 38 s", runner.StatusText);
        Assert.True(runner.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(38.4), runner.Elapsed);
        runner.Abandon();
        trainer.Release.Set();
        await run;
    }

    [Fact]
    public async Task Stop_KeepsTrainedStepsAndLeavesFinishedNull()
    {
        var trainer = new Gated(600, gateAfter: 120);
        var runner = Runner(_ => trainer);
        var run = Start(runner, 1);
        trainer.InStep.Wait();
        runner.Stop();
        trainer.Release.Set();
        await run;
        Assert.Null(runner.Finished);
        Assert.False(runner.IsRunning);
        Assert.Equal("Stopped after 120 steps.", runner.StatusText);
        Assert.NotEmpty(runner.LossSeries);
    }

    [Fact]
    public async Task Abandon_MeansNoLaterReportChangesStatusOrSeries()
    {
        var trainer = new Gated(100, gateAfter: 50);
        var runner = Runner(_ => trainer);
        int completed = 0;
        runner.Completed += (_, _) => completed++;
        var run = Start(runner, 1);
        trainer.InStep.Wait();
        string status = runner.StatusText;
        var series = runner.LossSeries;
        runner.Abandon();
        Assert.False(runner.IsRunning);
        trainer.Release.Set();   // the worker finishes its step and keeps reporting
        await run;
        Assert.Equal(status, runner.StatusText);
        Assert.Same(series, runner.LossSeries);
        Assert.Null(runner.Finished);
        Assert.Equal(0, completed);
    }

    [Fact]
    public async Task StartAsyncWhileRunning_DoesNothing()
    {
        var trainer = new Gated(100, gateAfter: 5);
        int made = 0;
        var runner = Runner(_ => { made++; return trainer; });
        var run = Start(runner, 1);
        trainer.InStep.Wait();
        await Start(runner, 2);
        Assert.Equal(1, made);
        Assert.True(runner.IsRunning);
        trainer.Release.Set();
        await run;
        Assert.Equal(1, made);
        Assert.NotNull(runner.Finished);
    }

    [Fact]
    public async Task ASecondRunAfterTheFirst_UsesTheNewSeedAndStartsAfresh()
    {
        var seeds = new List<int>();
        var runner = Runner(seed => { seeds.Add(seed); return new Gated(20) { Seed = seed }; });
        await Start(runner, 3);
        await Start(runner, 4);
        Assert.Equal([3, 4], seeds);
        Assert.Equal(4, ((Gated)runner.Finished!).Seed);
        Assert.Equal(2, runner.LossSeries[0].Points.Count);   // steps 10 and 20 only
    }

    [Fact]
    public async Task UpdatesAreThrottled_ButTheLastReportIsAlwaysShown()
    {
        var clock = new Clock();
        var runner = new RetrainRunner(_ => new Gated(100, 0) , () => clock.Now);   // the clock never moves
        await Start(runner, 1);
        var points = runner.LossSeries[0].Points;
        Assert.Equal([10.0, 100.0], points.Select(p => p.X));   // first report, then (interval not passed) only the last
    }

    [Fact]
    public async Task ADivergedRun_ShowsTheMessageAndFinishesNothing()
    {
        var runner = Runner(_ => new Diverging());
        await Start(runner, 1);
        Assert.Equal(SteppedTraining.DivergedMessage, runner.StatusText);
        Assert.Null(runner.Finished);
        Assert.False(runner.IsRunning);
    }

    private sealed class Diverging : ISteppedTrainer
    {
        public int TotalSteps => 50;
        public int StepsTaken { get; private set; }
        public double Step() { StepsTaken++; return double.NaN; }
    }
}
