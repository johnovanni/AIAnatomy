namespace AIAnatomy.Presentation.Visuals;

/// <summary>A cell of a <c>HeatMap</c> (0-based row and column).</summary>
public readonly record struct GridCell(int Row, int Column);
