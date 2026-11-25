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
    /// 1,000 steps or more), so about 66 to 84 s, leaving room under 2 minutes for the lab's drawing. Mean distance to the
    /// shape (500 samples, seed 1, 100 sampling steps): Spiral 0.069, Circle 0.021, Two moons 0.059, Letter S 0.046.
    /// </summary>
    public static DiffusionConfig Retrain { get; } = new(Hidden: 128);
    public const int RetrainSteps = 20_000;

    /// <summary>
    /// The shipped network, trained offline (about 4 minutes): the same size, more steps. Mean distance to the shape
    /// (500 samples, seed 1, 100 sampling steps): Spiral 0.032, Circle 0.008, Two moons 0.017, Letter S 0.016, every one
    /// under 0.04, with coverage at radius 0.05 of 0.96 to 1.00 (docs/decisions.md, Task 10 review).
    /// </summary>
    public static DiffusionConfig Shipped { get; } = new(Hidden: 128);
    public const int ShippedSteps = 60_000;
}
