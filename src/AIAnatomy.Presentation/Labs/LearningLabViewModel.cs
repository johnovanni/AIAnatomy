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
        Perceptron.Recalculated += ReportNeuron;
        Loss.Recalculated += ReportSurface;
        Graph.Recalculated += ReportBackprop;
        CrossEntropy.Recalculated += ReportCrossEntropy;
        // The first report of each screen is its baseline: opening the lab lights nothing.
        ReportNeuron();
        ReportSurface();
        ReportBackprop();
        ReportCrossEntropy();
    }

    // Each part reports once per action, after everything it shows is current. The lists are replaced, never edited.
    private void ReportNeuron()
    {
        var p = Perceptron;
        // Lines is left out on purpose: it is derived from LineText (the weights) and the table rows.
        ReportScreen("neuron", new object[] { p.Status, p.LineText, p.WrongPoints, p.IsXor, p.Points.ToArray(), p.TableRows.ToArray(), p.TableNote });
    }

    private void ReportSurface()
    {
        var l = Loss;
        ReportScreen("surface", new object?[] { l.Message, l.Summary, l.FactorText, l.Path.ToArray(), l.Rows.ToArray() });
    }

    private void ReportBackprop()
    {
        var g = Graph;
        ReportScreen("backprop", new object[] { g.Rows.ToArray(), g.Working.ToArray() });
    }

    private void ReportCrossEntropy()
    {
        var c = CrossEntropy;
        ReportScreen("crossentropy", new object?[]
        {
            c.ContextText, c.ActualText, c.ProbabilityText, c.FormulaText, c.BarsCaption,
            c.Bars.ToArray(), c.Losses, c.LossKey, c.MeanText,
        });
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
