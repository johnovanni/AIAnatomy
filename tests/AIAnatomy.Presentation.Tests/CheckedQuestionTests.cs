using System.Reflection;
using System.Text.RegularExpressions;
using AIAnatomy.Lessons;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

/// <summary>
/// One test for every Check yourself question whose answer depends on the app's own models, tokenizers or text
/// (its <c>checked-by-code:</c> line names the test). Each test works the answer out the way the lab does, through the
/// lab's view-model, with the shipped Fables model and the lab's default settings, and checks that the answer marked
/// correct in the lesson file says the same. If the shipped model, tokenizer or text changes, these fail rather than
/// a question going quietly wrong.
/// </summary>
public class CheckedQuestionTests : IDisposable
{
    private static readonly LessonCatalog Catalog = LessonCatalog.Load(TestEnvironment.Content);
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private static string Fables() => File.ReadAllText(Path.Combine(TestEnvironment.Content, "texts", "aesop-fables.txt"));

    /// <summary>The shipped Fables model, as a fresh install has it.</summary>
    private ActiveModelService Shipped()
    {
        var models = _env.NewActiveModels();
        Assert.Equal(ActiveModelService.DefaultModelName, models.Current.Info.Name);
        return models;
    }

    /// <summary>The text of the answer marked correct in the one question that names this test.</summary>
    private static string Correct(string testName) => QuestionNaming(testName).Answers[QuestionNaming(testName).CorrectIndex];

    private static Question QuestionNaming(string testName) =>
        Assert.Single(Catalog.All.SelectMany(l => l.Questions), q => q.CheckedBy == testName);

    // ---- the checks on the checks ----

    private static IReadOnlyList<string> CheckTests() =>
        typeof(CheckedQuestionTests).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<FactAttribute>() != null && m.Name.Contains('_'))
            .Where(m => !m.Name.StartsWith("Every", StringComparison.Ordinal))
            .Select(m => m.Name).ToList();

    [Fact]
    public void EveryCheckedByCode_NamesATestInThisClass()
    {
        var tests = CheckTests().ToHashSet();
        var missing = Catalog.All.SelectMany(l => l.Questions.Where(q => q.CheckedBy != null && !tests.Contains(q.CheckedBy))
            .Select(q => $"{l.Id}: '{q.CheckedBy}'")).ToList();
        Assert.True(missing.Count == 0, "checked-by-code names no test in CheckedQuestionTests: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryCheckTest_IsNamedByExactlyOneQuestion()
    {
        var named = Catalog.All.SelectMany(l => l.Questions).Where(q => q.CheckedBy != null)
            .GroupBy(q => q.CheckedBy!).ToDictionary(g => g.Key, g => g.Count());
        var problems = CheckTests().Where(t => named.GetValueOrDefault(t) != 1)
            .Select(t => $"{t} is named by {named.GetValueOrDefault(t)} questions").ToList();
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    // ---- 1.1 Rules versus learning ----

    [Fact]
    public void RulesVsLearning_LearnsFromTheOpeningPagesOfTheFables()
    {
        var lab = new RulesVsLearningLabViewModel(Fables);
        Assert.Contains("It learns from the opening pages of the fables", lab.LearnBlurb);
        string fables = Fables();
        Assert.StartsWith("AESOP'S FABLES", fables);
        Assert.True(RulesVsLearningLabViewModel.CorpusCharacters < fables.Length / 10, "the lab learns from only the opening pages");
        Assert.Equal("Training on examples from the opening pages of Aesop's fables", Correct(nameof(RulesVsLearning_LearnsFromTheOpeningPagesOfTheFables)));
    }

    [Fact]
    public void RulesVsLearning_UntrainedTableIsUniform()
    {
        var lab = new RulesVsLearningLabViewModel(Fables);
        Assert.Equal(0, lab.StepsTrained);
        int characters = AIAnatomy.Models.Tokenization.CharTokenizer.FromText(Fables()).VocabularySize;
        Assert.Equal(69, characters);
        // The mean loss of a table that makes every character equally likely is ln V; the why quotes it as 4.234.
        Assert.Equal(Math.Log(characters), lab.UntrainedLoss, 1e-9);
        Assert.Equal(lab.UntrainedLoss, lab.Loss);
        Assert.StartsWith("Loss 4.234: nothing learned yet", lab.LossText);
        Assert.Contains($"all {characters} characters, and the loss is ln {characters}, about 4.234", QuestionNaming(nameof(RulesVsLearning_UntrainedTableIsUniform)).Why);
        Assert.Equal("Every score starts at zero, so every character is equally likely", Correct(nameof(RulesVsLearning_UntrainedTableIsUniform)));
    }

    [Fact]
    public void RulesVsLearning_RulesRepeatTheFromTheStartText()
    {
        var lab = new RulesVsLearningLabViewModel(Fables);
        Assert.Equal("The ", lab.StartText);
        Assert.Equal(string.Concat(Enumerable.Repeat("the ", RulesVsLearningLabViewModel.OutputLength / 4)), lab.RuleOutput);
        Assert.Equal("\"the the the ...\", over and over", Correct(nameof(RulesVsLearning_RulesRepeatTheFromTheStartText)));
    }

    // ---- 1.2 The architecture map ----

    [Fact]
    public void ArchitectureMap_FablesModelHasTwoBlocks()
    {
        var lab = new ArchitectureMapLabViewModel(Shipped());
        var block = Assert.Single(lab.Nodes, n => n.Id == "block");
        Assert.Equal("Block × 2", block.Label);
        Assert.Equal("2", Correct(nameof(ArchitectureMap_FablesModelHasTwoBlocks)));
    }

    [Fact]
    public void ArchitectureMap_FablesTopChoiceIsCapitalF()
    {
        var lab = new ArchitectureMapLabViewModel(Shipped());
        Assert.Equal("The fox and the ", lab.Text);
        string top = lab.Top10[0].Label;
        Assert.Equal("F", top);
        // The why: nine of the top ten are capitals, and in the fables a capital follows "and the " more often than a small letter.
        Assert.Equal(9, lab.Top10.Count(i => i.Label.Length == 1 && char.IsUpper(i.Label[0])));
        string fables = Fables();
        Assert.True(Regex.Count(fables, "and the [A-Z]") > 5 * Regex.Count(fables, "and the [a-z]"));
        Assert.Equal($"Capital {top}", Correct(nameof(ArchitectureMap_FablesTopChoiceIsCapitalF)));
    }

    // ---- 2.1 Tokens ----

    [Fact]
    public void Tokens_FoxAndCrowIsTwentyCharacterTokens()
    {
        var lab = new TokensLabViewModel(Fables, TimeSpan.Zero);
        Assert.Equal("The fox and the crow", lab.Text);
        Assert.Equal(20, lab.CharacterTokenCount);
        Assert.Equal($"{lab.CharacterTokenCount}, one per character", Correct(nameof(Tokens_FoxAndCrowIsTwentyCharacterTokens)));
    }

    [Fact]
    public async Task Tokens_StrawberryIsSevenPiecesAtDefaultMerges()
    {
        var lab = new TokensLabViewModel(Fables, TimeSpan.Zero);
        await lab.BpeReady;
        Assert.Equal(TokensLabViewModel.DefaultMerges, lab.Merges);
        Assert.Equal("strawberry", lab.Word);
        int pieces = lab.WordChips.Count;
        Assert.Equal(7, pieces);
        Assert.Equal(3, "strawberry".Count(c => c == 'r'));
        var question = QuestionNaming(nameof(Tokens_StrawberryIsSevenPiecesAtDefaultMerges));
        Assert.Contains($"at its default {TokensLabViewModel.DefaultMerges}, the BPE tokenizer", question.Prompt);
        Assert.Contains($"cuts \"strawberry\" into {pieces} pieces", question.Prompt);
        Assert.Contains($"With Merges at {TokensLabViewModel.DefaultMerges}, the chips under Count the letters show the {pieces} pieces.", question.Why);
        Assert.Equal($"{pieces} ids, one per piece, with no letters inside", Correct(nameof(Tokens_StrawberryIsSevenPiecesAtDefaultMerges)));
    }

    [Fact]
    public void Tokens_EmojiIsTheUnknownToken()
    {
        var lab = new TokensLabViewModel(Fables, TimeSpan.Zero) { Text = "\U0001F642" };
        var chip = Assert.Single(lab.CharacterChips);
        Assert.True(chip.IsUnknown);
        Assert.Equal("�", chip.Text);
        // The why: the shipped Fables model has the same 69 tokens.
        var model = Shipped().Current;
        Assert.Equal(69, model.Tokenizer.VocabularySize);
        Assert.Equal([model.Tokenizer.UnknownId], model.Tokenizer.Encode("\U0001F642"));
        Assert.Equal($"Gives it the unknown token, shown as {chip.Text}", Correct(nameof(Tokens_EmojiIsTheUnknownToken)));
    }

    // ---- 2.2 Embeddings ----

    [Fact]
    public void Embeddings_FablesEmbeddingsHave64Numbers()
    {
        var lab = new EmbeddingsLabViewModel(Shipped()) { SelectedIndex = 1 };
        int numbers = lab.TrainedVector!.Length;
        Assert.Equal(64, numbers);
        Assert.Contains("is a list of 64 numbers", lab.SelectedSummary);
        Assert.Equal($"{numbers}", Correct(nameof(Embeddings_FablesEmbeddingsHave64Numbers)));
    }

    // ---- 2.3 Position ----

    [Fact]
    public void Position_FablesUsesLearnedPositions()
    {
        var models = Shipped();
        var lab = new PositionLabViewModel(models);
        Assert.Equal(PositionKind.Learned, models.Current.Transformer.Config.Positions);
        Assert.Equal("This model's position vectors (learned)", lab.ModelPositionsTitle);
        Assert.Equal(64, lab.PositionLabels.Count);
        Assert.Equal("Learned ones, trained like everything else", Correct(nameof(Position_FablesUsesLearnedPositions)));
    }

    [Fact]
    public void Position_FablesTopChoiceMovesFromFToT()
    {
        var lab = new PositionLabViewModel(Shipped());
        Assert.Equal("The fox and the ", lab.Text);
        string with = lab.WithPositions[0].Label, without = lab.WithoutPositions[0].Label;
        Assert.Equal(("F", "t"), (with, without));
        // The why: "about two thirds of the probability moves".
        var moved = Regex.Match(lab.Comparison, @"([\d.]+)% of the probability moved");
        Assert.True(moved.Success, lab.Comparison);
        Assert.InRange(double.Parse(moved.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 60, 72);
        Assert.Equal($"It changes from {with} to {without}", Correct(nameof(Position_FablesTopChoiceMovesFromFToT)));
    }

    // ---- 2.4 Attention ----

    [Fact]
    public void Attention_ToyStartGivesK1TheMostWeight()
    {
        var lab = new AttentionLabViewModel(Shipped());
        var rows = lab.Toy.Rows;
        Assert.Equal([1.0, 0.5, -0.75], rows.Select(r => r.Dot));
        var heaviest = rows.MaxBy(r => r.Weight)!;
        Assert.Equal("K1", heaviest.Name);
        Assert.Equal(0.50, heaviest.Weight, 2);
        Assert.Equal($"{heaviest.Name}: Q · {heaviest.Name} is the largest dot product", Correct(nameof(Attention_ToyStartGivesK1TheMostWeight)));
    }

    // ---- 2.5 Multi-head attention ----

    [Fact]
    public void MultiHead_FablesHeadsUse16NumbersEach()
    {
        var models = Shipped();
        var lab = new MultiHeadLabViewModel(models);
        int width = models.Current.Transformer.Config.HeadWidth;
        Assert.Equal(16, width);
        Assert.Equal(4, lab.Heads.Count);
        Assert.All(lab.Heads, h => Assert.Equal(width, h.Output!.Length));
        Assert.Equal(64, lab.ConcatenatedVector!.Length);
        Assert.Equal($"{width}", Correct(nameof(MultiHead_FablesHeadsUse16NumbersEach)));
    }

    [Fact]
    public void MultiHead_FablesLayer1Head2LooksAtThePreviousToken()
    {
        var lab = new MultiHeadLabViewModel(Shipped());
        Assert.Equal("the fox and the ", lab.Text);
        Assert.Equal(1, lab.Layer);
        var weights = lab.Heads[1].Weights!;
        for (int row = 1; row < weights.GetLength(0); row++)
        {
            int darkest = Enumerable.Range(0, weights.GetLength(1)).MaxBy(column => weights[row, column]);
            Assert.True(darkest == row - 1, $"row {row + 1} looks most at token {darkest + 1}");
        }
        Assert.Equal("At the token just before", Correct(nameof(MultiHead_FablesLayer1Head2LooksAtThePreviousToken)));
    }
}
