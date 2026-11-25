using AIAnatomy.Models.Training;

namespace AIAnatomy.Models.Tests;

public class SteppedTrainingTests
{
    private sealed class Fake(int total, Func<int, double>? loss = null, Action<int>? onStep = null) : ISteppedTrainer
    {
        public int TotalSteps => total;
        public int StepsTaken { get; private set; }
        public double Step()
        {
            StepsTaken++;
            onStep?.Invoke(StepsTaken);
            return loss?.Invoke(StepsTaken) ?? 1.0 / StepsTaken;
        }
    }

    private sealed class Collect : IProgress<TrainingProgress>
    {
        public List<TrainingProgress> Reports { get; } = [];
        public void Report(TrainingProgress value) => Reports.Add(value);
    }

    [Fact]
    public async Task RunsToTotalSteps_ReportingEveryNAndTheLast()
    {
        var trainer = new Fake(25);
        var progress = new Collect();
        var result = await SteppedTraining.RunAsync(trainer, progress, CancellationToken.None, reportEvery: 10);
        Assert.Null(result);
        Assert.Equal(25, trainer.StepsTaken);
        Assert.Equal([10, 20, 25], progress.Reports.Select(r => r.Step));
        Assert.All(progress.Reports, r => Assert.Equal(25, r.TotalSteps));
        Assert.Equal(1.0 / 25, progress.Reports[^1].Loss);
    }

    [Fact]
    public async Task ExactMultiple_ReportsTheLastOnlyOnce()
    {
        var progress = new Collect();
        await SteppedTraining.RunAsync(new Fake(20), progress, CancellationToken.None, reportEvery: 10);
        Assert.Equal([10, 20], progress.Reports.Select(r => r.Step));
    }

    [Fact]
    public async Task Cancellation_ReturnsNullAndTakesNoFurtherStep()
    {
        using var cts = new CancellationTokenSource();
        var trainer = new Fake(100, onStep: s => { if (s == 7) cts.Cancel(); });
        var result = await SteppedTraining.RunAsync(trainer, new Collect(), cts.Token);
        Assert.Null(result);
        Assert.Equal(7, trainer.StepsTaken);
    }

    [Fact]
    public async Task AlreadyCancelled_TakesNoStep()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var trainer = new Fake(100);
        Assert.Null(await SteppedTraining.RunAsync(trainer, new Collect(), cts.Token));
        Assert.Equal(0, trainer.StepsTaken);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(2e4)]
    public async Task BadLoss_ReturnsDivergedMessage(double bad)
    {
        var trainer = new Fake(100, s => s == 3 ? bad : 0.5);
        var result = await SteppedTraining.RunAsync(trainer, new Collect(), CancellationToken.None);
        Assert.Equal(SteppedTraining.DivergedMessage, result);
        Assert.Equal(3, trainer.StepsTaken);
    }
}
