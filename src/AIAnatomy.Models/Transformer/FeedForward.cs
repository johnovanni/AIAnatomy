using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>Two layers per token: [D,FF] + bias, GELU, [FF,D] + bias.</summary>
public sealed class FeedForward : Module
{
    private readonly Parameter _in, _inBias, _out, _outBias;

    public FeedForward(string name, int modelWidth, int hiddenWidth, SeededRandom rng)
    {
        _in = Weights(name + ".in", rng, modelWidth, hiddenWidth);
        _inBias = Constant(name + ".in_bias", hiddenWidth, 0);
        _out = Weights(name + ".out", rng, hiddenWidth, modelWidth);
        _outBias = Constant(name + ".out_bias", modelWidth, 0);
    }

    /// <summary>Hidden is [T,FF] after GELU; Output is [T,D].</summary>
    public (Tensor Hidden, Tensor Output) ForwardWithHidden(Tensor x)
    {
        var hidden = Ops.Gelu(Ops.Add(Ops.MatMul(x, _in.Value), _inBias.Value));
        return (hidden, Ops.Add(Ops.MatMul(hidden, _out.Value), _outBias.Value));
    }

    public Tensor Forward(Tensor x) => ForwardWithHidden(x).Output;

    public override IEnumerable<Parameter> Parameters()
    {
        yield return _in; yield return _inBias; yield return _out; yield return _outBias;
    }
}
