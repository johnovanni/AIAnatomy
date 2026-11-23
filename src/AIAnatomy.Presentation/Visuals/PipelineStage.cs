namespace AIAnatomy.Presentation.Visuals;

/// <summary>One step of a <c>PipelineView</c>: a vector of numbers, a list of named numbers, or text only.</summary>
public sealed record PipelineStage(string Title, string Description, double[]? Values, IReadOnlyList<LabeledValue>? Items);
