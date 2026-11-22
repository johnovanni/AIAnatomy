using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>A part of the transformer that owns learnable parameters. Names are hierarchical and the order is fixed.</summary>
public abstract class Module
{
    public abstract IEnumerable<Parameter> Parameters();

    protected static Parameter Weights(string name, SeededRandom rng, int rows, int cols) =>
        new(name, Tensor.RandomNormal(rng, 0.02, rows, cols).AsParameter());

    protected static Parameter Constant(string name, int length, double value)
    {
        var t = Tensor.Zeros(length);
        Array.Fill(t.Data, value);
        return new Parameter(name, t.AsParameter());
    }
}
