namespace AIAnatomy.Models.Rules;

/// <summary>A problem with how a rule relates to the others. <see cref="RuleNumber"/> is 1-based.</summary>
public sealed record RuleProblem(int RuleNumber, string Message);

/// <summary>
/// A list of rules and what can be said about them as a whole. Facts are normalized on the way in.
/// A rule with a problem is left out of chaining, and is also left out when working out
/// <see cref="AskableFacts"/> and <see cref="FinalConclusions"/> (they come from the rules with no problem).
/// </summary>
public sealed class RuleSet
{
    public RuleSet(IEnumerable<Rule> rules)
    {
        Rules = rules.Select(r => new Rule(
            r.Conditions.Select(c => new Condition(RuleText.Normalize(c.Fact), c.Negated)).ToList(),
            RuleText.Normalize(r.Conclusion))).ToList();

        // A rule that concludes one of its own conditions (negated or not) has a problem.
        var own = new HashSet<int>();
        for (int i = 0; i < Rules.Count; i++)
            if (Rules[i].Conditions.Any(c => c.Fact == Rules[i].Conclusion)) own.Add(i);

        // "not" only works with a fact you answer: it is a problem when any rule without
        // its own problem concludes that fact.
        var concluded = Enumerable.Range(0, Rules.Count).Where(i => !own.Contains(i)).Select(i => Rules[i].Conclusion).ToHashSet();

        var problems = new List<RuleProblem>();
        var bad = new HashSet<int>(own);
        for (int i = 0; i < Rules.Count; i++)
        {
            if (own.Contains(i)) problems.Add(new RuleProblem(i + 1, "This rule concludes one of its own conditions."));
            foreach (var c in Rules[i].Conditions.Where(c => c.Negated && concluded.Contains(c.Fact)).DistinctBy(c => c.Fact))
            {
                problems.Add(new RuleProblem(i + 1, $"\"not\" only works with a fact you answer, and a rule concludes \"{c.Fact}\"."));
                bad.Add(i);
            }
        }
        Problems = problems;
        Excluded = bad;

        var active = Enumerable.Range(0, Rules.Count).Where(i => !bad.Contains(i)).Select(i => Rules[i]).ToList();
        var conclusions = active.Select(r => r.Conclusion).ToHashSet();
        var conditions = active.SelectMany(r => r.Conditions.Select(c => c.Fact)).ToHashSet();
        AskableFacts = active.SelectMany(r => r.Conditions.Select(c => c.Fact)).Where(f => !conclusions.Contains(f)).Distinct().ToList();
        FinalConclusions = active.Select(r => r.Conclusion).Where(f => !conditions.Contains(f)).Distinct().ToList();
    }

    public IReadOnlyList<Rule> Rules { get; }

    /// <summary>Facts used in conditions that no rule concludes, in first-use order.</summary>
    public IReadOnlyList<string> AskableFacts { get; }

    /// <summary>Conclusions that are never a condition, in first-use order.</summary>
    public IReadOnlyList<string> FinalConclusions { get; }

    public IReadOnlyList<RuleProblem> Problems { get; }

    /// <summary>Zero-based indexes of the rules with a problem.</summary>
    internal IReadOnlySet<int> Excluded { get; }
}
