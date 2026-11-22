namespace AIAnatomy.Core;

/// <summary>
/// A row-major array of doubles that remembers how it was computed, so gradients can flow back
/// through the operations that produced it (reverse-mode automatic differentiation).
/// </summary>
public sealed class Tensor
{
    private readonly Action<Tensor>? _backward;

    internal Tensor(double[] data, int[] shape, string op, Tensor[] parents, Action<Tensor> backward)
        : this(data, shape, op, parents, backward, parents.Any(p => p.RequiresGrad))
    {
    }

    private Tensor(double[] data, int[] shape, string op, Tensor[] parents, Action<Tensor>? backward, bool requiresGrad)
    {
        Data = data;
        Shape = shape;
        Size = data.Length;
        Op = op;
        Parents = parents;
        _backward = backward;
        RequiresGrad = requiresGrad;
    }

    public int[] Shape { get; }
    public int Size { get; }
    public double[] Data { get; }
    /// <summary>Null until <see cref="Backward"/> reaches this tensor.</summary>
    public double[]? Grad { get; private set; }
    public bool RequiresGrad { get; }
    public string Op { get; }
    public IReadOnlyList<Tensor> Parents { get; }
    public string? Label { get; set; }

    [System.Runtime.CompilerServices.IndexerName("Element")]
    public double this[params int[] index]
    {
        get => Data[Offset(index)];
        set => Data[Offset(index)] = value;
    }

    public static Tensor Zeros(params int[] shape) => Leaf(new double[SizeOf(shape)], shape, false);

    /// <summary>Creates a tensor from a copy of <paramref name="data"/>.</summary>
    public static Tensor FromArray(double[] data, params int[] shape)
    {
        if (data.Length != SizeOf(shape))
            throw new ArgumentException($"Data has {data.Length} values but shape [{string.Join(", ", shape)}] needs {SizeOf(shape)}.", nameof(data));
        return Leaf((double[])data.Clone(), shape, false);
    }

    public static Tensor Scalar(double value) => Leaf(new[] { value }, new[] { 1 }, false);

    public static Tensor RandomNormal(SeededRandom rng, double std, params int[] shape)
    {
        var data = new double[SizeOf(shape)];
        for (int i = 0; i < data.Length; i++) data[i] = rng.NextGaussian() * std;
        return Leaf(data, shape, false);
    }

    /// <summary>A copy of this tensor that takes part in gradient computation.</summary>
    public Tensor AsParameter() => Leaf((double[])Data.Clone(), (int[])Shape.Clone(), true);

    public double Item()
    {
        if (Size != 1) throw new InvalidOperationException($"Item() needs a 1-element tensor, but this one has {Size} elements.");
        return Data[0];
    }

    /// <summary>Seeds this scalar's gradient with 1 and accumulates gradients into every ancestor.</summary>
    public void Backward()
    {
        if (Size != 1) throw new InvalidOperationException($"Backward() needs a 1-element tensor, but this one has {Size} elements.");

        var order = new List<Tensor>();
        var seen = new HashSet<Tensor>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<(Tensor Node, bool Expanded)>();
        stack.Push((this, false));
        while (stack.Count > 0)
        {
            var (node, expanded) = stack.Pop();
            if (expanded) { order.Add(node); continue; }
            if (!seen.Add(node)) continue;
            stack.Push((node, true));
            foreach (var p in node.Parents) if (!seen.Contains(p)) stack.Push((p, false));
        }

        EnsureGrad()[0] += 1.0;
        for (int i = order.Count - 1; i >= 0; i--)
            if (order[i].RequiresGrad) order[i]._backward?.Invoke(order[i]);
    }

    public void ZeroGrad() => Grad = null;

    /// <summary>A copy of row <paramref name="r"/> of a 2-D tensor.</summary>
    public double[] Row(int r)
    {
        if (Shape.Length != 2) throw new InvalidOperationException("Row() needs a 2-D tensor.");
        if (r < 0 || r >= Shape[0]) throw new ArgumentOutOfRangeException(nameof(r));
        var row = new double[Shape[1]];
        Array.Copy(Data, r * Shape[1], row, 0, Shape[1]);
        return row;
    }

    /// <summary>The gradient buffer, created (all zeros) on first use. Used by the operations' backward steps.</summary>
    internal double[] EnsureGrad() => Grad ??= new double[Size];

    private static Tensor Leaf(double[] data, int[] shape, bool requiresGrad) =>
        new(data, shape, "leaf", Array.Empty<Tensor>(), null, requiresGrad);

    private static int SizeOf(int[] shape)
    {
        int size = 1;
        foreach (int d in shape)
        {
            if (d < 0) throw new ArgumentException("Shape dimensions cannot be negative.", nameof(shape));
            size *= d;
        }
        return size;
    }

    private int Offset(int[] index)
    {
        if (index.Length != Shape.Length)
            throw new ArgumentException($"Expected {Shape.Length} indices but got {index.Length}.", nameof(index));
        int offset = 0;
        for (int i = 0; i < index.Length; i++)
        {
            if (index[i] < 0 || index[i] >= Shape[i]) throw new IndexOutOfRangeException($"Index {index[i]} is outside dimension {i} of size {Shape[i]}.");
            offset = offset * Shape[i] + index[i];
        }
        return offset;
    }
}
