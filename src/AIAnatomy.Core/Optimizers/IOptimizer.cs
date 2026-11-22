namespace AIAnatomy.Core;

public interface IOptimizer
{
    /// <summary>Updates every parameter that has a gradient, in place.</summary>
    void Step();

    /// <summary>Clears the gradient of every parameter.</summary>
    void ZeroGrad();

    double LearningRate { get; set; }
}
