namespace AIAnatomy.Presentation.Visuals;

/// <summary>
/// One step of a <c>PipelineView</c>: a vector of numbers, a list of named numbers, or text only. <paramref name="AllItems"/>,
/// when given, is the full list behind a shortened <paramref name="Items"/>; the view shows it in the step's tooltip.
/// </summary>
public sealed record PipelineStage(string Title, string Description, double[]? Values, IReadOnlyList<LabeledValue>? Items,
    IReadOnlyList<LabeledValue>? AllItems = null);
