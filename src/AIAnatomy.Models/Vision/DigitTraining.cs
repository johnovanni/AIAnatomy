namespace AIAnatomy.Models.Vision;

/// <summary>
/// How the image network is trained: the sizes and steps for "train it again" in the lab and for the shipped network
/// (chosen from the Pretrain speed check, see docs/decisions.md). Both read the bundled 8x8 digits with batch 32 and
/// learning rate 1e-3 (the <see cref="DigitTrainer"/> defaults). The lab and Pretrain read only this class.
/// </summary>
public static class DigitTraining
{
    /// <summary>
    /// Train it again: 0.17 to 0.28 ms per image in Release on the build PC (5.4 to 8.8 ms a step of 32; short timings of
    /// 8 or 50 steps are the slow end), so 2,000 steps (about 17 passes over the 3,760 training images) take about 11 to
    /// 18 s. Test accuracy on the 1,740 test images after 2,000 steps: 0.965, 0.970 and 0.971 for seeds 1, 2 and 3.
    /// </summary>
    public static DigitNetConfig Retrain { get; } = new(ImageSize: 8, Filters1: 16, Filters2: 32, Hidden: 128);
    public const int RetrainSteps = 2_000;

    /// <summary>
    /// The shipped network: the same network, trained twice as long offline (about 22 to 35 s). Test accuracy after 4,000
    /// steps: 0.980, 0.976 and 0.975 for seeds 1, 2 and 3.
    /// </summary>
    public static DigitNetConfig Shipped { get; } = new(ImageSize: 8, Filters1: 16, Filters2: 32, Hidden: 128);
    public const int ShippedSteps = 4_000;
}
