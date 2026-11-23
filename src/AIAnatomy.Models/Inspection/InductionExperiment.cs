using AIAnatomy.Core;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Inspection;

/// <summary>One training sequence and where its repeated segment sits.</summary>
public sealed record InductionSequence(IReadOnlyList<int> Tokens, int FirstStart, int SecondStart, int Length);

/// <summary>
/// The induction-head experiment: a tiny attention-only transformer learns to continue sequences it has already seen
/// (A B C ... A B -> C), which it can only do by finding the earlier copy and attending to the token after it.
/// </summary>
public static class InductionExperiment
{
    /// <summary>How many steps the lesson trains for.</summary>
    public const int DefaultSteps = 1500;

    private const int MinSegment = 4;
    private const int MaxSegment = 8;
    private const int SequenceLength = 17;   // 16 inputs + 1 final target
    private const int ContextLast = 15;
    private const int Letters = 15;
    private const int ValidationSequences = 32;
    private const int ValidationSeed = 424242;

    public static TransformerConfig Config { get; } = new(
        VocabularySize: 16, ContextLength: 16, ModelWidth: 32, Layers: 2, Heads: 2, UseFeedForward: false);

    /// <summary>The letters A..O: ids 1..15 (id 0 is unknown).</summary>
    public static CharTokenizer Tokenizer { get; } = CharTokenizer.FromVocabulary("ABCDEFGHIJKLMNO".ToCharArray());

    /// <summary>
    /// 17 tokens: a random prefix, a segment of L distinct letters (L from 4 to 8), the same segment again, then random filler.
    /// Prefix and filler never use the segment's letters, so the second copy can only be predicted by finding the first one.
    /// The gap between the copies (L) and the prefix length vary, so no fixed "look k back" rule works.
    /// </summary>
    public static InductionSequence MakeSequence(SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int length = MinSegment + rng.NextInt(MaxSegment - MinSegment + 1);
        int prefix = rng.NextInt(SequenceLength - 2 * length + 1);

        var letters = Enumerable.Range(1, Letters).ToList();
        var segment = new int[length];
        for (int i = 0; i < length; i++)
        {
            int pick = rng.NextInt(letters.Count);
            segment[i] = letters[pick];
            letters.RemoveAt(pick);   // what is left are the letters outside the segment
        }

        var tokens = new int[SequenceLength];
        for (int i = 0; i < prefix; i++) tokens[i] = letters[rng.NextInt(letters.Count)];
        for (int i = 0; i < length; i++) tokens[prefix + i] = tokens[prefix + length + i] = segment[i];
        for (int i = prefix + 2 * length; i < SequenceLength; i++) tokens[i] = letters[rng.NextInt(letters.Count)];
        return new InductionSequence(tokens, prefix, prefix + length, length);
    }

    /// <summary>
    /// Mean attention weight from each second-copy position (SecondStart + j) to the token just after its match in the first
    /// copy (FirstStart + j + 1), over positions that fit in the context. Near 1 means an induction head.
    /// <paramref name="layer"/> and <paramref name="head"/> are 0-based.
    /// </summary>
    public static double InductionScore(Transformer m, int layer, int head, int seed, int samples = 50)
        => MeanOverSamples(m, layer, head, seed, samples, (weights, seq) =>
        {
            double total = 0;
            int count = 0;
            for (int j = 0; j < seq.Length; j++)
            {
                int i = seq.SecondStart + j;
                if (i > ContextLast) break;
                total += weights[i, seq.FirstStart + j + 1];
                count++;
            }
            return (total, count);
        });

    /// <summary>
    /// Mean attention weight from each position to the one before it (positions 1..15). Induction needs a head like this in an
    /// earlier layer, which copies "what came before me" into each token (Olsson et al. 2022).
    /// </summary>
    public static double PreviousTokenScore(Transformer m, int layer, int head, int seed, int samples = 50)
        => MeanOverSamples(m, layer, head, seed, samples, (weights, _) =>
        {
            double total = 0;
            for (int i = 1; i <= ContextLast; i++) total += weights[i, i - 1];
            return (total, ContextLast);
        });

    private static double MeanOverSamples(Transformer m, int layer, int head, int seed, int samples,
        Func<Tensor, InductionSequence, (double Total, int Count)> measure)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (samples < 1) throw new ArgumentOutOfRangeException(nameof(samples));
        var rng = new SeededRandom(seed);
        double total = 0;
        for (int s = 0; s < samples; s++)
        {
            var seq = MakeSequence(rng);
            var weights = m.Forward(seq.Tokens.Take(ContextLast + 1).ToArray()).Trace.Blocks[layer].Heads[head].Weights;
            var (sum, count) = measure(weights, seq);
            total += sum / count;
        }
        return total / samples;
    }

    /// <summary>
    /// Trains a fresh model on endless fresh sequences; the loss falls once an induction head forms.
    /// <paramref name="modelCreated"/> receives the model before training starts, so a caller can look at it while it learns
    /// (only from inside <paramref name="p"/>'s Report, which runs between steps; the model changes during each step).
    /// </summary>
    public static async Task<Transformer> TrainAsync(int steps, int seed, IProgress<TrainingStepReport> p, CancellationToken ct,
        Action<Transformer>? modelCreated = null)
    {
        var model = new Transformer(Config, new SeededRandom(seed));
        modelCreated?.Invoke(model);
        var trainer = new Trainer(model, new SequenceSource(), new TrainingSettings(BatchSize: 16, LearningRate: 3e-3, Seed: seed));
        await trainer.RunAsync(steps, p, ct).ConfigureAwait(false);
        return model;
    }

    private sealed class SequenceSource : IBatchSource
    {
        public SequenceSource()
        {
            var rng = new SeededRandom(ValidationSeed);
            ValidationWindows = Enumerable.Range(0, ValidationSequences).Select(_ => (IReadOnlyList<int>)MakeSequence(rng).Tokens).ToArray();
        }

        public IReadOnlyList<IReadOnlyList<int>> ValidationWindows { get; }

        public IReadOnlyList<int> NextTrainingWindow(SeededRandom rng) => MakeSequence(rng).Tokens;
    }
}
