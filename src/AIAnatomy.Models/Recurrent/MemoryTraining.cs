using AIAnatomy.Core;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;

namespace AIAnatomy.Models.Recurrent;

/// <summary>
/// How the memory network is trained: the sizes and steps for "train it again" in the lab and for the shipped network
/// (chosen from the Pretrain speed check, see docs/decisions.md), and its windows of the fables. The lab and Pretrain read
/// only this class.
/// </summary>
public static class MemoryTraining
{
    /// <summary>The Fables model's settings (D20), so the memory network is measured on exactly the same windows.</summary>
    public const int ContextLength = RecordedResults.FablesContextLength;
    public const double ValidationFraction = 0.1;
    public const int WindowSeed = 1;

    /// <summary>Train it again: 21 to 35 ms a step in Release on the build PC (training steps only), so about 53 to 87 s.</summary>
    public const int RetrainHidden = 128;
    public const int RetrainSteps = 2500;

    /// <summary>The shipped network: the Fables model's training budget (3,000 steps of 8 windows of 64) and about its size.</summary>
    public const int ShippedHidden = 256;
    public const int ShippedSteps = RecordedResults.FablesSteps;

    public static RnnConfig Retrain(int vocabularySize) => new(vocabularySize, RetrainHidden);
    public static RnnConfig Shipped(int vocabularySize) => new(vocabularySize, ShippedHidden);

    /// <summary>
    /// Windows of 65 tokens from the fables, built exactly as tools/AIAnatomy.Pretrain built the shipped Fables model's
    /// (normalised text, the shipped model's tokenizer, context 64, validation fraction 0.1, a fresh generator seeded 1),
    /// so the validation windows are the same 32 windows the Fables model was measured on.
    /// </summary>
    public static CorpusBatchSource FablesSource(CharTokenizer tokenizer, string fablesText)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(fablesText);
        var tokens = tokenizer.Encode(TextNormalizer.Normalize(fablesText));
        return new CorpusBatchSource(tokens, ContextLength, tokenizer.VocabularySize, ValidationFraction, new SeededRandom(WindowSeed));
    }
}
