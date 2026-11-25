using System.Reflection;
using System.Text.RegularExpressions;
using AIAnatomy.Lessons;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Models.Vision;
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

    private static string N(long n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

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
        int tokens = AIAnatomy.Models.Tokenization.CharTokenizer.FromText(Fables()).VocabularySize;
        Assert.Equal(69, tokens);   // the 68 characters of the fables plus the unknown token
        // The mean loss of a table that makes every token equally likely is ln V; the why quotes it as 4.234.
        Assert.Equal(Math.Log(tokens), lab.UntrainedLoss, 1e-9);
        Assert.Equal(lab.UntrainedLoss, lab.Loss);
        Assert.StartsWith("Loss 4.234: nothing learned yet", lab.LossText);
        // Final review M5: 69 tokens, not 69 characters (the unknown token is why "�" shows in the gibberish).
        Assert.Contains($"all {tokens} tokens (the {tokens - 1} characters of the fables and the unknown token), and the loss is ln {tokens}, about 4.234",
            QuestionNaming(nameof(RulesVsLearning_UntrainedTableIsUniform)).Why);
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

    // ---- 2.6 Feed-forward layers ----

    [Fact]
    public async Task FeedForward_FablesLayersHave256Neurons()
    {
        var models = Shipped();
        var lab = new FeedForwardLabViewModel(models, Fables);
        await lab.WorkCompleted;
        var config = models.Current.Transformer.Config;
        int neurons = lab.NeuronNumbers.Count;
        Assert.Equal(256, neurons);
        Assert.Equal(neurons, config.FeedForwardWidth);
        Assert.Equal(1, lab.NeuronNumbers[0]);
        Assert.Equal(neurons, lab.NeuronNumbers[^1]);
        Assert.Contains($"of {neurons}:", lab.ActivationCaption);
        // The answer and the why: four times the width of 64, and the Neuron list runs from 1 to 256.
        Assert.Equal(4 * config.ModelWidth, neurons);
        Assert.Equal(64, config.ModelWidth);
        var question = QuestionNaming(nameof(FeedForward_FablesLayersHave256Neurons));
        Assert.Contains($"The Neuron list in the lab runs from 1 to {neurons}.", question.Why);
        Assert.Equal($"{neurons}, four times the width of {config.ModelWidth}", Correct(nameof(FeedForward_FablesLayersHave256Neurons)));
    }

    // ---- 2.7 The residual stream ----

    [Fact]
    public void Residual_FablesVectorGrowsAfterEachBlock()
    {
        var lab = new ResidualLabViewModel(Shipped());
        Assert.Equal("the fox and the ", lab.Text);
        Assert.Equal(lab.PositionChoices.Count - 1, lab.PositionIndex);   // the last token is followed
        var sizes = lab.Sizes.Select(s => s.Value).ToList();
        Assert.Equal(["after the embeddings", "after block 1", "after block 2"], lab.Sizes.Select(s => s.Label));
        for (int i = 1; i < sizes.Count; i++) Assert.True(sizes[i] > sizes[i - 1], $"stage {i} is not longer than stage {i - 1}");
        // The why: "from about 2.2 after the embeddings to about 32 after block 1 and 72 after block 2".
        Assert.Equal(2.2, Math.Round(sizes[0], 1));
        Assert.Equal(32, Math.Round(sizes[1]));
        Assert.Equal(72, Math.Round(sizes[2]));
        var question = QuestionNaming(nameof(Residual_FablesVectorGrowsAfterEachBlock));
        Assert.Contains("about 2.2 after the embeddings to about 32 after block 1 and 72 after block 2", question.Why);
        Assert.Contains($"\"{lab.Text}\"", question.Prompt);
        Assert.Equal("It grows after each block", Correct(nameof(Residual_FablesVectorGrowsAfterEachBlock)));
    }

    [Fact]
    public void Residual_LogitLensLastRowIsTheModelsPrediction()
    {
        var models = Shipped();
        var lab = new ResidualLabViewModel(models);
        var sampling = new SamplingLabViewModel(models);   // the model's own probabilities at temperature 1
        Assert.Equal(lab.Text, sampling.Text);
        var last = lab.LensRows[^1];
        Assert.Equal("after block 2", last.Label);
        Assert.Equal(sampling.Scores.Take(5).Select(s => s.Token), last.Guesses.Select(g => g.Token));
        Assert.Equal(sampling.Scores.Take(5).Select(s => s.ProbabilityText), last.Guesses.Select(g => g.PercentText));
        // The earlier rows are a rougher reading: the first row's top guess is not the model's.
        Assert.NotEqual(last.Guesses[0].Token, lab.LensRows[0].Guesses[0].Token);
        Assert.Equal("The last row, after the final block", Correct(nameof(Residual_LogitLensLastRowIsTheModelsPrediction)));
    }

    // ---- 2.8 Output and sampling ----

    [Fact]
    public void Sampling_TemperatureZeroGivesTheTopChoiceEverything()
    {
        var lab = new SamplingLabViewModel(Shipped());
        Assert.Equal("the fox and the ", lab.Text);
        Assert.Equal(SamplingLabViewModel.DefaultTemperature, lab.Temperature);
        string top = lab.Scores[0].Token;
        lab.Temperature = 0;
        Assert.Equal(1, lab.Probabilities.Count(p => p > 0));
        Assert.Equal(1.0, lab.Probabilities.Max());
        Assert.Equal(top, lab.Scores[0].Token);
        Assert.Equal($"Most likely next character: {top} at 100.00%.", lab.TopChoiceText);
        Assert.Equal("F", top);
        Assert.Contains($"with \"{lab.Text}\" in Your text, {top} then gets 100%", QuestionNaming(nameof(Sampling_TemperatureZeroGivesTheTopChoiceEverything)).Why);
        Assert.Equal("The top choice gets all the probability", Correct(nameof(Sampling_TemperatureZeroGivesTheTopChoiceEverything)));
    }

    [Fact]
    public void Sampling_SameSeedGivesTheSameDraws()
    {
        var models = Shipped();
        var lab = new SamplingLabViewModel(models);
        Assert.Equal(1, lab.Seed);
        lab.SampleHundredCommand.Execute(null);
        var first = lab.SampleCounts.ToList();
        Assert.Equal(SamplingLabViewModel.SampleSize, lab.SampleTotal);
        lab.SampleHundredCommand.Execute(null);
        Assert.Equal(first, lab.SampleCounts);
        var again = new SamplingLabViewModel(models);
        again.SampleHundredCommand.Execute(null);
        Assert.Equal(first, again.SampleCounts);
        // The why: a different seed gives different draws.
        lab.Seed = 2;
        lab.SampleHundredCommand.Execute(null);
        Assert.NotEqual(first, lab.SampleCounts);

        // Draw for draw: the lab's 100 draws (SampleHundred draws from Probabilities with a new SeededRandom(Seed)),
        // made twice with seed 1, are the same sequence, and they add up to the counts the lab shows.
        lab.Seed = 1;
        lab.SampleHundredCommand.Execute(null);
        int[] Draws(int seed)
        {
            var rng = new AIAnatomy.Core.SeededRandom(seed);
            return Enumerable.Range(0, SamplingLabViewModel.SampleSize).Select(_ => AIAnatomy.Models.Language.Sampling.Sample(lab.Probabilities, rng)).ToArray();
        }
        var draws = Draws(lab.Seed);
        Assert.Equal(draws, Draws(lab.Seed));
        Assert.NotEqual(draws, Draws(2));
        var model = models.Current;
        var counted = draws.GroupBy(id => id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new SampleCount(LabText.Visible(model.Tokenizer.TokenText(g.Key)), g.Count())).ToList();
        Assert.Equal(first, counted);
        Assert.Equal(first, lab.SampleCounts);
        Assert.Equal("Exactly the same, draw for draw", Correct(nameof(Sampling_SameSeedGivesTheSameDraws)));
    }

    // ---- 2.9 Learning ----

    [Fact]
    public void Learning_PerceptronNeverLearnsXor()
    {
        var lab = new LearningLabViewModel(Shipped());
        var p = lab.Perceptron;
        foreach (double rate in new[] { PerceptronPanelViewModel.MinRate, PerceptronPanelViewModel.DefaultRate, PerceptronPanelViewModel.MaxRate })
        {
            p.Dataset = PerceptronPanelViewModel.XorName;
            p.Rate = rate;
            Assert.Equal(PerceptronPanelViewModel.XorEpochLimit, p.EpochLimit);
            p.RunCommand.Execute(null);
            Assert.Equal(PerceptronPanelViewModel.XorEpochLimit, p.Epochs.Count);
            Assert.All(p.Epochs, e => Assert.True(e.Errors > 0, $"rate {rate}: epoch {e.Epoch} had no wrong point"));
            Assert.StartsWith("On the wrong side now:", p.WrongPoints);
        }
        // AND and OR are learned, so the epochs are not the problem.
        foreach (var rule in new[] { PerceptronPanelViewModel.AndName, PerceptronPanelViewModel.OrName })
        {
            p.Dataset = rule;
            p.RunCommand.Execute(null);
            Assert.Equal(0, p.Epochs[^1].Errors);
        }
        Assert.Contains($"after {PerceptronPanelViewModel.XorEpochLimit} epochs some points are still wrong", QuestionNaming(nameof(Learning_PerceptronNeverLearnsXor)).Why);
        Assert.Equal("No straight line separates its 1s from its 0s", Correct(nameof(Learning_PerceptronNeverLearnsXor)));
    }

    [Fact]
    public void Learning_RateAbovePointOneOvershoots()
    {
        var lab = new LearningLabViewModel(Shipped());
        var loss = lab.Loss;
        Assert.Equal(0.04, LossPanelViewModel.DefaultRate);
        Assert.False(loss.IsDiverging);
        loss.Rate = 0.12;
        Assert.True(loss.IsDiverging);
        Assert.Equal(LossPanelViewModel.TooLargeMessage, loss.Message);
        Assert.Contains("1 − 20 × 0.12 = −1.400", loss.FactorText);
        var question = QuestionNaming(nameof(Learning_RateAbovePointOneOvershoots));
        Assert.Contains("a Learning rate of 0.12", question.Prompt);
        Assert.Contains("1 − 20 × 0.12 = −1.4", question.Why);
        Assert.Contains("than at 0.04", string.Join(" ", question.Answers));
        Assert.Equal("Each step overshoots further and the path flies off", Correct(nameof(Learning_RateAbovePointOneOvershoots)));
    }

    // ---- 3.1 Training a model ----

    [Fact]
    public void Train_DefaultNewModelHas32005Numbers()
    {
        var models = Shipped();
        var settings = new TrainSettingsPanelViewModel(models, Fables);
        Assert.False(settings.UseOwnText);
        Assert.False(settings.ContinueActive);
        Assert.Equal((32, "2", "4", "64", "600"), (settings.Width, settings.LayersText, settings.HeadsText, settings.ContextText, settings.StepsText));
        long total = settings.ParameterRows.Sum(r => r.Count);
        Assert.Equal(32_005, total);
        Assert.Equal("Total: 32,005 learnable numbers (parameters).", settings.ParameterTotalText);
        // The distractors: the shipped Fables model's total, and the default number of steps.
        long shipped = new ScaleLabViewModel(models).OurCount;
        Assert.Equal(113_093, shipped);
        var question = QuestionNaming(nameof(Train_DefaultNewModelHas32005Numbers));
        Assert.Equal(["32,005", "113,093", TrainSettingsPanelViewModel.DefaultSteps], question.Answers);
        Assert.Contains("Width 32, 2 layers, 4 heads and context 64 give 32,005", question.Why);
        Assert.Contains($"has {N(shipped)}", question.Why);
        Assert.Equal("32,005", Correct(nameof(Train_DefaultNewModelHas32005Numbers)));
    }

    // ---- 3.2 Step through a prediction ----

    [Fact]
    public void StepThrough_ChangingAnEarlierCharacterFirstChangesAttention()
    {
        var models = Shipped();
        var lab = new StepThroughLabViewModel(models);
        Assert.Equal("the fox and the ", lab.Text);
        var before = lab.Stages.ToList();
        lab.Text = "The fox and the ";   // the first character changes; the last token is still followed
        var after = lab.Stages.ToList();
        Assert.Equal(lab.PositionChoices.Count - 1, lab.PositionIndex);
        Assert.Equal(before.Select(s => s.Title), after.Select(s => s.Title));

        static double[] Numbers(AIAnatomy.Presentation.Visuals.PipelineStage s) => s.Values ?? s.Items!.Select(i => i.Value).ToArray();
        int firstChanged = Enumerable.Range(1, after.Count - 1).First(i => !Numbers(before[i]).SequenceEqual(Numbers(after[i])));
        Assert.Equal("Block 1: where each head looks", after[firstChanged].Title);
        Assert.Equal("Block 1: layer norm before attention", after[firstChanged - 1].Title);
        Assert.Equal($"{after[firstChanged].Title}", Correct(nameof(StepThrough_ChangingAnEarlierCharacterFirstChangesAttention)));
    }

    // ---- 3.3 Scale ----

    [Fact]
    public void Scale_FablesFeedForwardHoldsTheMost()
    {
        var lab = new ScaleLabViewModel(Shipped());
        var most = lab.Parts.MaxBy(p => p.Count)!;
        Assert.Equal("Feed-forward", most.Part);
        Assert.Equal(66_176, most.Count);
        Assert.Equal(113_093, lab.OurCount);
        Assert.True(2 * most.Count > lab.OurCount, "more than half");
        Assert.Contains($"{N(most.Count)} of the {N(lab.OurCount)}, more than half", QuestionNaming(nameof(Scale_FablesFeedForwardHoldsTheMost)).Why);
        Assert.Equal(most.Part, Correct(nameof(Scale_FablesFeedForwardHoldsTheMost)));
    }

    [Fact]
    public void Scale_Gpt3IsAbout1Point5MillionTimesFables()
    {
        var lab = new ScaleLabViewModel(Shipped());
        Assert.Equal(175_000_000_000, ScaleLabViewModel.Gpt3Count);
        double ratio = ScaleLabViewModel.Gpt3Count / (double)lab.OurCount;
        Assert.Equal(1.5, Math.Round(ratio / 1e6, 1));
        Assert.Contains("about 1.5 million times", lab.RatioSentence);
        // The why: "just over six steps of ten" on the logarithmic scale.
        Assert.InRange(Math.Log10(ratio), 6, 6.5);
        var question = QuestionNaming(nameof(Scale_Gpt3IsAbout1Point5MillionTimesFables));
        Assert.Contains("175 billion", question.Prompt);
        Assert.Contains($"÷ {N(lab.OurCount)} is about 1.5 million", question.Why);
        Assert.Equal("About 1.5 million times", Correct(nameof(Scale_Gpt3IsAbout1Point5MillionTimesFables)));
    }

    // ---- 4.1 The family tree ----

    [Fact]
    public void FamilyTree_TransformerIsLearnedFromExamples()
    {
        var tree = FamilyTreeData.Load(Path.Combine(TestEnvironment.Content, "family-tree.json"));
        var lab = new FamilyTreeLabViewModel(tree, Catalog, new AIAnatomy.Presentation.IdeaMap.MsaglGraphLayout());
        var node = tree.Nodes.Single(n => n.LessonId == "architecture-map");
        // Walk up the tree, as the lines in the lab go, to the bubble just under the root: one of the three main branches.
        while (node.Parent != tree.Root.Id) node = tree.Nodes.Single(n => n.Id == node.Parent);
        Assert.Contains(lab.Bubbles, b => b.Id == node.Id);
        Assert.Contains(node, tree.ChildrenOf(tree.Root.Id));
        Assert.Equal(node.Label, Correct(nameof(FamilyTree_TransformerIsLearnedFromExamples)));
    }

    // ---- 4.2 Rules: an expert system ----

    [Fact]
    public void ExpertSystem_BatGetsNoAnswerFromTheDefaultRules()
    {
        var lab = new ExpertSystemLabViewModel();
        lab.Describe("bat");
        Assert.Equal("No animal fits these answers.", lab.AnswerText);
        Assert.Equal(lab.AnswerText.TrimEnd('.'), Correct(nameof(ExpertSystem_BatGetsNoAnswerFromTheDefaultRules)));
    }

    // ---- 4.3 Search: looking ahead ----

    [Fact]
    public async Task Search_TheComputerLooksAt549946PositionsForItsFirstMove()
    {
        var lab = new SearchLabViewModel();
        await lab.NewGameAsync(AIAnatomy.Models.Search.Mark.O);   // the computer opens on the empty board
        var count = Regex.Match(lab.CountText, @"^The computer looked at ([\d,]+) positions to choose its move\.$");
        Assert.True(count.Success, lab.CountText);
        Assert.Equal(N(549_946), count.Groups[1].Value);
        Assert.Equal(count.Groups[1].Value, Correct(nameof(Search_TheComputerLooksAt549946PositionsForItsFirstMove)));
    }

    // ---- 4.4 Classic machine learning ----

    [Fact]
    public void ClassicMl_ADepth10TreeGetsEveryDefaultPointRight()
    {
        var lab = new ClassicMlLabViewModel { Depth = ClassicMlLabViewModel.MaxDepth };   // the lab opens on the default set
        Assert.Equal(10, lab.Depth);
        var right = Regex.Match(lab.AccuracyText, @"^Right on (\d+) of (\d+) training points \((\d+)%\)\.$");
        Assert.True(right.Success, lab.AccuracyText);
        Assert.Equal(right.Groups[2].Value, right.Groups[1].Value);   // every point right
        Assert.Equal("100", right.Groups[3].Value);
        Assert.Equal($"All {right.Groups[2].Value} of them", Correct(nameof(ClassicMl_ADepth10TreeGetsEveryDefaultPointRight)));
    }

    // ---- 4.5 Learning by reward ----

    [Fact]
    public void Reward_ASquareTwoStepsFromTheGoalIsWorthEight()
    {
        // The lab's default maze, rewards and discount; exploring at random (rate 1) so every square is tried often.
        var lab = new RewardLabViewModel();
        Assert.Equal((10.0, 1.0, 0.9), (lab.GoalReward, lab.StepCost, lab.Discount));
        var agent = new AIAnatomy.Models.Reinforcement.QLearningAgent(lab.World, lab.Settings with { ExplorationRate = 1 }, lab.Seed);
        for (int i = 0; i < 5000; i++) agent.RunEpisode();
        // (5, 3) is two steps above the goal at (5, 5): down to (5, 4), then down into the goal ((4, 4) is a wall).
        var square = new AIAnatomy.Models.Reinforcement.GridCell(5, 3);
        Assert.Equal(new AIAnatomy.Models.Reinforcement.GridCell(5, 5), lab.World.Goal);
        Assert.Equal(-lab.StepCost + lab.Discount * lab.GoalReward, agent.Value(square), 1e-3);
        Assert.Equal(8, agent.Value(square), 1e-3);
        Assert.Contains("−1 + 0.9 × 10 = 8", QuestionNaming(nameof(Reward_ASquareTwoStepsFromTheGoalIsWorthEight)).Why);
        Assert.Equal("8", Correct(nameof(Reward_ASquareTwoStepsFromTheGoalIsWorthEight)));
    }
    // ---- 4.6 Image networks: filters ----

    [Fact]
    public async Task Filters_OneFilterIsUsedAt64Positions()
    {
        // The lab's 8 by 8 digits with padding 1: the 3 by 3 window is centred on every pixel, so the feature map is 8 by 8,
        // and every one of its 64 numbers is the same 9 weights times the 9 pixels under the window (0 off the image).
        SynchronizationContext.SetSynchronizationContext(null);
        var lab = new FiltersLabViewModel(new FamilyModels(TestEnvironment.Content));
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        lab.ChoosePreset(lab.LearnedPresets[0]);
        var image = lab.Image!;
        var map = lab.FeatureMap!;
        Assert.Equal((8, 8), (map.GetLength(0), map.GetLength(1)));
        var w = lab.Weights;
        int positions = 0;
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                double sum = 0;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int y = r + dr, x = c + dc;
                        if (y is >= 0 and < 8 && x is >= 0 and < 8) sum += w[(dr + 1) * 3 + dc + 1] * image[y, x];
                    }
                Assert.Equal(sum, map[r, c], 1e-12);
                positions++;
            }
        Assert.Equal(64, positions);
        Assert.Equal(FiltersLabViewModel.CountText, $"The same 9 numbers were used at all {positions} positions.");
        Assert.Equal("At all 64 positions, with missing pixels at the edge as 0", Correct(nameof(Filters_OneFilterIsUsedAt64Positions)));
    }

    // ---- 4.7 Image networks: the whole network ----

    [Fact]
    public async Task DigitNetwork_ShippedAccuracyOnTheTestDigits()
    {
        // The shipped network's accuracy, worked out again here on the bundled test digits (the most probable digit against
        // the label), must be the one the lab shows as recorded and the one the answer quotes.
        SynchronizationContext.SetSynchronizationContext(null);
        var family = new FamilyModels(TestEnvironment.Content);
        var (net, info) = await family.DigitsAsync();
        var test = await family.DigitTestSetAsync();
        double accuracy = DigitTrainer.Accuracy(net, test);
        Assert.Equal(info.Results!["testAccuracy"], accuracy, 1e-12);
        int right = (int)Math.Round(accuracy * test.Count);
        Assert.Equal(accuracy, right / (double)test.Count, 1e-12);

        var lab = new DigitNetworkLabViewModel(family);
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        Assert.Contains($"right on {right:N0} of {test.Count:N0} test digits ({accuracy * 100:0.0}%)", lab.RecordedAccuracyText);
        Assert.Equal($"Right on {right:N0} of {test.Count:N0}, about {accuracy * 100:0}%", Correct(nameof(DigitNetwork_ShippedAccuracyOnTheTestDigits)));
    }

    // ---- 4.8 Memory networks ----

    private async Task<MemoryNetworkLabViewModel> MemoryLab()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var lab = new MemoryNetworkLabViewModel(new FamilyModels(TestEnvironment.Content), Shipped(), Fables);
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        return lab;
    }

    [Fact]
    public async Task Memory_BothCurvesFallFastThenLevelOff()
    {
        // The live loss-by-position measurement on the 32 shared validation windows (bands of characters read:
        // 1, 2-4, 5-8, 9-16, 17-32, 33-64). Each curve drops steeply from 1 character to 5-8, then stays within 0.2 of its
        // 33-64 average; and the two curves end close together. So neither network shows a clear gain from characters
        // further back than about 8, and the textbook expectation (the transformer gaining more) is not what this shows.
        var lab = await MemoryLab();
        var bands = lab.MemoryBands;
        Assert.Equal(6, bands.Count);
        foreach (var curve in new Func<MemoryBand, double>[] { b => b.Transformer, b => b.Memory })
        {
            double first = curve(bands[0]), late = curve(bands[5]);
            Assert.True(first - curve(bands[2]) > 0.5, $"drop from 1 to 5-8 characters: {first - curve(bands[2]):F3}");
            for (int i = 2; i < 5; i++)
                Assert.True(Math.Abs(curve(bands[i]) - late) < 0.2, $"band {bands[i].CharactersRead}: {curve(bands[i]):F3} against {late:F3}");
        }
        Assert.True(Math.Abs(bands[5].Transformer - bands[5].Memory) < 0.1, $"33-64: {bands[5].Transformer:F3} against {bands[5].Memory:F3}");
        Assert.Equal("Both fall steeply over the first few characters, then stay about level",
            Correct(nameof(Memory_BothCurvesFallFastThenLevelOff)));
    }

    [Fact]
    public async Task Memory_TheValidationLossesAreAboutTheSame()
    {
        // Recorded: the transformer 1.711, the shipped memory network 1.678, on the same 32 windows. Compared window by window
        // (paired), the memory network is lower there by 0.033 ± 0.012; on all 292 held-back windows the gap is 0.005 ± 0.007,
        // under two standard errors from zero. Each network was trained once, from one seed: no win either way.
        var lab = await MemoryLab();
        Assert.Equal(1.711, lab.TransformerRecordedLoss);
        Assert.Equal(1.678, Math.Round(lab.RnnRecordedLoss, 3));
        var shared = Labs.MemoryMeasurements.MeanAndError(Labs.MemoryMeasurements.PairedGaps(Labs.MemoryMeasurements.Shared));
        Assert.Equal(lab.TransformerRecordedLoss - lab.RnnRecordedLoss, shared.Mean, 3e-3);   // the curves' means are the losses
        var held = Labs.MemoryMeasurements.MeanAndError(Labs.MemoryMeasurements.PairedGaps(Labs.MemoryMeasurements.HeldBack));
        Assert.True(Math.Abs(held.Mean) < 2 * held.Error, $"held-back gap {held.Mean:F4} ± {held.Error:F4}");
        Assert.True(Math.Abs(held.Mean) < 0.02, $"held-back gap {held.Mean:F4}");
        static string F3(double v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        string why = QuestionNaming(nameof(Memory_TheValidationLossesAreAboutTheSame)).Why;
        Assert.Contains($"lower by {F3(shared.Mean)} ± {F3(shared.Error)}", why);
        Assert.Contains($"all 292 windows of the held-back text the gap is only {F3(held.Mean)} ± {F3(held.Error)}", why);
        Assert.Contains("1.711", QuestionNaming(nameof(Memory_TheValidationLossesAreAboutTheSame)).Prompt);
        Assert.Contains("1.678", QuestionNaming(nameof(Memory_TheValidationLossesAreAboutTheSame)).Prompt);
        Assert.Equal("About the same: the gap is too small to call a win", Correct(nameof(Memory_TheValidationLossesAreAboutTheSame)));
    }
}
