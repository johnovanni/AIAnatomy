namespace AIAnatomy.Presentation.Visuals;

/// <summary>A number with a name, such as one choice and its probability.</summary>
public sealed record LabeledValue(string Label, double Value);
