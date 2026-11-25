namespace AIAnatomy.Models.ClassicMl;

/// <summary>A point on the plot, with its class: 0, 1 or 2.</summary>
public sealed record LabeledPoint(double X, double Y, int Label);
