using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>Everything one block computed. Fields for the feed-forward part are null when the model has none.</summary>
public sealed class BlockTrace
{
    public required Tensor ResidualIn { get; init; }
    public required Tensor Norm1Out { get; init; }
    /// <summary>[T,D] heads joined side by side, before the output projection.</summary>
    public required Tensor ConcatenatedHeads { get; init; }
    /// <summary>Attention result after the output projection.</summary>
    public required Tensor AttentionOut { get; init; }
    /// <summary>ResidualIn + AttentionOut.</summary>
    public required Tensor ResidualMid { get; init; }
    public Tensor? Norm2Out { get; init; }
    /// <summary>Feed-forward hidden layer after GELU.</summary>
    public Tensor? FeedForwardHidden { get; init; }
    public Tensor? FeedForwardOut { get; init; }
    /// <summary>The block's result. Equals ResidualMid when there is no feed-forward part.</summary>
    public required Tensor ResidualOut { get; init; }
    public required IReadOnlyList<HeadTrace> Heads { get; init; }
}

/// <summary>Pre-layer-norm block: x + Attention(LN1(x)), then (if present) that + FeedForward(LN2(.)).</summary>
public sealed class TransformerBlock : Module
{
    private readonly LayerNormModule _norm1;
    private readonly MultiHeadAttention _attention;
    private readonly LayerNormModule? _norm2;
    private readonly FeedForward? _feedForward;

    public TransformerBlock(string name, TransformerConfig config, SeededRandom rng)
    {
        _norm1 = new LayerNormModule(name + ".ln1", config.ModelWidth);
        _attention = new MultiHeadAttention(name + ".attn", config.ModelWidth, config.Heads, rng);
        if (config.UseFeedForward)
        {
            _norm2 = new LayerNormModule(name + ".ln2", config.ModelWidth);
            _feedForward = new FeedForward(name + ".ff", config.ModelWidth, config.FeedForwardWidth, rng);
        }
    }

    public MultiHeadAttention Attention => _attention;

    public BlockTrace Forward(Tensor x, IReadOnlySet<int>? disabledHeads = null)
    {
        var norm1 = _norm1.Forward(x);
        var attention = _attention.Forward(norm1, disabledHeads);
        var mid = Ops.Add(x, attention.Output);
        if (_norm2 is null || _feedForward is null)
            return new BlockTrace
            {
                ResidualIn = x, Norm1Out = norm1, ConcatenatedHeads = attention.Concatenated, AttentionOut = attention.Output,
                ResidualMid = mid, ResidualOut = mid, Heads = attention.Heads,
            };

        var norm2 = _norm2.Forward(mid);
        var (hidden, ffOut) = _feedForward.ForwardWithHidden(norm2);
        return new BlockTrace
        {
            ResidualIn = x, Norm1Out = norm1, ConcatenatedHeads = attention.Concatenated, AttentionOut = attention.Output,
            ResidualMid = mid, Norm2Out = norm2, FeedForwardHidden = hidden, FeedForwardOut = ffOut,
            ResidualOut = Ops.Add(mid, ffOut), Heads = attention.Heads,
        };
    }

    public override IEnumerable<Parameter> Parameters()
    {
        foreach (var p in _norm1.Parameters()) yield return p;
        foreach (var p in _attention.Parameters()) yield return p;
        if (_norm2 is not null) foreach (var p in _norm2.Parameters()) yield return p;
        if (_feedForward is not null) foreach (var p in _feedForward.Parameters()) yield return p;
    }

    internal IEnumerable<Parameter> AttentionParameters() => _attention.Parameters();
    internal IEnumerable<Parameter> FeedForwardParameters() => _feedForward?.Parameters() ?? Enumerable.Empty<Parameter>();
    internal IEnumerable<Parameter> NormParameters() =>
        _norm1.Parameters().Concat(_norm2?.Parameters() ?? Enumerable.Empty<Parameter>());
}
