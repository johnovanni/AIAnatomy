namespace AIAnatomy.Models.Toys;

/// <summary>A plain-English rule. Apply gets the text so far and returns the next character, or null if the rule does not apply.</summary>
public sealed record WritingRule(string Description, Func<string, char?> Apply);

/// <summary>Writes text one character at a time using hand-made rules; the first rule that applies wins. No randomness.</summary>
public sealed class RuleBasedWriter
{
    public RuleBasedWriter()
    {
        Rules = new[]
        {
            new WritingRule("After q, write u.", s => Last(s) == 'q' ? 'u' : null),
            new WritingRule("After a full stop, write a space.", s => Last(s) == '.' ? ' ' : null),
            new WritingRule("After a comma, write a space.", s => Last(s) == ',' ? ' ' : null),
            new WritingRule("After a space that follows a full stop, write T.", s => Last(s) == ' ' && Before(s) == '.' ? 'T' : null),
            new WritingRule("After a space, write t.", s => Last(s) == ' ' ? 't' : null),
            new WritingRule("After t, write h.", s => Last(s) == 't' ? 'h' : null),
            new WritingRule("After h, write e.", s => Last(s) == 'h' ? 'e' : null),
            new WritingRule("After e, write a space.", s => Last(s) == 'e' ? ' ' : null),
            new WritingRule("After a vowel, write n.", s => Last(s) is 'a' or 'i' or 'o' or 'u' ? 'n' : null),
            new WritingRule("Otherwise, write e.", _ => 'e'),
        };
    }

    public IReadOnlyList<WritingRule> Rules { get; }

    /// <summary>Returns only the new text: <paramref name="count"/> characters written after <paramref name="start"/>.</summary>
    public string Write(string start, int count)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var text = new System.Text.StringBuilder(start);
        for (int i = 0; i < count; i++) text.Append(Next(text.ToString()));
        return text.ToString(start.Length, count);
    }

    private char Next(string text)
    {
        foreach (var rule in Rules)
            if (rule.Apply(text) is { } c) return c;
        return 'e'; // not reached: the last rule always applies
    }

    private static char? Last(string s) => s.Length > 0 ? s[^1] : null;
    private static char? Before(string s) => s.Length > 1 ? s[^2] : null;
}
