using AIAnatomy.Models.Rules;

namespace AIAnatomy.Models.Tests.Rules;

public class ForwardChainerTests
{
    private static Rule R(string text) => RuleText.Parse(text).Rule!;
    private static RuleSet Defaults() => new(AnimalRules.DefaultRuleTexts.Select(R));
    private static HashSet<string> Facts(params string[] f) => new(f);

    [Fact]
    public void AskableFacts_AreTheNineteenInFirstUseOrder() =>
        Assert.Equal(new[]
        {
            "has hair", "gives milk", "has feathers", "flies", "lays eggs", "eats meat", "has pointed teeth", "has claws",
            "has forward-facing eyes", "has hooves", "chews cud", "tawny", "dark spots", "black stripes", "long neck",
            "long legs", "black-and-white", "swims", "good flier",
        }, Defaults().AskableFacts);

    [Fact]
    public void FinalConclusions_AreTheSevenAnimals() =>
        Assert.Equal(new[] { "cheetah", "tiger", "giraffe", "zebra", "ostrich", "penguin", "albatross" }, Defaults().FinalConclusions);

    [Fact]
    public void Defaults_HaveNoProblems() => Assert.Empty(Defaults().Problems);

    public static IEnumerable<object[]> Cases() => new[]
    {
        new object[] { 0, new[] { 1, 5, 9 }, "cheetah" },
        new object[] { 1, new[] { 1, 5, 10 }, "tiger" },
        new object[] { 2, new[] { 1, 7, 11 }, "giraffe" },
        new object[] { 3, new[] { 1, 7, 12 }, "zebra" },
        new object[] { 4, new[] { 3, 13 }, "ostrich" },
        new object[] { 5, new[] { 3, 14 }, "penguin" },
        new object[] { 6, new[] { 3, 15 }, "albatross" },
        new object[] { 7, new[] { 1 }, "" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachDescription_FiresTheseRules(int index, int[] expected, string answer)
    {
        var result = ForwardChainer.Run(Defaults(), AnimalRules.Descriptions[index].Yes);
        Assert.Equal(expected, result.Fired.Select(f => f.RuleNumber));
        Assert.All(result.Fired, f => Assert.Equal(1, f.Pass));
        Assert.Equal(answer == "" ? Array.Empty<string>() : new[] { answer }, result.Answers);
        if (answer == "") Assert.Contains("mammal", result.Known);
    }

    [Fact]
    public void Descriptions_AreTheEightAnimalsInOrder() =>
        Assert.Equal(new[] { "cheetah", "tiger", "giraffe", "zebra", "ostrich", "penguin", "albatross", "bat" },
            AnimalRules.Descriptions.Select(d => d.Animal));

    [Fact]
    public void RulesOutOfOrder_NeedASecondPass()
    {
        var rules = new RuleSet(new[] { R("if mammal and eats meat then carnivore"), R("if has hair then mammal") });
        var result = ForwardChainer.Run(rules, Facts("has hair", "eats meat"));
        Assert.Equal(new[] { (2, 1), (1, 2) }, result.Fired.Select(f => (f.RuleNumber, f.Pass)));
    }

    [Fact]
    public void PenguinThatFlies_GetsNoAnswer()
    {
        var yes = new HashSet<string>(AnimalRules.Descriptions[5].Yes) { "flies" };
        Assert.Empty(ForwardChainer.Run(Defaults(), yes).Answers);
    }

    [Fact]
    public void TigerWithSpots_GetsTwoAnswers()
    {
        var yes = new HashSet<string>(AnimalRules.Descriptions[1].Yes) { "dark spots" };
        Assert.Equal(new[] { "cheetah", "tiger" }, ForwardChainer.Run(Defaults(), yes).Answers);
    }

    [Fact]
    public void YesFacts_AreNormalized()
    {
        var result = ForwardChainer.Run(Defaults(), Facts("  HAS  Hair "));
        Assert.Contains("mammal", result.Known);
    }

    [Fact]
    public void NotOnAConcludedFact_IsAProblem_AndTheRuleIsLeftOut()
    {
        var rules = new RuleSet(AnimalRules.DefaultRuleTexts.Select(R).Append(R("if not mammal then fish")));
        var problem = Assert.Single(rules.Problems);
        Assert.Equal(16, problem.RuleNumber);
        Assert.Equal("\"not\" only works with a fact you answer, and a rule concludes \"mammal\".", problem.Message);
        Assert.DoesNotContain("fish", rules.FinalConclusions);
        var result = ForwardChainer.Run(rules, Facts("has feathers"));
        Assert.DoesNotContain(result.Fired, f => f.RuleNumber == 16);
        Assert.DoesNotContain("fish", result.Known);
    }

    [Fact]
    public void RulesThatConcludeEachOther_StillEnd()
    {
        var rules = new RuleSet(new[] { R("if a then b"), R("if b then a") });
        var result = ForwardChainer.Run(rules, Facts("a"));
        Assert.Equal(new[] { "a", "b" }, result.Known.OrderBy(x => x));
        Assert.Equal(1, Assert.Single(result.Fired).RuleNumber);

        var own = new RuleSet(new[] { R("if c and d then c") });
        Assert.Equal("This rule concludes one of its own conditions.", Assert.Single(own.Problems).Message);
        Assert.Empty(ForwardChainer.Run(own, Facts("c", "d")).Fired);
    }
}
