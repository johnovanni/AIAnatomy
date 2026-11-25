namespace AIAnatomy.Models.Diffusion;

/// <summary>
/// How the diffusion network is trained: the sizes and steps for "train it again" in the lab and for the shipped network
/// (chosen from the Pretrain speed check, see docs/decisions.md). Both use the full 100-step schedule, batch 128 and
/// learning rate 1e-3 (the <see cref="DiffusionTrainer"/> defaults). The lab and Pretrain read only this class.
/// </summary>
public static class DiffusionTraining
{
    /// <summary>
    /// Train it again: 3.3 to 4.2 ms a step in Release on the build PC at hidden size 128 (training steps only, runs of
    /// 1,000 steps or more), so about 66 to 84 s, leaving room under 2 minutes for the lab's drawing. On the spiral this
    /// reached a mean distance of 0.078 to the shape (0.067 after 30,000 steps).
    /// </summary>
    public static DiffusionConfig Retrain { get; } = new(Hidden: 128);
    public const int RetrainSteps = 20_000;

    /// <summary>
    /// The shipped network, trained offline (about 4 minutes): the same size, more steps. On the spiral 60,000 steps
    /// reached 0.046, better than hidden size 256 after 30,000 steps (0.056, three times slower per step).
    /// </summary>
    public static DiffusionConfig Shipped { get; } = new(Hidden: 128);
    public const int ShippedSteps = 60_000;
}
