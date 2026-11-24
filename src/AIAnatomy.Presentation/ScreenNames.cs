namespace AIAnatomy.Presentation;

/// <summary>Screen ids ("real-heads", "crossentropy") as words a screen reader can say (final review M11).</summary>
public static class ScreenNames
{
    // Ids that are short forms; everything else is its own words with hyphens as spaces.
    private static readonly Dictionary<string, string> Words = new(StringComparer.Ordinal)
    {
        ["crossentropy"] = "cross-entropy",
        ["backprop"] = "backpropagation",
        ["lens"] = "logit lens",
        ["norm"] = "layer norm",
        ["heat"] = "heat map",
        ["reads"] = "what the model reads",
        ["sizes"] = "vector sizes",
    };

    /// <summary>"Real heads screen", "Cross-entropy screen", "Head 2 screen"; "Screen" for an empty id.</summary>
    public static string Readable(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "Screen";
        string words = Words.TryGetValue(id, out var w) ? w : id.Replace('-', ' ');
        return char.ToUpperInvariant(words[0]) + words[1..] + " screen";
    }
}
