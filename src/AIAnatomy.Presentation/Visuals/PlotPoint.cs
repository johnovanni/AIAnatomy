namespace AIAnatomy.Presentation.Visuals;

/// <summary>One point of a <c>ScatterPlot</c>. Points with the same <see cref="Group"/> share a marker shape.</summary>
public sealed record PlotPoint(double X, double Y, string Label, string Group);
