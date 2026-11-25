namespace AIAnatomy.Models.Rules;

/// <summary>One "if" part of a rule: a fact that must be known, or (when negated) must not be among the facts you answered yes to.</summary>
public sealed record Condition(string Fact, bool Negated);

/// <summary>An if-then rule: every condition holds, so the conclusion follows.</summary>
public sealed record Rule(IReadOnlyList<Condition> Conditions, string Conclusion)
{
    /// <summary>The canonical lower-case text, for example "if has hair and gives milk then mammal".</summary>
    public string ToText() =>
        "if " + string.Join(" and ", Conditions.Select(c => (c.Negated ? "not " : "") + c.Fact)) + " then " + Conclusion;
}
