using AIAnatomy.Core;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Tests;

public class TrainerTests
{
    private const string CatText = "the cat sat on the mat. the cat sat on the mat. the cat sat on the mat. the cat sat on the mat. ";

    private static (Transformer Model, IReadOnlyList<int> Tokens) Setup(string text = CatText, int seed = 1)
    {
        var tokenizer = CharTokenizer.FromText(text);
        var config = new TransformerConfig(tokenizer.VocabularySize, ContextLength: 16, ModelWidth: 32, Layers: 1, Heads: 2, FeedForwardWidth: 64);
        return (new Transformer(config, new SeededRandom(seed)), tokenizer.Encode(text));
    }

    private sealed class ListProgress : IProgress<TrainingStepReport>
    {
        public List<TrainingStepReport> Reports { get; } = new();
        public Action<TrainingStepReport>? OnReport { get; set; }
        public void Report(TrainingStepReport value) { lock (Reports) Reports.Add(value); OnReport?.Invoke(value); }
    }

    [Fact]
    public void Trainer_OverfitsTinyText()
    {
        var (model, tokens) = Setup();
        var trainer = new Trainer(model, tokens, new TrainingSettings());
        TrainingStepReport last = null!;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 300; i++) last = trainer.Step();
        watch.Stop();
        Assert.True(last.TrainLoss < 0.3, $"train loss {last.TrainLoss}");
        Assert.True(watch.Elapsed.TotalSeconds < 30, $"took {watch.Elapsed.TotalSeconds:F1} s");
    }

    [Fact]
    public void Trainer_SameSeed_SameLosses()
    {
        var losses = new List<double>[2];
        for (int run = 0; run < 2; run++)
        {
            var (model, tokens) = Setup();
            var trainer = new Trainer(model, tokens, new TrainingSettings(Seed: 7));
            losses[run] = Enumerable.Range(0, 5).Select(_ => trainer.Step().TrainLoss).ToList();
        }
        Assert.Equal(losses[0], losses[1]);
    }

    [Fact]
    public void Trainer_BatchOfOne_StillTrains()
    {
        var (model, tokens) = Setup();
        var trainer = new Trainer(model, tokens, new TrainingSettings(BatchSize: 1));
        var first = trainer.Step().TrainLoss;
        for (int i = 0; i < 60; i++) trainer.Step();
        Assert.True(trainer.Step().TrainLoss < first);
    }

    [Fact]
    public void Trainer_DifferentSeed_DifferentLosses()
    {
        double Run(int seed)
        {
            var (model, tokens) = Setup();
            var trainer = new Trainer(model, tokens, new TrainingSettings(Seed: seed));
            return trainer.Step().TrainLoss;
        }
        Assert.NotEqual(Run(1), Run(2));
    }

    [Fact]
    public void Trainer_ValidationLoss_AtStepOneAndEvery25()
    {
        var (model, tokens) = Setup();
        var trainer = new Trainer(model, tokens, new TrainingSettings());
        var reports = Enumerable.Range(0, 50).Select(_ => trainer.Step()).ToList();
        var withValidation = reports.Where(r => r.ValidationLoss.HasValue).Select(r => r.Step).ToArray();
        Assert.Equal(new[] { 1, 25, 50 }, withValidation);
        Assert.True(reports[49].ValidationLoss < reports[0].ValidationLoss);
        Assert.Equal(reports[49].ValidationLoss, trainer.LatestValidationLoss);
    }

    [Fact]
    public void Trainer_HugeLearningRate_StopsWithMessage()
    {
        var (model, tokens) = Setup();
        var trainer = new Trainer(model, tokens, new TrainingSettings(LearningRate: 50));
        TrainingStepReport? stopped = null;
        for (int i = 0; i < 200 && stopped is null; i++)
        {
            var report = trainer.Step();
            if (report.StoppedReason is not null) stopped = report;
        }
        Assert.NotNull(stopped);
        Assert.Equal("Training stopped: the loss became too large (try a smaller learning rate)", stopped!.StoppedReason);
        Assert.True(trainer.HasDiverged);
        Assert.Throws<InvalidOperationException>(() => trainer.Step());
    }

    [Fact]
    public async Task Trainer_RunAsync_StopsAtRequestedCountAndCanRunAgain()
    {
        var (model, tokens) = Setup();
        var trainer = new Trainer(model, tokens, new TrainingSettings());
        var progress = new ListProgress();
        await trainer.RunAsync(5, progress, CancellationToken.None);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, progress.Reports.Select(r => r.Step).ToArray());
        await trainer.RunAsync(3, progress, CancellationToken.None);
        Assert.Equal(8, progress.Reports.Count);
        Assert.Equal(8, progress.Reports[^1].Step);
    }

    [Fact]
    public async Task Trainer_RunAsync_Cancels()
    {
        var (model, tokens) = Setup();
        var trainer = new Trainer(model, tokens, new TrainingSettings());
        using var cts = new CancellationTokenSource();
        var progress = new ListProgress { OnReport = _ => cts.Cancel() };
        await trainer.RunAsync(1000, progress, cts.Token);
        Assert.Single(progress.Reports);
    }

    [Fact]
    public async Task Trainer_RunAsync_StopsOnDivergence()
    {
        var (model, tokens) = Setup();
        var trainer = new Trainer(model, tokens, new TrainingSettings(LearningRate: 50));
        var progress = new ListProgress();
        await trainer.RunAsync(200, progress, CancellationToken.None);
        Assert.NotNull(progress.Reports[^1].StoppedReason);
        Assert.True(trainer.HasDiverged);
        Assert.Single(progress.Reports, r => r.StoppedReason is not null);
    }

    [Fact]
    public void Trainer_TooShortText_ThrowsWithMessage()
    {
        var (model, _) = Setup();
        var ex = Assert.Throws<ArgumentException>(() => new Trainer(model, new[] { 1, 2, 3 }, new TrainingSettings()));
        Assert.Contains("too short", ex.Message);
        // Exactly ContextLength + 2 tokens is enough.
        new Trainer(model, Enumerable.Repeat(1, 18).ToArray(), new TrainingSettings()).Step();
    }

    [Fact]
    public void Trainer_TokenOutsideVocabulary_Throws()
    {
        var (model, tokens) = Setup();
        var bad = tokens.Concat(new[] { 9999 }).ToArray();
        Assert.Throws<ArgumentException>(() => new Trainer(model, bad, new TrainingSettings()));
    }

    [Fact]
    public void CorpusValidator_TooShort_GivesMessage()
    {
        string? problem = CorpusValidator.Problem("short", 64);
        Assert.NotNull(problem);
        Assert.Contains("66", problem);
        Assert.Null(CorpusValidator.Problem(new string('a', 66), 64));
        Assert.NotNull(CorpusValidator.Problem(new string('a', 65), 64));
    }

    [Fact]
    public void CorpusValidator_TooLarge_GivesMessage()
    {
        Assert.NotNull(CorpusValidator.Problem(new string('a', CorpusValidator.MaxCharacters + 1), 64));
        Assert.Null(CorpusValidator.Problem(new string('a', CorpusValidator.MaxCharacters), 64));
    }

    [Fact]
    public void CorpusValidator_Normal_IsNull() => Assert.Null(CorpusValidator.Problem(CatText, 16));
}
