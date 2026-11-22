using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>The result of attention: Concatenated is [T,D] before the output projection, Output is after it.</summary>
public sealed record AttentionResult(Tensor Output, Tensor Concatenated, IReadOnlyList<HeadTrace> Heads);

/// <summary>Several independent heads whose outputs are joined side by side and mixed by an output matrix [D,D].</summary>
public sealed class MultiHeadAttention : Module
{
    private readonly List<AttentionHead> _heads = new();
    private readonly Parameter _output, _outputBias;

    public MultiHeadAttention(string name, int modelWidth, int heads, SeededRandom rng)
    {
        int headWidth = modelWidth / heads;
        for (int h = 0; h < heads; h++) _heads.Add(new AttentionHead($"{name}.heads.{h}", modelWidth, headWidth, rng));
        _output = Weights(name + ".output", rng, modelWidth, modelWidth);
        _outputBias = Constant(name + ".output_bias", modelWidth, 0);
    }

    public IReadOnlyList<AttentionHead> Heads => _heads;

    /// <param name="disabledHeads">Indices of heads (within this layer) whose output is replaced by zeros.</param>
    public AttentionResult Forward(Tensor x, IReadOnlySet<int>? disabledHeads = null)
    {
        var traces = new List<HeadTrace>(_heads.Count);
        for (int h = 0; h < _heads.Count; h++)
            traces.Add(_heads[h].Forward(x, disabledHeads?.Contains(h) == true));
        var concatenated = Ops.ConcatColumns(traces.Select(t => t.Output).ToList());
        var output = Ops.Add(Ops.MatMul(concatenated, _output.Value), _outputBias.Value);
        return new AttentionResult(output, concatenated, traces);
    }

    public override IEnumerable<Parameter> Parameters()
    {
        foreach (var head in _heads)
            foreach (var p in head.Parameters()) yield return p;
        yield return _output;
        yield return _outputBias;
    }
}
