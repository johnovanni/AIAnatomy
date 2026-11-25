using AIAnatomy.Models.Rules;

namespace AIAnatomy.Models.Tests.Rules;

public class RuleTextTests
{
    public static IEnumerable<object[]> Defaults() => AnimalRules.DefaultRuleTexts.Select(t => new object[] { t });

    [Fact]
    public void Parse_Rule13_HasFiveConditionsWithFliesNegated()
    {
        var r = RuleText.Parse(AnimalRules.DefaultRuleTexts[12]);
        Assert.Null(r.Error);
        Assert.NotNull(r.Rule);
        Assert.Equal(5, r.Rule!.Conditions.Count);
        Assert.Equal(new Condition("flies", true), r.Rule.Conditions[1]);
        Assert.Equal("ostrich", r.Rule.Conclusion);
    }

    [Fact]
    public void Parse_IsCaseAndSpaceInsensitive()
    {
        var r = RuleText.Parse("IF  Has   Hair THEN Mammal");
        Assert.Null(r.Error);
        Assert.Equal(new Condition("has hair", false), Assert.Single(r.Rule!.Conditions));
        Assert.Equal("mammal", r.Rule.Conclusion);
    }

    [Theory]
    [MemberData(nameof(Defaults))]
    public void ToText_RoundTripsEveryDefaultRule(string text)
    {
        var r = RuleText.Parse(text);
        Assert.Null(r.Error);
        Assert.Equal(text, r.Rule!.ToText());
    }

    [Fact]
    public void Normalize_TrimsLowersAndCollapses() =>
        Assert.Equal("has hair", RuleText.Normalize("  Has \t HAIR "));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("has hair then mammal")]
    public void Error_StartWithIf(string? text) =>
        Assert.Equal("Start the rule with \"if\".", RuleText.Parse(text).Error);

    [Fact]
    public void Error_MissingThen() =>
        Assert.Equal("Put \"then\" before the conclusion.", RuleText.Parse("if has hair mammal").Error);

    [Theory]
    [InlineData("if has hair and then x")]
    [InlineData("if then x")]
    [InlineData("if and has hair then x")]
    [InlineData("if not then x")]
    public void Error_EmptyCondition(string text) =>
        Assert.Equal("A condition is empty.", RuleText.Parse(text).Error);

    [Fact]
    public void Error_EmptyConclusion() =>
        Assert.Equal("The conclusion is empty.", RuleText.Parse("if has hair then").Error);

    [Fact]
    public void Error_FactTooLong()
    {
        var fact = new string('a', 41);
        Assert.Equal($"\"{fact}\" is too long (40 characters at most).", RuleText.Parse($"if {fact} then b").Error);
        Assert.Null(RuleText.Parse($"if {new string('a', 40)} then b").Error);
    }

    [Fact]
    public void Error_BadCharacters() =>
        Assert.Equal("Use only letters, numbers, spaces, hyphens and apostrophes.", RuleText.Parse("if a! then b").Error);

    [Fact]
    public void Error_SevenConditions()
    {
        Assert.Equal("A rule can have at most 6 conditions.", RuleText.Parse("if a and b and c and d and e and f and g then h").Error);
        Assert.Null(RuleText.Parse("if a and b and c and d and e and f then h").Error);
    }
}
