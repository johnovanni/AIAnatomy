namespace AIAnatomy.Lessons;

/// <summary>The parts of a transformer that lessons, labs and diagrams are tagged with. Each key has a colour
/// brush named "Part" + PascalCase(key) + "Brush" in the Dark and Light theme files.</summary>
public static class Parts
{
    public static IReadOnlyList<string> All { get; } =
        ["intro", "tokens", "embeddings", "position", "attention", "feedforward", "residual", "output", "learning", "whole", "later"];

    public static bool IsKnown(string? key) => key is not null && All.Contains(key, StringComparer.Ordinal);
}
