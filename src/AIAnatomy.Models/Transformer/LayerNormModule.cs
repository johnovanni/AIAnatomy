using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>Layer normalisation with learnable gamma (starts 1) and beta (starts 0).</summary>
public sealed class LayerNormModule : Module
{
    public LayerNormModule(string name, int width)
    {
        Gamma = Constant(name + ".gamma", width, 1.0);
        Beta = Constant(name + ".beta", width, 0.0);
    }

    public Parameter Gamma { get; }
    public Parameter Beta { get; }

    public Tensor Forward(Tensor x) => Ops.LayerNormRows(x, Gamma.Value, Beta.Value);

    public override IEnumerable<Parameter> Parameters() { yield return Gamma; yield return Beta; }
}
