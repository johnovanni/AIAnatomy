namespace AIAnatomy.Core;

public static partial class Ops
{
    /// <summary>[N,K] · [K,M] → [N,M].</summary>
    public static Tensor MatMul(Tensor a, Tensor b)
    {
        if (a.Shape.Length != 2 || b.Shape.Length != 2 || a.Shape[1] != b.Shape[0])
            throw new ArgumentException($"matmul: shapes [{string.Join(", ", a.Shape)}] and [{string.Join(", ", b.Shape)}] cannot be multiplied.");
        int n = a.Shape[0], k = a.Shape[1], m = b.Shape[1];
        var data = new double[n * m];
        for (int i = 0; i < n; i++)
            for (int p = 0; p < k; p++)
            {
                double av = a.Data[i * k + p];
                for (int j = 0; j < m; j++) data[i * m + j] += av * b.Data[p * m + j];
            }

        return new Tensor(data, new[] { n, m }, "matmul", new[] { a, b }, self =>
        {
            if (self.Grad is not { } g) return;
            if (a.RequiresGrad)
            {
                var ga = a.EnsureGrad();   // dA = dC · B transposed
                for (int i = 0; i < n; i++)
                    for (int p = 0; p < k; p++)
                    {
                        double sum = 0;
                        for (int j = 0; j < m; j++) sum += g[i * m + j] * b.Data[p * m + j];
                        ga[i * k + p] += sum;
                    }
            }
            if (b.RequiresGrad)
            {
                var gb = b.EnsureGrad();   // dB = A transposed · dC
                for (int i = 0; i < n; i++)
                    for (int p = 0; p < k; p++)
                    {
                        double av = a.Data[i * k + p];
                        for (int j = 0; j < m; j++) gb[p * m + j] += av * g[i * m + j];
                    }
            }
        });
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
