using AIAnatomy.Models.Training;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>One test sets the static ErrorLog.BackgroundFailed, so the class runs apart from the others (triage, Task 5).</summary>
[Collection(ErrorLogCollection.Name)]
public class RetrainRunnerTests
{
    // xUnit gives each test its own SynchronizationContext, which would queue the runner's UI posts; with none (as in
    // a plain test run) each report is applied right where it is made, so the tests need no pumping and no waiting.
    // The runner reads the context when it starts, so the test's own context is put back at once (triage, Task 5: a
    // context left changed could reach a later test on the same thread).
    private static Task Start(RetrainRunner runner, int seed)
    {
        var before = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try { return runner.StartAsync(seed); }
        finally { SynchronizationContext.SetSynchronizationContext(before); }
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

    // Final review M10: "1 step", not "1 steps".
    [Fact]
    public async Task Stop_AfterOneStep_SaysStepInTheSingular()
    {
        var trainer = new Gated(600, gateAfter: 1);
        var runner = Runner(_ => trainer);
        var run = Start(runner, 1);
        trainer.InStep.Wait();
        runner.Stop();
        trainer.Release.Set();
        await run;
        Assert.Equal("Stopped after 1 step.", runner.StatusText);
    }

    [Fact]
    public void Start_PutsTheTestsSynchronizationContextBack()
    {
        var mine = new SynchronizationContext();
        var before = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(mine);
        try
        {
            var runner = Runner(_ => new Gated(10));
            var run = Start(runner, 1);
            Assert.Same(mine, SynchronizationContext.Current);
            run.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(before); }
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

    // ---- the real UI path: a context that queues posts until the test pumps them ----

    private sealed class QueueContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
        public override void Post(SendOrPostCallback d, object? state) { lock (_queue) _queue.Enqueue((d, state)); }
        public int Count { get { lock (_queue) return _queue.Count; } }
        public void Pump()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) item;
                lock (_queue) { if (_queue.Count == 0) return; item = _queue.Dequeue(); }
                item.Callback(item.State);
            }
        }
    }

    private static Task StartOn(QueueContext ui, RetrainRunner runner, int seed)
    {
        SynchronizationContext.SetSynchronizationContext(ui);
        try { return runner.StartAsync(seed); }
        finally { SynchronizationContext.SetSynchronizationContext(null); }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        // Waits on the worker's progress, not on time: yields until the condition holds.
        while (!condition()) await Task.Yield();
    }

    [Fact]
    public async Task QueuedReports_ThenAbandon_ThenPump_ChangeNothing()
    {
        var ui = new QueueContext();
        var runner = Runner(_ => new Gated(100));
        var run = StartOn(ui, runner, 1);
        await WaitUntil(() => ui.Count >= 11);   // ten reports and the end of the run are queued
        string status = runner.StatusText;
        var series = runner.LossSeries;
        runner.Abandon();
        ui.Pump();
        await run;
        Assert.Equal(status, runner.StatusText);
        Assert.Same(series, runner.LossSeries);
        Assert.Null(runner.Finished);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task Completed_IsRaisedOnThePumpingThread_AfterTheFinalReport()
    {
        var ui = new QueueContext();
        var runner = Runner(_ => new Gated(30));
        int completedOn = -1;
        string? statusAtCompleted = null;
        ISteppedTrainer? finishedAtCompleted = null;
        runner.Completed += (_, _) =>
        {
            completedOn = Environment.CurrentManagedThreadId;
            statusAtCompleted = runner.StatusText;
            finishedAtCompleted = runner.Finished;
        };
        var run = StartOn(ui, runner, 1);
        await WaitUntil(() => ui.Count >= 4);   // three reports and the continuation
        int pumpThread = Environment.CurrentManagedThreadId;   // after the waiting, which may have moved threads
        ui.Pump();
        await run;
        Assert.Equal(pumpThread, completedOn);
        Assert.StartsWith("Step 30 of 30", statusAtCompleted);   // the final report was applied first
        Assert.NotNull(finishedAtCompleted);
    }

    [Fact]
    public async Task ATrainerThatThrows_ShowsAPlainSentenceAndStopsRunning()
    {
        Exception? logged = null;
        var before = ErrorLog.BackgroundFailed;
        ErrorLog.BackgroundFailed = e => logged = e;
        try
        {
            var runner = Runner(_ => new Throwing());
            await Start(runner, 1);
            Assert.Equal("Training stopped because of an unexpected problem.", runner.StatusText);
            Assert.DoesNotContain("secret", runner.StatusText);
            Assert.False(runner.IsRunning);
            Assert.Null(runner.Finished);
            Assert.Contains("secret", logged?.Message);
        }
        finally { ErrorLog.BackgroundFailed = before; }
    }

    private sealed class Throwing : ISteppedTrainer
    {
        public int TotalSteps => 10;
        public int StepsTaken => 0;
        public double Step() => throw new InvalidOperationException("secret detail");
    }

    [Fact]
    public async Task ATrainerThatCannotBeCreated_LeavesTheRunnerUntouched()
    {
        bool fail = true;
        var runner = Runner(_ => fail ? throw new InvalidOperationException("no") : new Gated(20));
        SynchronizationContext.SetSynchronizationContext(null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync(1));
        Assert.False(runner.IsRunning);
        fail = false;
        await Start(runner, 1);
        Assert.NotNull(runner.Finished);
    }

    [Fact]
    public async Task AStopAfterTheLastStep_CountsAsCompleted()
    {
        RetrainRunner? runner = null;
        var trainer = new Gated(20) { OnStep = s => { if (s == 20) runner!.Stop(); } };
        runner = Runner(_ => trainer);
        await Start(runner, 1);
        Assert.NotNull(runner.Finished);
        Assert.StartsWith("Step 20 of 20", runner.StatusText);
    }

    [Fact]
    public async Task StopThenStartAgain_RunsAFreshTrainer()
    {
        var first = new Gated(100, gateAfter: 30);
        int made = 0;
        var runner = Runner(_ => made++ == 0 ? first : new Gated(20));
        var run = Start(runner, 1);
        first.InStep.Wait();
        runner.Stop();
        first.Release.Set();
        await run;
        Assert.Null(runner.Finished);
        await Start(runner, 2);
        Assert.Equal(20, runner.Finished!.StepsTaken);
        Assert.StartsWith("Step 20 of 20", runner.StatusText);
    }
}
