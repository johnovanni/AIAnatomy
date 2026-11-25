namespace AIAnatomy.Models.Rules;

/// <summary>The result of parsing one line: a rule, or a plain-English error.</summary>
public sealed record RuleParse(Rule? Rule, string? Error);

/// <summary>Reads rules typed as text: <c>if A [and B]... then C</c>, where a condition may start with "not".</summary>
public static class RuleText
{
    public const int MaxFactLength = 40, MaxConditions = 6;

    /// <summary>Trim, lower-case (invariant) and collapse inner whitespace to one space.</summary>
    public static string Normalize(string fact) =>
        string.Join(' ', fact.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>Never throws. Keywords if, and, not, then are accepted in any case.</summary>
    public static RuleParse Parse(string? text)
    {
        var words = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || !Is(words[0], "if")) return Fail("Start the rule with \"if\".");

        int then = Array.FindIndex(words, 1, w => Is(w, "then"));
        if (then < 0) return Fail("Put \"then\" before the conclusion.");

        // Split the words between "if" and "then" into conditions at each "and".
        var groups = new List<List<string>> { new() };
        for (int i = 1; i < then; i++)
        {
            if (Is(words[i], "and")) groups.Add(new());
            else groups[^1].Add(words[i]);
        }

        var conditions = new List<(bool Negated, string Fact)>();
        foreach (var g in groups)
        {
            bool negated = g.Count > 0 && Is(g[0], "not");
            var fact = string.Join(' ', negated ? g.Skip(1) : g);
            if (fact.Length == 0) return Fail("A condition is empty.");
            conditions.Add((negated, fact));
        }

        var conclusion = string.Join(' ', words.Skip(then + 1));
        if (conclusion.Length == 0) return Fail("The conclusion is empty.");
        if (conditions.Count > MaxConditions) return Fail($"A rule can have at most {MaxConditions} conditions.");

        foreach (var fact in conditions.Select(c => c.Fact).Append(conclusion))
        {
            var error = CheckFact(Normalize(fact));
            if (error != null) return Fail(error);
        }

        return new RuleParse(
            new Rule(conditions.Select(c => new Condition(Normalize(c.Fact), c.Negated)).ToList(), Normalize(conclusion)), null);
    }

    private static string? CheckFact(string fact)
    {
        if (!fact.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\''))
            return "Use only letters, numbers, spaces, hyphens and apostrophes.";
        if (fact.Length > MaxFactLength) return $"\"{fact}\" is too long ({MaxFactLength} characters at most).";
        return null;
    }

    private static bool Is(string word, string keyword) => word.Equals(keyword, StringComparison.OrdinalIgnoreCase);
    private static RuleParse Fail(string error) => new(null, error);
}
