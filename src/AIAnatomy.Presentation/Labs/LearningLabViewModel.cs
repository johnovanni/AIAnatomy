using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// The four parts of learning, one under the other: a perceptron, a loss surface, a graph with backpropagation, and the
/// cross-entropy loss of the active model. Reset puts every part back to its defaults.
/// </summary>
public sealed class LearningLabViewModel : LabViewModel
{
    public const string Id = "LearningLab";

    public LearningLabViewModel(IActiveModelService models)
    {
        ArgumentNullException.ThrowIfNull(models);
        Perceptron = new PerceptronPanelViewModel();
        Loss = new LossPanelViewModel();
        Graph = new GraphPanelViewModel();
        CrossEntropy = new CrossEntropyPanelViewModel(models);
    }

    public override string LabId => Id;

    public PerceptronPanelViewModel Perceptron { get; }
    public LossPanelViewModel Loss { get; }
    public GraphPanelViewModel Graph { get; }
    public CrossEntropyPanelViewModel CrossEntropy { get; }

    public override void Reset()
    {
        Perceptron.Reset();
        Loss.Reset();
        Graph.Reset();
        CrossEntropy.Reset();
    }

    public override void OnActiveModelChanged() => CrossEntropy.OnActiveModelChanged();
}
