namespace AIAnatomy.Core;

/// <summary>Adam: gradient descent with running averages of the gradient (m) and its square (v), bias-corrected.</summary>
public sealed class Adam : IOptimizer
{
    private readonly IReadOnlyList<Parameter> _parameters;
    private readonly double _beta1, _beta2, _eps;
    private readonly double[][] _m, _v;
    private int _t;

    public Adam(IReadOnlyList<Parameter> ps, double learningRate,
        double beta1 = 0.9, double beta2 = 0.999, double eps = 1e-8)
    {
        _parameters = ps;
        LearningRate = learningRate;
        _beta1 = beta1;
        _beta2 = beta2;
        _eps = eps;
        _m = ps.Select(p => new double[p.Value.Size]).ToArray();
        _v = ps.Select(p => new double[p.Value.Size]).ToArray();
    }

    public double LearningRate { get; set; }

    public void Step()
    {
        _t++;
        double correction1 = 1 - Math.Pow(_beta1, _t);
        double correction2 = 1 - Math.Pow(_beta2, _t);
        for (int k = 0; k < _parameters.Count; k++)
        {
            var value = _parameters[k].Value;
            if (value.Grad is not { } grad) continue;
            var data = value.Data;
            var m = _m[k];
            var v = _v[k];
            for (int i = 0; i < data.Length; i++)
            {
                m[i] = _beta1 * m[i] + (1 - _beta1) * grad[i];
                v[i] = _beta2 * v[i] + (1 - _beta2) * grad[i] * grad[i];
                double mHat = m[i] / correction1;
                double vHat = v[i] / correction2;
                data[i] -= LearningRate * mHat / (Math.Sqrt(vHat) + _eps);
            }
        }
    }

    public void ZeroGrad()
    {
        foreach (var p in _parameters) p.Value.ZeroGrad();
    }
}
