namespace AIAnatomy.Presentation.Visuals;

/// <summary>One line of a <c>LineChart</c>.</summary>
public sealed record LineSeries(string Name, IReadOnlyList<(double X, double Y)> Points);
