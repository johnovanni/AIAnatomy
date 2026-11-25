using AIAnatomy.Models.Rules;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class ExpertSystemLabTests
{
    private static QuestionAnswer Question(ExpertSystemLabViewModel lab, string fact) => lab.Questions.Single(q => q.Fact == fact);

    private static RuleLine AddRule(ExpertSystemLabViewModel lab, string text)
    {
        lab.AddRule();
        var line = lab.Rules[^1];
        lab.CommitRule(line, text);
        return line;
    }

    [Fact]
    public void Opens_WithTheDefaultRules_AndTheTigerDescribed()
    {
        var lab = new ExpertSystemLabViewModel();
        Assert.Equal(AnimalRules.DefaultRuleTexts, lab.Rules.Select(r => r.Text));
        Assert.Equal(Enumerable.Range(1, 15), lab.Rules.Select(r => r.Number));
        Assert.All(lab.Rules, r => { Assert.Null(r.Error); Assert.True(r.IsUsed); });
        Assert.Equal(AnimalRules.Descriptions.Select(d => d.Animal), lab.Animals);
        Assert.Equal(new RuleSet(AnimalRules.DefaultRuleTexts.Select(t => RuleText.Parse(t).Rule!)).AskableFacts, lab.Questions.Select(q => q.Fact));
        Assert.Equal("Has hair?", lab.Questions[0].Prompt);
        Assert.Equal("Answer: tiger.", lab.AnswerText);
        Assert.Equal([1, 5, 10], lab.Trace.Select(t => t.RuleNumber));
        Assert.All(lab.Trace, t => Assert.Equal(1, t.Pass));
        Assert.Equal("Rule 1: has hair, so mammal.", lab.Trace[0].Text);
        Assert.Equal("Rule 5: mammal and eats meat, so carnivore.", lab.Trace[1].Text);
        Assert.Equal("Rule 10: carnivore and tawny and black stripes, so tiger.", lab.Trace[2].Text);
        Assert.Equal(["has hair", "eats meat", "tawny", "black stripes"], lab.Questions.Where(q => q.IsYes).Select(q => q.Fact));
    }

    [Fact]
    public void Prompt_CapitalisesTheFirstLetterAndAddsAQuestionMark()
    {
        var lab = new ExpertSystemLabViewModel();
        Assert.Equal("Black-and-white?", Question(lab, "black-and-white").Prompt);
        Assert.Equal("Has forward-facing eyes?", Question(lab, "has forward-facing eyes").Prompt);
    }

    [Fact]
    public void TheBat_GetsNoAnswer()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.Describe("bat");
        Assert.Equal("No animal fits these answers.", lab.AnswerText);
        Assert.Equal([1], lab.Trace.Select(t => t.RuleNumber));   // a mammal, and then nothing more follows
        Assert.Equal(["has hair", "gives milk", "flies"], lab.Questions.Where(q => q.IsYes).Select(q => q.Fact));
    }

    [Fact]
    public void DescribeCommand_DescribesTheAnimal()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.DescribeCommand.Execute("albatross");
        Assert.Equal("Answer: albatross.", lab.AnswerText);
        Assert.Throws<ArgumentException>(() => lab.Describe("unicorn"));
    }

    [Theory]
    [InlineData("cheetah")]
    [InlineData("tiger")]
    [InlineData("giraffe")]
    [InlineData("zebra")]
    [InlineData("ostrich")]
    [InlineData("penguin")]
    [InlineData("albatross")]
    public void EveryAnimalButTheBat_GetsItsOwnName(string animal)
    {
        var lab = new ExpertSystemLabViewModel();
        lab.Describe(animal);
        Assert.Equal($"Answer: {animal}.", lab.AnswerText);
    }

    [Fact]
    public void TickingDarkSpotsOnTheTiger_GivesTwoAnswers()
    {
        var lab = new ExpertSystemLabViewModel();
        Question(lab, "dark spots").IsYes = true;
        Assert.Equal("The rules give more than one answer: cheetah and tiger.", lab.AnswerText);
        Assert.Equal([1, 5, 9, 10], lab.Trace.Select(t => t.RuleNumber));
    }

    [Fact]
    public void ThreeAnswers_AreJoinedWithCommasAndAnd()
    {
        var lab = new ExpertSystemLabViewModel();
        Question(lab, "dark spots").IsYes = true;
        AddRule(lab, "if carnivore and tawny then lion");
        Assert.Equal("The rules give more than one answer: cheetah, tiger and lion.", lab.AnswerText);
    }

    [Fact]
    public void CommitRule_WithAParseError_ShowsItAndLeavesTheRuleOut()
    {
        var lab = new ExpertSystemLabViewModel();
        var line = lab.Rules[0];
        lab.CommitRule(line, "if has hair mammal");
        Assert.Equal("if has hair mammal", line.Text);
        Assert.Equal("Put \"then\" before the conclusion.", line.Error);
        Assert.False(line.IsUsed);
        // Without rule 1, "has hair" is no longer a way to "mammal"; the tiger is a mammal only by "gives milk", which it lacks.
        Assert.DoesNotContain(lab.Questions, q => q.Fact == "has hair");
        Assert.Equal("No animal fits these answers.", lab.AnswerText);

        lab.CommitRule(line, "if has hair then mammal");
        Assert.Null(line.Error);
        Assert.True(line.IsUsed);
        Assert.True(Question(lab, "has hair").IsYes);   // the answer comes back with its question
        Assert.Equal("Answer: tiger.", lab.AnswerText);
    }

    [Fact]
    public void ARuleWithAProblem_ShowsItInItsOwnLine()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.CommitRule(lab.Rules[0], "nonsense");   // a parse error on line 1, so line 2 is rule 1 of the rule set
        var line = AddRule(lab, "if not mammal then fish");
        Assert.Equal(16, line.Number);
        Assert.Equal("\"not\" only works with a fact you answer, and a rule concludes \"mammal\".", line.Error);
        Assert.False(line.IsUsed);
        Assert.Equal("Start the rule with \"if\".", lab.Rules[0].Error);
        Assert.All(lab.Rules.Skip(1).Take(14), r => Assert.Null(r.Error));
    }

    [Fact]
    public void TraceNumbers_AreLineNumbers_WhenAnEarlierLineHasAnError()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.CommitRule(lab.Rules[2], "if has feathers bird");   // line 3 is left out of the rule set
        Assert.Equal("Answer: tiger.", lab.AnswerText);
        Assert.Equal([1, 5, 10], lab.Trace.Select(t => t.RuleNumber));   // still the numbers shown beside the lines
    }

    [Fact]
    public void AnEmptyLine_IsNotUsed_AndHasNoError()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.AddRule();
        var line = lab.Rules[^1];
        Assert.Equal(16, line.Number);
        Assert.Equal("", line.Text);
        Assert.Null(line.Error);
        Assert.False(line.IsUsed);
        Assert.Equal("Answer: tiger.", lab.AnswerText);
    }

    [Fact]
    public void AddingABatRule_GivesTheBatAnAnswer()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.Describe("bat");
        AddRule(lab, "if mammal and flies then bat");
        Assert.Equal("Answer: bat.", lab.AnswerText);
        Assert.Equal([1, 16], lab.Trace.Select(t => t.RuleNumber));
        Assert.Equal("Rule 16: mammal and flies, so bat.", lab.Trace[1].Text);
    }

    [Fact]
    public void TheCarelessBatRule_BreaksTheAlbatross()
    {
        // The lesson's Try this: "if flies then bat" fixes the bat but gives the albatross two answers.
        var lab = new ExpertSystemLabViewModel();
        var line = AddRule(lab, "if flies then bat");
        lab.Describe("bat");
        Assert.Equal("Answer: bat.", lab.AnswerText);
        lab.Describe("albatross");
        Assert.Equal("The rules give more than one answer: albatross and bat.", lab.AnswerText);
        lab.CommitRule(line, "if mammal and flies then bat");
        Assert.Equal("Answer: albatross.", lab.AnswerText);
        lab.Describe("bat");
        Assert.Equal("Answer: bat.", lab.AnswerText);
    }

    [Fact]
    public void ANewAskableFact_AddsAQuestion_AndKeepsTheOtherAnswers()
    {
        var lab = new ExpertSystemLabViewModel();
        Question(lab, "dark spots").IsYes = true;
        var before = lab.Questions.ToList();
        AddRule(lab, "if mammal and barks then dog");
        var barks = Question(lab, "barks");
        Assert.Equal("Barks?", barks.Prompt);
        Assert.False(barks.IsYes);
        Assert.Equal(lab.Questions.Count - 1, lab.Questions.IndexOf(barks));   // askable facts are in first-use order
        Assert.All(before, q => Assert.Contains(q, lab.Questions));   // the same question objects, answers kept
        Assert.True(Question(lab, "dark spots").IsYes);
        Assert.Equal(new[] { "has hair", "eats meat", "tawny", "dark spots", "black stripes" }.Order(),
            lab.Questions.Where(q => q.IsYes).Select(q => q.Fact).Order());
    }

    [Fact]
    public void DeleteRule_RenumbersTheRest()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.DeleteRuleCommand.Execute(lab.Rules[0]);
        Assert.Equal(14, lab.Rules.Count);
        Assert.Equal(Enumerable.Range(1, 14), lab.Rules.Select(r => r.Number));
        Assert.Equal(AnimalRules.DefaultRuleTexts[1], lab.Rules[0].Text);
        Assert.Equal("No animal fits these answers.", lab.AnswerText);   // no rule makes the tiger a mammal now
        lab.Describe("bat");
        Assert.Equal([1], lab.Trace.Select(t => t.RuleNumber));   // "gives milk, so mammal" is now rule 1
    }

    [Fact]
    public void AddRule_IsDisabledAtFortyRules()
    {
        var lab = new ExpertSystemLabViewModel();
        while (lab.Rules.Count < ExpertSystemLabViewModel.MaxRules)
        {
            Assert.True(lab.AddRuleCommand.CanExecute(null));
            lab.AddRuleCommand.Execute(null);
        }
        Assert.Equal(40, lab.Rules.Count);
        Assert.False(lab.AddRuleCommand.CanExecute(null));
        lab.AddRule();
        Assert.Equal(40, lab.Rules.Count);
        lab.DeleteRule(lab.Rules[^1]);
        Assert.True(lab.AddRuleCommand.CanExecute(null));
    }

    [Fact]
    public void Reset_RestoresTheRulesAndTheTiger()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.Describe("bat");
        lab.CommitRule(lab.Rules[3], "if oops");
        lab.DeleteRule(lab.Rules[0]);
        AddRule(lab, "if mammal and barks then dog");
        lab.Reset();
        Assert.Equal(AnimalRules.DefaultRuleTexts, lab.Rules.Select(r => r.Text));
        Assert.Equal(Enumerable.Range(1, 15), lab.Rules.Select(r => r.Number));
        Assert.All(lab.Rules, r => Assert.Null(r.Error));
        Assert.DoesNotContain(lab.Questions, q => q.Fact == "barks");
        Assert.Equal(19, lab.Questions.Count);
        Assert.Equal("Answer: tiger.", lab.AnswerText);
        Assert.Equal([1, 5, 10], lab.Trace.Select(t => t.RuleNumber));
    }

    [Fact]
    public void TracePasses_GroupByPass_WithHeadingsOnlyWhenThereIsMoreThanOne()
    {
        var lab = new ExpertSystemLabViewModel();
        var one = Assert.Single(lab.TracePasses);
        Assert.Null(one.Heading);
        Assert.Null(lab.TraceMessage);

        // A rule for "tawny" written after the cheetah and tiger rules: the tiger rule can only fire on a second pass.
        lab.CommitRule(lab.Rules[9], "if carnivore and orange and black stripes then tiger");
        AddRule(lab, "if has fur then orange");
        Question(lab, "has fur").IsYes = true;
        Assert.Equal("Answer: tiger.", lab.AnswerText);
        Assert.Equal([(1, 1), (1, 5), (1, 16), (2, 10)], lab.Trace.Select(t => (t.Pass, t.RuleNumber)));
        Assert.Equal(["Pass 1", "Pass 2"], lab.TracePasses.Select(p => p.Heading));
        Assert.Equal([1, 5, 16], lab.TracePasses[0].Lines.Select(l => l.RuleNumber));
    }

    [Fact]
    public void NoRuleFired_SaysSo()
    {
        var lab = new ExpertSystemLabViewModel();
        foreach (var q in lab.Questions) q.IsYes = false;
        Assert.Empty(lab.Trace);
        Assert.Empty(lab.TracePasses);
        Assert.Equal("No rule fired.", lab.TraceMessage);
        Assert.Equal("No animal fits these answers.", lab.AnswerText);
    }

    [Fact]
    public void DescribedText_SaysWhichAnimal_UntilAnAnswerIsChangedByHand()
    {
        var lab = new ExpertSystemLabViewModel();
        Assert.Equal("Answered for a tiger.", lab.DescribedText);
        lab.Describe("albatross");
        Assert.Equal("Answered for an albatross.", lab.DescribedText);
        Question(lab, "swims").IsYes = true;
        Assert.Equal("Your own answers.", lab.DescribedText);
    }

    [Fact]
    public void Screens_AreBaselines_AndLightOnceOnDescribe()
    {
        var lab = new ExpertSystemLabViewModel();
        Assert.Equal(["trace", "answer"], lab.ReportedScreens);
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => raised.Add(e.Screen);

        lab.Describe("bat");
        Assert.Equal(["trace", "answer"], raised);
        raised.Clear();
        lab.Describe("bat");
        Assert.Empty(raised);

        Question(lab, "eats meat").IsYes = true;   // mammal and eats meat: carnivore, still no animal
        Assert.Equal(["trace"], raised);
        raised.Clear();

        lab.AddRule();
        Assert.Empty(raised);   // an empty line changes nothing
        lab.CommitRule(lab.Rules[^1], "if carnivore and flies then vampire bat");
        Assert.Equal(["trace", "answer"], raised);
    }

    [Fact]
    public void CommitRule_ForALineNotInTheList_Throws()
    {
        var lab = new ExpertSystemLabViewModel();
        var other = new ExpertSystemLabViewModel().Rules[0];
        Assert.Throws<ArgumentException>(() => lab.CommitRule(other, "if a then b"));
    }

    [Fact]
    public void IsRegisteredInTheFactory()
    {
        using var env = new TestEnvironment();
        var factory = new LabFactory(env.NewActiveModels(), TestEnvironment.Content);
        Assert.IsType<ExpertSystemLabViewModel>(factory.Create(ExpertSystemLabViewModel.Id));
    }
}
