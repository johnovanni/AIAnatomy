using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>What one attention head computed for one sequence of T tokens.</summary>
public sealed class HeadTrace
{
    public required Tensor Queries { get; init; }
    public required Tensor Keys { get; init; }
    public required Tensor Values { get; init; }
    /// <summary>[T,T] query-key scores, scaled by 1/sqrt(HeadWidth) and causally masked (-infinity above the diagonal).</summary>
    public required Tensor Scores { get; init; }
    /// <summary>[T,T] softmax of Scores; each row sums to 1.</summary>
    public required Tensor Weights { get; init; }
    /// <summary>[T,HeadWidth] weighted mix of the values. All zeros when the head is switched off.</summary>
    public required Tensor Output { get; init; }
}

/// <summary>One attention head with its own query, key and value matrices [D, HeadWidth] and biases.</summary>
public sealed class AttentionHead : Module
{
    private readonly Parameter _query, _queryBias, _key, _keyBias, _value, _valueBias;
    private readonly int _headWidth;

    public AttentionHead(string name, int modelWidth, int headWidth, SeededRandom rng)
    {
        _headWidth = headWidth;
        _query = Weights(name + ".query", rng, modelWidth, headWidth);
        _key = Weights(name + ".key", rng, modelWidth, headWidth);
        _value = Weights(name + ".value", rng, modelWidth, headWidth);
        _queryBias = Constant(name + ".query_bias", headWidth, 0);
        _keyBias = Constant(name + ".key_bias", headWidth, 0);
        _valueBias = Constant(name + ".value_bias", headWidth, 0);
    }

    public HeadTrace Forward(Tensor x, bool disabled = false)
    {
        var q = Ops.Add(Ops.MatMul(x, _query.Value), _queryBias.Value);
        var k = Ops.Add(Ops.MatMul(x, _key.Value), _keyBias.Value);
        var v = Ops.Add(Ops.MatMul(x, _value.Value), _valueBias.Value);
        var scores = Ops.CausalMask(Ops.Scale(Ops.MatMul(q, Ops.Transpose(k)), 1.0 / Math.Sqrt(_headWidth)));
        var weights = Ops.SoftmaxRows(scores);
        var output = disabled ? Tensor.Zeros(x.Shape[0], _headWidth) : Ops.MatMul(weights, v);
        return new HeadTrace { Queries = q, Keys = k, Values = v, Scores = scores, Weights = weights, Output = output };
    }

    public override IEnumerable<Parameter> Parameters()
    {
        yield return _query; yield return _queryBias;
        yield return _key; yield return _keyBias;
        yield return _value; yield return _valueBias;
    }
}
