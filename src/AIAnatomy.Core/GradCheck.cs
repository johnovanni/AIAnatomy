namespace AIAnatomy.Core;

/// <summary>Checks backpropagation against numerical differentiation.</summary>
public static class GradCheck
{
    private const double Step = 1e-5;

    /// <summary>
    /// Runs <paramref name="scalarFn"/> on parameter copies of <paramref name="inputs"/>, compares the gradients from
    /// <see cref="Tensor.Backward"/> with central differences (h = 1e-5), and returns the largest relative error,
    /// |analytic - numeric| / max(1, |analytic|, |numeric|). The max(1, ...) keeps near-zero gradients from inflating it.
    /// </summary>
    public static double MaxRelativeError(Func<Tensor[], Tensor> scalarFn, params Tensor[] inputs)
    {
        var parameters = inputs.Select(t => t.AsParameter()).ToArray();
        scalarFn(parameters).Backward();

        double worst = 0;
        foreach (var p in parameters)
        {
            for (int i = 0; i < p.Size; i++)
            {
                double analytic = p.Grad?[i] ?? 0;
                double original = p.Data[i];
                p.Data[i] = original + Step;
                double up = scalarFn(parameters).Item();
                p.Data[i] = original - Step;
                double down = scalarFn(parameters).Item();
                p.Data[i] = original;
                double numeric = (up - down) / (2 * Step);
                double error = Math.Abs(analytic - numeric) / Math.Max(1.0, Math.Max(Math.Abs(analytic), Math.Abs(numeric)));
                worst = Math.Max(worst, error);
            }
        }
        return worst;
    }
}
