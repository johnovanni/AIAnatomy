using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>Every intermediate result of one forward pass, for the labs to display.</summary>
public sealed class ForwardTrace
{
    public required IReadOnlyList<int> TokenIds { get; init; }
    public required Tensor TokenEmbeddings { get; init; }
    /// <summary>A zero tensor when positions are removed.</summary>
    public required Tensor PositionEmbeddings { get; init; }
    /// <summary>TokenEmbeddings + PositionEmbeddings: what enters the first block.</summary>
    public required Tensor Input { get; init; }
    public required IReadOnlyList<BlockTrace> Blocks { get; init; }
    public required Tensor FinalNorm { get; init; }
    /// <summary>[T,V] one score per vocabulary token for every position.</summary>
    public required Tensor Logits { get; init; }
}

public sealed record ForwardResult(Tensor Logits, ForwardTrace Trace);
