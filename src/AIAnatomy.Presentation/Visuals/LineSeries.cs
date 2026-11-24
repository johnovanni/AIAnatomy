namespace AIAnatomy.Presentation.Visuals;

/// <summary>One line of a <c>LineChart</c>.</summary>
public sealed record LineSeries(string Name, IReadOnlyList<(double X, double Y)> Points)
{
    /// <summary>The latest value as the legend shows it: 3 decimals (<see cref="NumberText.Short"/>), or null with no
    /// points. Hovering the chart still gives exact values (owner's choice, AA-001).</summary>
    public string? LatestText => Points.Count > 0 ? NumberText.Short(Points[^1].Y) : null;
}
