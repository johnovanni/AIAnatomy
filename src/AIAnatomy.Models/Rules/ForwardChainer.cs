namespace AIAnatomy.Models.Rules;

/// <summary>A rule that fired. <see cref="RuleNumber"/> and <see cref="Pass"/> are 1-based.</summary>
public sealed record FiredRule(int RuleNumber, Rule Rule, int Pass);

public sealed record ChainResult(IReadOnlyList<FiredRule> Fired, IReadOnlySet<string> Known, IReadOnlyList<string> Answers);

/// <summary>Forward chaining: keep applying the rules to the known facts until nothing new follows.</summary>
public static class ForwardChainer
{
    public static ChainResult Run(RuleSet rules, IReadOnlySet<string> yesFacts)
    {
        var yes = yesFacts.Select(RuleText.Normalize).ToHashSet();
        var known = new HashSet<string>(yes);
        var fired = new List<FiredRule>();

        for (int pass = 1; ; pass++)
        {
            bool any = false;
            for (int i = 0; i < rules.Rules.Count; i++)
            {
                if (rules.Excluded.Contains(i)) continue;
                var rule = rules.Rules[i];
                if (known.Contains(rule.Conclusion)) continue;
                if (!rule.Conditions.All(c => c.Negated ? !yes.Contains(c.Fact) : known.Contains(c.Fact))) continue;
                known.Add(rule.Conclusion);
                fired.Add(new FiredRule(i + 1, rule, pass));
                any = true;
            }
            if (!any) break;
        }

        return new ChainResult(fired, known, rules.FinalConclusions.Where(known.Contains).ToList());
    }
}
