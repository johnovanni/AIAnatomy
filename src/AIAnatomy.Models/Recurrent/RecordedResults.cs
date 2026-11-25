namespace AIAnatomy.Models.Recurrent;

/// <summary>
/// The shipped Fables transformer's training results, as recorded when it was built (D20 in docs/decisions.md). It is
/// not retrained in the memory lab; these are shown beside the memory network's own results and labelled as recorded.
/// </summary>
public static class RecordedResults
{
    public const double FablesValidationLoss = 1.711;
    public const int FablesSteps = 3000;
    public const double FablesSeconds = 517;
    public const int FablesBatchSize = 8;
    public const int FablesContextLength = 64;

    /// <summary>Characters of training text read per second: steps times batch times context, over the seconds (about 2,971).</summary>
    public static double FablesCharactersPerSecond => FablesSteps * FablesBatchSize * FablesContextLength / FablesSeconds;
}
