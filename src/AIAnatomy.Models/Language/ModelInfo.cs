namespace AIAnatomy.Models.Language;

/// <summary>What a saved model remembers about itself. <c>ValidationLoss</c> is null when it was never measured.</summary>
public sealed record ModelInfo(string Name, string TrainingTextName, int StepsTrained, double? ValidationLoss, int Seed, bool IsShipped);
