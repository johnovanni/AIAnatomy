using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class RulesVsLearningTests
{
    private static string Fables() => File.ReadAllText(Path.Combine(TestEnvironment.Content, "texts", "aesop-fables.txt"));

    [Fact]
    public void RuleOutput_ShownWithRules()
    {
        var lab = new RulesVsLearningLabViewModel(Fables);
        Assert.Equal(10, lab.Rules.Count);
        Assert.Equal("1. After q, write u.", lab.Rules[0]);
        Assert.Equal(RulesVsLearningLabViewModel.OutputLength, lab.RuleOutput.Length);
        Assert.StartsWith("the", lab.RuleOutput);   // after "The ": a space gives t, then h, then e
        Assert.Equal(0, lab.StepsTrained);
    }

    [Fact]
    public async Task Learn_LowersLoss_AndChangesOutput()
    {
        var lab = new RulesVsLearningLabViewModel(Fables) { ChunksPerRun = 3 };
        string before = lab.LearnedOutput;
        double untrained = lab.Loss;
        Assert.Equal(untrained, lab.UntrainedLoss);
        Assert.InRange(untrained, 4.0, 4.4);   // uniform guessing over about 70 characters: ln 70 = 4.25

        await lab.LearnCommand.ExecuteAsync(null);

        Assert.Equal(60, lab.StepsTrained);
        Assert.True(lab.Loss < untrained - 0.5, $"{untrained} -> {lab.Loss}");
        Assert.NotEqual(before, lab.LearnedOutput);
        Assert.Equal(4, lab.LossSeries[0].Points.Count);   // step 0 and one point per chunk
        Assert.False(lab.IsLearning);
    }

    [Fact]
    public async Task ShownLoss_IsTheLossAfterTheLastStep()
    {
        // Final review M8: "Loss X after 20 steps" is measured after step 20, not before it.
        var lab = new RulesVsLearningLabViewModel(Fables) { ChunksPerRun = 1 };
        await lab.LearnCommand.ExecuteAsync(null);

        string text = Fables();
        var tokenizer = AIAnatomy.Models.Tokenization.CharTokenizer.FromText(text);
        var corpus = tokenizer.Encode(text[..RulesVsLearningLabViewModel.CorpusCharacters]);
        var model = new AIAnatomy.Models.Toys.BigramModel(tokenizer, 1);
        double beforeLastStep = model.TrainSteps(corpus, RulesVsLearningLabViewModel.StepsPerChunk, RulesVsLearningLabViewModel.LearningRate);
        var previous = corpus.Take(corpus.Count - 1).ToArray();
        var next = corpus.Skip(1).ToArray();
        double after = AIAnatomy.Core.Losses.CrossEntropy(AIAnatomy.Core.Ops.Rows(model.Logits, previous), next).Item();

        Assert.Equal(after, lab.Loss, 12);
        Assert.NotEqual(beforeLastStep, lab.Loss);
        Assert.Contains($"after {RulesVsLearningLabViewModel.StepsPerChunk} steps", lab.LossText);
    }

    [Fact]
    public async Task StartTextEditedWhileLearning_EndsWithTheOutputForTheNewText()
    {
        // Final review M8: the learned output is written only between chunks, from a table that is not changing.
        var lab = new RulesVsLearningLabViewModel(Fables) { ChunksPerRun = 3 };
        var run = lab.LearnCommand.ExecuteAsync(null);
        lab.StartText = "Once ";
        Assert.StartsWith("Once ", lab.StartText);
        await run;
        string shown = lab.LearnedOutput;
        lab.StartText = "x";
        lab.StartText = "Once ";
        Assert.Equal(shown, lab.LearnedOutput);
    }

    [Fact]
    public async Task Learn_LossFallsEveryChunk_AtTheLabsLearningRate()
    {
        var lab = new RulesVsLearningLabViewModel(Fables) { ChunksPerRun = 20 };
        await lab.LearnCommand.ExecuteAsync(null);
        var points = lab.LossSeries[0].Points;
        Assert.Equal(21, points.Count);
        for (int i = 1; i < points.Count; i++) Assert.True(points[i].Y < points[i - 1].Y, $"chunk {i}: {points[i - 1].Y} -> {points[i].Y}");
    }

    [Fact]
    public async Task Reset_RestoresUntrainedModel()
    {
        var lab = new RulesVsLearningLabViewModel(Fables) { ChunksPerRun = 2 };
        string untrainedOutput = lab.LearnedOutput;
        double untrained = lab.Loss;
        await lab.LearnCommand.ExecuteAsync(null);
        lab.Seed = 7;
        lab.StartText = "Once ";

        lab.Reset();

        Assert.Equal(0, lab.StepsTrained);
        Assert.Equal(untrained, lab.Loss);
        Assert.Equal(untrainedOutput, lab.LearnedOutput);
        Assert.Equal(1, lab.Seed);
        Assert.Equal(RulesVsLearningLabViewModel.DefaultStart, lab.StartText);
        Assert.Single(lab.LossSeries[0].Points);
    }

    [Fact]
    public void EmptyStart_ShowsPrompt_NotAnError()
    {
        var lab = new RulesVsLearningLabViewModel(Fables) { StartText = "" };
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Equal("", lab.RuleOutput);
        Assert.Equal("", lab.LearnedOutput);
    }

    [Fact]
    public void Seed_ChangesTheLearnedOutput_AndSameSeedRepeats()
    {
        var lab = new RulesVsLearningLabViewModel(Fables);
        string one = lab.LearnedOutput;
        lab.Seed = 2;
        Assert.NotEqual(one, lab.LearnedOutput);
        lab.Seed = 1;
        Assert.Equal(one, lab.LearnedOutput);
    }

    [Fact]
    public async Task LeavingTheLab_StopsLearning()
    {
        var lab = new RulesVsLearningLabViewModel(Fables) { ChunksPerRun = 20 };
        var running = lab.LearnCommand.ExecuteAsync(null);
        lab.Deactivate();
        await running;
        Assert.True(lab.StepsTrained < 400);
    }
}
