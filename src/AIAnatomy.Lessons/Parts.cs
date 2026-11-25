namespace AIAnatomy.Lessons;

/// <summary>The parts that lessons, labs and diagrams are tagged with: the transformer's own parts (sections 1 to 3 and
/// "later") and the parts of the wider family of AI (section 4). Each key has a colour brush named
/// "Part" + PascalCase(key) + "Brush" in the Dark and Light theme files, and an icon glyph.</summary>
public static class Parts
{
    /// <summary>The transformer's parts, plus "whole" and "later".</summary>
    public static IReadOnlyList<string> Transformer { get; } =
        ["intro", "tokens", "embeddings", "position", "attention", "feedforward", "residual", "output", "learning", "whole", "later"];

    /// <summary>The parts of the family of AI (section 4); Part B appends vision, memory and diffusion.</summary>
    public static IReadOnlyList<string> Family { get; } =
        ["family", "rules", "search", "classic", "reward"];

    public static IReadOnlyList<string> All { get; } = [.. Transformer, .. Family];

    public static bool IsKnown(string? key) => key is not null && All.Contains(key, StringComparer.Ordinal);
}
