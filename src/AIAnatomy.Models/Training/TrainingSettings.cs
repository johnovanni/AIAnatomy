namespace AIAnatomy.Models.Training;

public sealed record TrainingSettings(int BatchSize = 8, double LearningRate = 3e-3, int Seed = 1, double ValidationFraction = 0.1);

/// <summary>One finished training step. <c>StoppedReason</c> is set only on the step that ended training.</summary>
public sealed record TrainingStepReport(int Step, double TrainLoss, double? ValidationLoss, string? StoppedReason);
