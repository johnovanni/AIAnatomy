namespace AIAnatomy.Models.Training;

/// <summary>Something trained one optimisation step at a time (the image, memory and diffusion networks).</summary>
public interface ISteppedTrainer
{
    int TotalSteps { get; }
    int StepsTaken { get; }

    /// <summary>Takes one optimisation step and returns its training loss.</summary>
    double Step();
}

/// <summary>One report from a stepped run: the steps taken so far, the total, and the latest loss.</summary>
public sealed record TrainingProgress(int Step, int TotalSteps, double Loss);

/// <summary>The shared training loop for stepped trainers.</summary>
public static class SteppedTraining
{
    public const string DivergedMessage = "The training went wrong: the loss stopped being a number. Try again with another seed.";
    private const double DivergenceLimit = 1e4;

    /// <summary>
    /// Runs on a worker thread until the trainer has taken all its steps (returns null), the token is cancelled (returns null,
    /// no further step is taken), or a loss is not finite or is above 1e4 (returns <see cref="DivergedMessage"/>).
    /// Progress is reported on the worker thread every <paramref name="reportEvery"/> steps and after the last step.
    /// </summary>
    public static Task<string?> RunAsync(ISteppedTrainer trainer, IProgress<TrainingProgress> progress,
        CancellationToken token, int reportEvery = 10)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentOutOfRangeException.ThrowIfLessThan(reportEvery, 1);
        return Task.Run<string?>(() =>
        {
            while (trainer.StepsTaken < trainer.TotalSteps && !token.IsCancellationRequested)
            {
                double loss = trainer.Step();
                if (!double.IsFinite(loss) || loss > DivergenceLimit) return DivergedMessage;
                int step = trainer.StepsTaken;
                if (step % reportEvery == 0 || step >= trainer.TotalSteps)
                    progress.Report(new TrainingProgress(step, trainer.TotalSteps, loss));
            }
            return null;
        });
    }
}
