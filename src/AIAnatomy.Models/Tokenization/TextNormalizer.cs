namespace AIAnatomy.Models.Tokenization;

/// <summary>Text clean-up shared by all tokenizers.</summary>
public static class TextNormalizer
{
    /// <summary>Turns "\r\n" and lone "\r" into "\n". Nothing else changes.</summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
