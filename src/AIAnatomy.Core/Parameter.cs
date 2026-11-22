namespace AIAnatomy.Core;

/// <summary>A named, learnable tensor. Optimisers update its <see cref="Value"/> in place.</summary>
public sealed class Parameter
{
    public Parameter(string name, Tensor value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public Tensor Value { get; }
}
