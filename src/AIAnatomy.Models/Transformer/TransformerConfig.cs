namespace AIAnatomy.Models.Transformers;

public enum PositionKind { Learned, Sinusoidal }

/// <summary>The shape of a transformer. All sizes are counted in numbers, not bytes.</summary>
public sealed record TransformerConfig(
    int VocabularySize,
    int ContextLength = 64,
    int ModelWidth = 64,
    int Layers = 2,
    int Heads = 4,
    int FeedForwardWidth = 256,
    PositionKind Positions = PositionKind.Learned,
    bool UseFeedForward = true)
{
    /// <summary>How many numbers each attention head works with.</summary>
    public int HeadWidth => ModelWidth / Heads;

    /// <summary>Total number of learnable numbers; equals the summed size of <c>Transformer.Parameters()</c>.</summary>
    public long CountParameters()
    {
        long d = ModelWidth, v = VocabularySize, hw = HeadWidth;
        long tokens = v * d;
        long positions = Positions == PositionKind.Learned ? (long)ContextLength * d : 0;
        long attention = Layers * (Heads * 3 * (d * hw + hw) + d * d + d);
        long feedForward = UseFeedForward ? Layers * (d * FeedForwardWidth + FeedForwardWidth + (long)FeedForwardWidth * d + d) : 0;
        long norms = (UseFeedForward ? 2L * Layers + 1 : Layers + 1L) * 2 * d;
        long output = d * v + v;
        return tokens + positions + attention + feedForward + norms + output;
    }
}
