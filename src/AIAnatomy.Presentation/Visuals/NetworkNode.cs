namespace AIAnatomy.Presentation.Visuals;

/// <summary>
/// A box in a <c>NetworkDiagram</c>. <see cref="X"/> and <see cref="Y"/> place its centre as a fraction (0 to 1) of
/// the diagram's width and height. A node with a <see cref="LinkLessonId"/> opens that lesson when clicked.
/// </summary>
public sealed record NetworkNode(string Id, string Label, double X, double Y, string? Value, string? LinkLessonId);
