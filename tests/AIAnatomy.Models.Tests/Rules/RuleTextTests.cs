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

    [Fact]
    public void Error_ConclusionWithAnd() =>
        Assert.Equal("A conclusion is one fact. Write a separate rule for each conclusion.", RuleText.Parse("if has hair then mammal and warm").Error);

    [Fact]
    public void Error_ConclusionStartingWithNot() =>
        Assert.Equal("A conclusion can't start with \"not\": a rule can only conclude that something is true.", RuleText.Parse("if has hair then not mammal").Error);

    [Theory]
    [InlineData("if a then b then c")]
    [InlineData("if a not b then c")]
    [InlineData("if not not x then y")]
    [InlineData("if a if b then c")]
    [InlineData("if a then b if c")]
    public void Error_KeywordInsideAFact(string text) =>
        Assert.Equal("The words \"if\", \"then\", \"and\" and \"not\" can't be part of a fact.", RuleText.Parse(text).Error);

    [Fact]
    public void Parse_KeywordsAreWholeWords()
    {
        var r = RuleText.Parse("if android and not notable then thenceforth");
        Assert.Null(r.Error);
        Assert.Equal(new[] { new Condition("android", false), new Condition("notable", true) }, r.Rule!.Conditions);
        Assert.Equal("thenceforth", r.Rule.Conclusion);
    }

    [Fact]
    public void Parse_KeywordsInAnyCase()
    {
        var r = RuleText.Parse("IF a AnD NOT b THEN c");
        Assert.Null(r.Error);
        Assert.Equal(new[] { new Condition("a", false), new Condition("b", true) }, r.Rule!.Conditions);
        Assert.Equal("c", r.Rule.Conclusion);
    }

    [Fact]
    public void Parse_AcceptsHyphensAndApostrophes() =>
        Assert.Null(RuleText.Parse("if black-and-white and bird's wing then it's a bird").Error);

    [Fact]
    public void Parse_LongLineGivesAnErrorNotAnException()
    {
        var r = RuleText.Parse("if " + new string('x', 200) + " then y");
        Assert.NotNull(r.Error);
        Assert.Null(r.Rule);
    }

    [Fact]
    public void Normalize_MapsCurlyApostrophe() =>
        Assert.Equal("bird's wing", RuleText.Normalize("Bird’s  Wing"));
}
