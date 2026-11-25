namespace AIAnatomy.Models.Recurrent;

/// <summary>
/// The shipped Fables transformer's training results, as recorded when it was built (D20 in docs/decisions.md). It is
/// not retrained in the memory lab; these are shown beside the memory network's own results and labelled as recorded.
/// <see cref="FablesSeconds"/> is the wall time of Pretrain's whole training loop (its "done" line, timed around
/// <c>Trainer.RunAsync</c>), which includes the validation pass at step 1 and every 25 steps; it is not the training steps
/// alone (loading the text and saving the file are outside it). A speed shown beside it must be timed the same way.
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
