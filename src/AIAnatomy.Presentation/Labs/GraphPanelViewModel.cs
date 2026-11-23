using AIAnatomy.Core;
using AIAnatomy.Models.Toys;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One node of the expression graph: its value going forwards and its gradient coming back.</summary>
public sealed record GraphNodeRow(string Name, double Value, double Gradient, string ValueText, string GradientText)
{
    public string Description => $"{Name}: value {ValueText}, gradient {GradientText}";
}

/// <summary>
/// Part (c) of the learning lab: L = (a·b + c)·f as a graph of single numbers. <see cref="ExpressionGraphDemo"/> runs the
/// forward pass and backpropagation; this view-model only reads values and gradients from its nodes.
/// </summary>
public sealed partial class GraphPanelViewModel : ObservableObject
{
    public const double DefaultA = 2, DefaultB = -3, DefaultC = 10, DefaultF = -2;
    public const double Limit = 100;

    public const string BackpropNote =
        "Backpropagation passes “how much L changes if this number changes” backwards through the graph. It starts with L itself (changing L by 1 changes L by 1), " +
        "then at each step multiplies by how much that node's output changes if its input changes: the chain rule. A gradient is that answer for one node.";

    private bool _syncing;

    public GraphPanelViewModel() => Refresh();

    [ObservableProperty] private double a = DefaultA;
    [ObservableProperty] private double b = DefaultB;
    [ObservableProperty] private double c = DefaultC;
    [ObservableProperty] private double f = DefaultF;
    [ObservableProperty] private IReadOnlyList<GraphNodeRow> rows = [];
    [ObservableProperty] private IReadOnlyList<string> working = [];
    [ObservableProperty] private IReadOnlyList<NetworkNode> nodes = [];

    public IReadOnlyList<NetworkEdge> Edges { get; } =
    [
        new("a", "e", "×"), new("b", "e", "×"), new("e", "d", "+"), new("c", "d", "+"), new("d", "L", "×"), new("f", "L", "×"),
    ];

    partial void OnAChanged(double value) => Changed(ref a, value, nameof(A));
    partial void OnBChanged(double value) => Changed(ref b, value, nameof(B));
    partial void OnCChanged(double value) => Changed(ref c, value, nameof(C));
    partial void OnFChanged(double value) => Changed(ref f, value, nameof(F));

    /// <summary>Keeps a typed number usable: not-a-number is put back to 0 and large numbers are limited to ±100 (the box is told, so it shows the number used).</summary>
    private void Changed(ref double field, double value, string name)
    {
        if (_syncing) return;
        double used = double.IsNaN(value) ? 0 : Math.Clamp(value, -Limit, Limit);
        if (used != value)
        {
            _syncing = true;
            field = used;
            _syncing = false;
            OnPropertyChanged(name);
        }
        Refresh();
    }

    public void Reset()
    {
        _syncing = true;
        A = DefaultA;
        B = DefaultB;
        C = DefaultC;
        F = DefaultF;
        _syncing = false;
        Refresh();
    }

    private void Refresh()
    {
        var graph = new ExpressionGraphDemo(A, B, C, F);
        graph.Run();
        var t = graph.Nodes;
        string[] ids = ["a", "b", "c", "f", "e", "d", "L"];
        string Value(int i) => NumberText.Short(t[i].Data[0]);
        string Gradient(int i) => NumberText.Short(t[i].Grad?[0] ?? 0);

        Rows = t.Select((n, i) => new GraphNodeRow(n.Label ?? ids[i], n.Data[0], n.Grad?[0] ?? 0, NumberText.Exact(n.Data[0]), NumberText.Exact(n.Grad?[0] ?? 0))).ToList();

        // Same order as the nodes: a, b, c, f, e, d, L. Positions are fractions of the diagram.
        (double X, double Y)[] places = [(0.12, 0.10), (0.12, 0.34), (0.12, 0.62), (0.12, 0.90), (0.42, 0.22), (0.64, 0.50), (0.88, 0.72)];
        Nodes = t.Select((n, i) => new NetworkNode(ids[i], n.Label ?? ids[i], places[i].X, places[i].Y,
            $"value {Value(i)}\ngradient {Gradient(i)}", null)).ToList();

        // The working, from L back to the inputs. Every number is read from the nodes.
        Working =
        [
            $"∂L/∂L = {Gradient(6)}: changing L by 1 changes L by 1.",
            $"∂L/∂d = ∂L/∂L × f = {Gradient(6)} × {Value(3)} = {Gradient(5)}",
            $"∂L/∂f = ∂L/∂L × d = {Gradient(6)} × {Value(5)} = {Gradient(3)}",
            $"∂L/∂e = ∂L/∂d × 1 = {Gradient(5)} × 1 = {Gradient(4)}",
            $"∂L/∂c = ∂L/∂d × 1 = {Gradient(5)} × 1 = {Gradient(2)}",
            $"∂L/∂a = ∂L/∂e × b = {Gradient(4)} × {Value(1)} = {Gradient(0)}",
            $"∂L/∂b = ∂L/∂e × a = {Gradient(4)} × {Value(0)} = {Gradient(1)}",
        ];
    }
}
