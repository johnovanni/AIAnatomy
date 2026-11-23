using System.Text;

namespace AIAnatomy.Presentation.Labs;

/// <summary>Wording and text helpers shared by the labs that take typed text.</summary>
public static class LabText
{
    public const string EmptyPrompt = "Type some text to begin";

    /// <summary>The note shown when the typed text is longer than the model can read.</summary>
    public static string TruncationNote(int contextLength) =>
        $"Only the last {contextLength} characters are used: that is this model's context length.";

    /// <summary>Makes spaces and newlines visible, so a token such as " the" can be seen: ␣ for a space, ↵ for a newline.</summary>
    public static string Visible(string tokenText)
    {
        ArgumentNullException.ThrowIfNull(tokenText);
        return tokenText.Replace(' ', '␣').Replace("\r\n", "↵").Replace('\n', '↵').Replace('\r', '↵');
    }

    /// <summary>Newlines shown as ↵ (spaces left alone), for running text on one line.</summary>
    public static string ShowNewlines(string text) => text.Replace("\r\n", "↵").Replace('\n', '↵').Replace('\r', '↵');

    /// <summary>Number of Unicode scalars (an emoji counts once), which is what the character tokenizer counts.</summary>
    public static int CountScalars(string text)
    {
        int n = 0;
        foreach (var _ in text.EnumerateRunes()) n++;
        return n;
    }
}
