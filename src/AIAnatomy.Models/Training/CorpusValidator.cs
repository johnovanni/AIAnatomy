namespace AIAnatomy.Models.Training;

/// <summary>Plain-language checks on pasted training text, before any training starts.</summary>
public static class CorpusValidator
{
    public const int MaxCharacters = 2_000_000;

    /// <summary>Null when the text is fine; otherwise a message the user can act on.</summary>
    public static string? Problem(string text, int contextLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ProblemForLength(text.Length, contextLength);
    }

    /// <summary>The same rules for text already turned into tokens (one token per character).</summary>
    public static string? ProblemForLength(int length, int contextLength)
    {
        int minimum = contextLength + 2;
        if (length < minimum)
            return $"The text is too short to learn from: it needs at least {minimum} characters, and this has {length}.";
        if (length > MaxCharacters)
            return $"The text is too long: it can have at most {MaxCharacters:N0} characters, and this has {length:N0}.";
        return null;
    }
}
