using AIAnatomy.Core;

namespace AIAnatomy.Models.Toys;

/// <summary>L = (a·b + c)·f on scalar tensors, so backpropagation can be shown one node at a time.</summary>
public sealed class ExpressionGraphDemo
{
    private readonly double _a, _b, _c, _f;

    public ExpressionGraphDemo(double a, double b, double c, double f)
    {
        _a = a; _b = b; _c = c; _f = f;
        Nodes = Build();
    }

    /// <summary>Leaves first, then each result, in topological order: a, b, c, f, e = a·b, d = e + c, L = d·f.</summary>
    public IReadOnlyList<Tensor> Nodes { get; private set; }

    /// <summary>Rebuilds the graph, runs the forward pass and then Backward. Calling it again gives the same gradients.</summary>
    public void Run()
    {
        Nodes = Build();
        Nodes[^1].Backward();
    }

    private IReadOnlyList<Tensor> Build()
    {
        var a = Leaf(_a, "a");
        var b = Leaf(_b, "b");
        var c = Leaf(_c, "c");
        var f = Leaf(_f, "f");
        var e = Ops.Mul(a, b); e.Label = "e = a·b";
        var d = Ops.Add(e, c); d.Label = "d = e + c";
        var l = Ops.Mul(d, f); l.Label = "L = d·f";
        return new[] { a, b, c, f, e, d, l };
    }

    private static Tensor Leaf(double value, string label)
    {
        var t = Tensor.Scalar(value).AsParameter();
        t.Label = label;
        return t;
    }
}
