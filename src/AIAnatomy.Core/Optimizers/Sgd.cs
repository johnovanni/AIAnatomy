namespace AIAnatomy.Core;

/// <summary>Plain stochastic gradient descent: w ← w − lr · grad.</summary>
public sealed class Sgd : IOptimizer
{
    private readonly IReadOnlyList<Parameter> _parameters;

    public Sgd(IReadOnlyList<Parameter> ps, double learningRate)
    {
        _parameters = ps;
        LearningRate = learningRate;
    }

    public double LearningRate { get; set; }

    public void Step()
    {
        foreach (var p in _parameters)
        {
            if (p.Value.Grad is not { } grad) continue;
            var data = p.Value.Data;
            for (int i = 0; i < data.Length; i++) data[i] -= LearningRate * grad[i];
        }
    }

    public void ZeroGrad()
    {
        foreach (var p in _parameters) p.Value.ZeroGrad();
    }
}
