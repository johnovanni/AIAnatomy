using AIAnatomy.Core;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Inspection;

/// <summary>The logit lens: read the residual stream after every layer as if it were the final one.</summary>
public static class LogitLens
{
    /// <summary>
    /// Entry 0 is after the embeddings; entry i is after block i. Each entry is
    /// softmax(OutputWeights · FinalNorm(residual) + bias) at <paramref name="position"/>, so the last entry is the model's own prediction.
    /// </summary>
    public static IReadOnlyList<double[]> PerLayerProbabilities(Transformer m, ForwardTrace t, int position)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(t);
        if (position < 0 || position >= t.TokenIds.Count)
            throw new ArgumentOutOfRangeException(nameof(position), $"Position {position} is outside the {t.TokenIds.Count} tokens.");

        var stages = new List<Tensor>(t.Blocks.Count + 1) { t.Input };
        foreach (var block in t.Blocks) stages.Add(block.ResidualOut);

        var result = new List<double[]>(stages.Count);
        foreach (var residual in stages)
        {
            var normed = m.FinalNorm.Forward(residual);
            var logits = Ops.Add(Ops.MatMul(normed, m.OutputWeights.Value), m.OutputBias.Value);
            result.Add(Ops.SoftmaxRows(logits).Row(position));
        }
        return result;
    }
}
