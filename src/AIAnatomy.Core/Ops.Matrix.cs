using System.Numerics;

namespace AIAnatomy.Core;

public static partial class Ops
{
    /// <summary>[N,K] · [K,M] → [N,M].</summary>
    public static Tensor MatMul(Tensor a, Tensor b)
    {
        if (a.Shape.Length != 2 || b.Shape.Length != 2 || a.Shape[1] != b.Shape[0])
            throw new ArgumentException($"matmul: shapes [{string.Join(", ", a.Shape)}] and [{string.Join(", ", b.Shape)}] cannot be multiplied.");
        int n = a.Shape[0], k = a.Shape[1], m = b.Shape[1];
        bool parallel = (long)n * k * m > ParallelThreshold;
        var data = new double[n * m];
        var ad = a.Data;
        var bd = b.Data;

        // Output rows are independent and each is summed in the same order, so threading never changes a bit of the result.
        void ForwardRow(int i)
        {
            var row = new Span<double>(data, i * m, m);
            for (int p = 0; p < k; p++)
                AddScaled(row, new ReadOnlySpan<double>(bd, p * m, m), ad[i * k + p]);
        }
        RunRows(n, parallel, ForwardRow);

        return new Tensor(data, new[] { n, m }, "matmul", new[] { a, b }, self =>
        {
            if (self.Grad is not { } g) return;
            if (a.RequiresGrad)
            {
                var ga = a.EnsureGrad();   // dA = dC · B transposed: row i of dA = sum over j of g[i,j] * (column j of B)
                var bt = TransposeData(bd, k, m);
                void RowOfA(int i)
                {
                    var sums = new double[k];
                    for (int j = 0; j < m; j++)
                        AddScaled(sums, new ReadOnlySpan<double>(bt, j * k, k), g[i * m + j]);
                    for (int p = 0; p < k; p++) ga[i * k + p] += sums[p];
                }
                RunRows(n, parallel, RowOfA);
            }
            if (b.RequiresGrad)
            {
                var gb = b.EnsureGrad();   // dB = A transposed · dC; one task per row p of dB
                void RowOfB(int p)
                {
                    var target = new Span<double>(gb, p * m, m);
                    for (int i = 0; i < n; i++)
                        AddScaled(target, new ReadOnlySpan<double>(g, i * m, m), ad[i * k + p]);
                }
                RunRows(k, parallel, RowOfB);
            }
        });
    }

    /// <summary>target[j] += scale * source[j], four or more at a time. Each element is one multiply then one add, exactly as the plain loop does.</summary>
    private static void AddScaled(Span<double> target, ReadOnlySpan<double> source, double scale)
    {
        int j = 0;
        if (Vector.IsHardwareAccelerated && target.Length >= Vector<double>.Count)
        {
            var s = new Vector<double>(scale);
            int last = target.Length - Vector<double>.Count;
            for (; j <= last; j += Vector<double>.Count)
                (new Vector<double>(target.Slice(j)) + new Vector<double>(source.Slice(j)) * s).CopyTo(target.Slice(j));
        }
        for (; j < target.Length; j++) target[j] += scale * source[j];
    }

    private static double[] TransposeData(double[] data, int rows, int cols)
    {
        var t = new double[data.Length];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++) t[j * rows + i] = data[i * cols + j];
        return t;
    }

    private const long ParallelThreshold = 200_000;

    private static void RunRows(int rows, bool parallel, Action<int> body)
    {
        if (parallel) Parallel.For(0, rows, body);
        else for (int r = 0; r < rows; r++) body(r);
    }

    public static Tensor Transpose(Tensor a)
    {
        Require2D(a, "transpose");
        int n = a.Shape[0], d = a.Shape[1];
        var data = new double[a.Size];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < d; j++) data[j * n + i] = a.Data[i * d + j];

        return new Tensor(data, new[] { d, n }, "transpose", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < d; j++) ga[i * d + j] += g[j * n + i];
        });
    }

    /// <summary>Columns [start, start+count) of a [N,D] matrix.</summary>
    public static Tensor SliceColumns(Tensor a, int start, int count)
    {
        Require2D(a, "slice");
        int n = a.Shape[0], d = a.Shape[1];
        if (start < 0 || count < 0 || start + count > d)
            throw new ArgumentOutOfRangeException(nameof(count), $"slice: columns {start}..{start + count} are outside a matrix with {d} columns.");
        var data = new double[n * count];
        for (int i = 0; i < n; i++) Array.Copy(a.Data, i * d + start, data, i * count, count);

        return new Tensor(data, new[] { n, count }, "slice", new[] { a }, self =>
        {
            if (self.Grad is not { } g || !a.RequiresGrad) return;
            var ga = a.EnsureGrad();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < count; j++) ga[i * d + start + j] += g[i * count + j];
        });
    }

    /// <summary>Places [N,d_i] matrices side by side → [N,Σd_i].</summary>
    public static Tensor ConcatColumns(IReadOnlyList<Tensor> parts)
    {
        if (parts.Count == 0) throw new ArgumentException("concat: needs at least one tensor.", nameof(parts));
        foreach (var p in parts) Require2D(p, "concat");
        int n = parts[0].Shape[0];
        if (parts.Any(p => p.Shape[0] != n)) throw new ArgumentException("concat: all parts need the same number of rows.", nameof(parts));
        int total = parts.Sum(p => p.Shape[1]);
        var data = new double[n * total];
        int offset = 0;
        foreach (var p in parts)
        {
            int w = p.Shape[1];
            for (int i = 0; i < n; i++) Array.Copy(p.Data, i * w, data, i * total + offset, w);
            offset += w;
        }

        var parents = parts.ToArray();
        return new Tensor(data, new[] { n, total }, "concat", parents, self =>
        {
            if (self.Grad is not { } g) return;
            int off = 0;
            foreach (var p in parents)
            {
                int w = p.Shape[1];
                if (p.RequiresGrad)
                {
                    var gp = p.EnsureGrad();
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < w; j++) gp[i * w + j] += g[i * total + off + j];
                }
                off += w;
            }
        });
    }

    /// <summary>Gathers rows of a [V,D] table (an embedding lookup). Repeated ids add their gradients.</summary>
    public static Tensor Rows(Tensor table, IReadOnlyList<int> ids)
    {
        Require2D(table, "rows");
        int v = table.Shape[0], d = table.Shape[1];
        var idArray = ids.ToArray();
        var data = new double[idArray.Length * d];
        for (int r = 0; r < idArray.Length; r++)
        {
            if (idArray[r] < 0 || idArray[r] >= v) throw new ArgumentOutOfRangeException(nameof(ids), $"rows: id {idArray[r]} is outside a table with {v} rows.");
            Array.Copy(table.Data, idArray[r] * d, data, r * d, d);
        }

        return new Tensor(data, new[] { idArray.Length, d }, "rows", new[] { table }, self =>
        {
            if (self.Grad is not { } g || !table.RequiresGrad) return;
            var gt = table.EnsureGrad();
            for (int r = 0; r < idArray.Length; r++)
                for (int j = 0; j < d; j++) gt[idArray[r] * d + j] += g[r * d + j];
        });
    }

    private static void Require2D(Tensor t, string op)
    {
        if (t.Shape.Length != 2) throw new ArgumentException($"{op}: needs a 2-D tensor but got shape [{string.Join(", ", t.Shape)}].");
    }
}
