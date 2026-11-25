namespace AIAnatomy.Core;

public static partial class Ops
{
    private static string Dims(int[] shape) => $"[{string.Join(", ", shape)}]";

    /// <summary>
    /// 2-D convolution, stride 1, zero padding. input [N,C,H,W]; weights [F, C*k*k] (row f is filter f, laid out c, then ky, then kx);
    /// bias [F]; output [N,F,H+2p-k+1,W+2p-k+1]. Each image is unfolded (im2col) into a [C*k*k, Hout*Wout] matrix and multiplied by the weights.
    /// Images are independent and each is summed in a fixed order, so threading never changes a bit of the result.
    /// </summary>
    public static Tensor Conv2d(Tensor input, Tensor weights, Tensor bias, int kernel, int padding)
    {
        if (input.Shape.Length != 4) throw new ArgumentException($"conv2d: input must be [N, C, H, W] but got {Dims(input.Shape)}.");
        if (kernel < 1 || padding < 0) throw new ArgumentException($"conv2d: kernel must be at least 1 and padding at least 0 but got kernel {kernel}, padding {padding}.");
        int n = input.Shape[0], c = input.Shape[1], h = input.Shape[2], w = input.Shape[3];
        int ckk = c * kernel * kernel;
        if (weights.Shape.Length != 2 || weights.Shape[1] != ckk)
            throw new ArgumentException($"conv2d: weights must be [F, C*k*k] = [?, {ckk}] but got {Dims(weights.Shape)}.");
        int f = weights.Shape[0];
        if (bias.Shape.Length != 1 || bias.Shape[0] != f)
            throw new ArgumentException($"conv2d: bias must be [{f}] to match the {f} filters but got {Dims(bias.Shape)}.");
        int ho = h + 2 * padding - kernel + 1, wo = w + 2 * padding - kernel + 1;
        if (ho < 1 || wo < 1) throw new ArgumentException($"conv2d: a {kernel}x{kernel} kernel with padding {padding} does not fit a {h}x{w} image.");
        int p = ho * wo;
        bool parallel = (long)n * f * ckk * p > ParallelThreshold;
        var data = new double[n * f * p];
        var wd = weights.Data;
        var bd = bias.Data;

        // Unfold image img into cols [ckk, p]: row (ch, ky, kx), column (oy, ox) holds the input pixel that filter tap sees there (0 outside).
        double[] Unfold(int img)
        {
            var cols = new double[ckk * p];
            for (int ch = 0; ch < c; ch++)
                for (int ky = 0; ky < kernel; ky++)
                    for (int kx = 0; kx < kernel; kx++)
                    {
                        int row = (ch * kernel + ky) * kernel + kx;
                        for (int oy = 0; oy < ho; oy++)
                        {
                            int iy = oy + ky - padding;
                            if (iy < 0 || iy >= h) continue;
                            for (int ox = 0; ox < wo; ox++)
                            {
                                int ix = ox + kx - padding;
                                if (ix >= 0 && ix < w) cols[row * p + oy * wo + ox] = input.Data[((img * c + ch) * h + iy) * w + ix];
                            }
                        }
                    }
            return cols;
        }

        RunRows(n, parallel, img =>
        {
            var cols = Unfold(img);
            for (int ff = 0; ff < f; ff++)
            {
                var row = new Span<double>(data, (img * f + ff) * p, p);
                row.Fill(bd[ff]);
                for (int q = 0; q < ckk; q++)
                    AddScaled(row, new ReadOnlySpan<double>(cols, q * p, p), wd[ff * ckk + q]);
            }
        });

        return new Tensor(data, new[] { n, f, ho, wo }, "conv2d", new[] { input, weights, bias }, self =>
        {
            if (self.Grad is not { } g) return;
            if (bias.RequiresGrad)
            {
                var gb = bias.EnsureGrad();
                for (int img = 0; img < n; img++)
                    for (int ff = 0; ff < f; ff++)
                    {
                        double s = 0;
                        for (int j = 0; j < p; j++) s += g[(img * f + ff) * p + j];
                        gb[ff] += s;
                    }
            }
            if (weights.RequiresGrad)
            {
                // Per-image dW computed independently, then added in image order.
                var perImage = new double[n][];
                RunRows(n, parallel, img =>
                {
                    var cols = Unfold(img);
                    var dw = new double[f * ckk];
                    for (int ff = 0; ff < f; ff++)
                        for (int q = 0; q < ckk; q++)
                        {
                            double s = 0;
                            int gOff = (img * f + ff) * p, cOff = q * p;
                            for (int j = 0; j < p; j++) s += g[gOff + j] * cols[cOff + j];
                            dw[ff * ckk + q] = s;
                        }
                    perImage[img] = dw;
                });
                var gw = weights.EnsureGrad();
                for (int img = 0; img < n; img++)
                    for (int i = 0; i < gw.Length; i++) gw[i] += perImage[img][i];
            }
            if (input.RequiresGrad)
            {
                var gi = input.EnsureGrad();
                RunRows(n, parallel, img =>
                {
                    // dcols[q,:] = sum over filters of W[f,q] * g[f,:]; then fold back, adding where windows overlap.
                    var dcols = new double[ckk * p];
                    for (int q = 0; q < ckk; q++)
                    {
                        var row = new Span<double>(dcols, q * p, p);
                        for (int ff = 0; ff < f; ff++)
                            AddScaled(row, new ReadOnlySpan<double>(g, (img * f + ff) * p, p), wd[ff * ckk + q]);
                    }
                    for (int ch = 0; ch < c; ch++)
                        for (int ky = 0; ky < kernel; ky++)
                            for (int kx = 0; kx < kernel; kx++)
                            {
                                int row = (ch * kernel + ky) * kernel + kx;
                                for (int oy = 0; oy < ho; oy++)
                                {
                                    int iy = oy + ky - padding;
                                    if (iy < 0 || iy >= h) continue;
                                    for (int ox = 0; ox < wo; ox++)
                                    {
                                        int ix = ox + kx - padding;
                                        if (ix >= 0 && ix < w) gi[((img * c + ch) * h + iy) * w + ix] += dcols[row * p + oy * wo + ox];
                                    }
                                }
                            }
                });
            }
        });
    }

    /// <summary>Max pooling over non-overlapping size x size windows: [N,C,H,W] to [N,C,H/size,W/size]. A tie sends the gradient to the first maximum in row-major order.</summary>
    public static Tensor MaxPool2d(Tensor input, int size)
    {
        if (input.Shape.Length != 4) throw new ArgumentException($"maxpool2d: input must be [N, C, H, W] but got {Dims(input.Shape)}.");
        int n = input.Shape[0], c = input.Shape[1], h = input.Shape[2], w = input.Shape[3];
        if (size < 1 || h % size != 0 || w % size != 0)
            throw new ArgumentException($"maxpool2d: height {h} and width {w} must both be divisible by the window size {size}.");
        int ho = h / size, wo = w / size;
        var data = new double[n * c * ho * wo];
        var argmax = new int[data.Length];
        for (int plane = 0; plane < n * c; plane++)
            for (int oy = 0; oy < ho; oy++)
                for (int ox = 0; ox < wo; ox++)
                {
                    int best = -1;
                    for (int dy = 0; dy < size; dy++)
                        for (int dx = 0; dx < size; dx++)
                        {
                            int idx = (plane * h + oy * size + dy) * w + ox * size + dx;
                            if (best < 0 || input.Data[idx] > input.Data[best]) best = idx;
                        }
                    int o = (plane * ho + oy) * wo + ox;
                    data[o] = input.Data[best];
                    argmax[o] = best;
                }

        return new Tensor(data, new[] { n, c, ho, wo }, "maxpool2d", new[] { input }, self =>
        {
            if (self.Grad is not { } g || !input.RequiresGrad) return;
            var gi = input.EnsureGrad();
            for (int o = 0; o < g.Length; o++) gi[argmax[o]] += g[o];
        });
    }
}
