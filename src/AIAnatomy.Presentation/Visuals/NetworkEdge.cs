namespace AIAnatomy.Presentation.Visuals;

/// <summary>An arrow in a <c>NetworkDiagram</c> from one node to another, with an optional label.</summary>
public sealed record NetworkEdge(string FromId, string ToId, string? Label);
