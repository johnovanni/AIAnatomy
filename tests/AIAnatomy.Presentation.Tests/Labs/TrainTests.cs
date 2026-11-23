using AIAnatomy.Models.Training;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>Train lab tests train real models with all cores, so they run on their own (see <see cref="TrainingCollection"/>).</summary>
[Collection(TrainingCollection.Name)]
public class TrainTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(120);
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private static readonly Lazy<string> Fables = new(() => File.ReadAllText(Path.Combine(TestEnvironment.Content, "texts", "aesop-fables.txt")));

    private TrainLabViewModel NewLab(ActiveModelService? models = null, Action? libraryChanged = null) =>
        new(models ?? _env.NewActiveModels(), () => Fables.Value, libraryChanged);

    /// <summary>A very small fresh model, so a test trains in well under a second per few steps.</summary>
    private TrainLabViewModel TinyLab(ActiveModelService? models = null, int steps = 5)
    {
        var lab = NewLab(models);
        lab.Width = 16;
        lab.LayersText = "1";
        lab.HeadsText = "2";
        lab.ContextText = "16";
        lab.BatchText = "2";
        lab.StepsText = steps.ToString();
        Assert.Null(lab.StartProblem);
        return lab;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition did not come true in time.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public void Defaults_AreTheDocumentedOnes()
    {
        var lab = NewLab();
        Assert.False(lab.UseOwnText);
        Assert.False(lab.ContinueActive);
        Assert.Equal(32, lab.Width);
        Assert.Equal("2", lab.LayersText);
        Assert.Equal("4", lab.HeadsText);
        Assert.Equal("64", lab.ContextText);
        Assert.Equal("0.003", lab.LearningRateText);
        Assert.Equal("8", lab.BatchText);
        Assert.Equal("600", lab.StepsText);
        Assert.Equal(1, lab.Seed);
        Assert.Null(lab.StartProblem);
        Assert.True(lab.StartCommand.CanExecute(null));
    }

    [Fact]
    public void InvalidHeads_ShowsMessage()
    {
        var lab = NewLab();
        lab.HeadsText = "3";
        Assert.Equal("The number of heads must divide the width evenly: 32 can't be split into 3 heads.", lab.SettingsProblem);
        Assert.Equal(lab.SettingsProblem, lab.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));
        Assert.Empty(lab.ParameterRows);

        lab.HeadsText = "9";
        Assert.Equal("The number of heads must be a whole number from 1 to 8.", lab.SettingsProblem);
        lab.HeadsText = "8";
        Assert.Null(lab.SettingsProblem);
        Assert.True(lab.StartCommand.CanExecute(null));
    }

    [Fact]
    public void OtherBadSettings_ShowPlainMessages()
    {
        var lab = NewLab();
        lab.LayersText = "5";
        lab.ContextText = "abc";
        lab.LearningRateText = "0";
        lab.StepsText = "-1";
        lab.BatchText = "";
        Assert.Equal(string.Join("\n",
            "The number of layers must be a whole number from 1 to 4.",
            "The context length must be a whole number from 16 to 128.",
            "The learning rate must be a number above 0 and no more than 100, such as 0.003.",
            "The number of steps must be a whole number from 1 to 100,000.",
            "The batch size must be a whole number from 1 to 64."), lab.SettingsProblem);
        lab.LearningRateText = "3e-3";
        Assert.DoesNotContain("learning rate", lab.SettingsProblem);
    }

    [Fact]
    public void TooShortText_DisablesStart()
    {
        var lab = NewLab();
        lab.UseOwnText = true;
        Assert.Equal("Type some text to begin", lab.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));

        lab.OwnText = "short";
        Assert.Equal(CorpusValidator.Problem("short", 64), lab.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));

        lab.ContextText = "16";
        lab.OwnText = new string('a', 17);
        Assert.Equal(CorpusValidator.Problem(lab.OwnText, 16), lab.StartProblem);
        lab.OwnText = "the cat sat on the mat";
        Assert.Null(lab.StartProblem);
        Assert.True(lab.StartCommand.CanExecute(null));

        lab.OwnText = new string('a', CorpusValidator.MaxCharacters + 1);
        Assert.Equal(CorpusValidator.Problem(lab.OwnText, 16), lab.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));
    }

    [Fact]
    public void ParameterCount_IsByPartInTheFixedOrder_AndAddsUpToTheConfig()
    {
        var lab = NewLab();
        Assert.Equal(TrainLabViewModel.PartOrder, lab.ParameterRows.Select(r => r.Part).ToList());
        Assert.Equal(lab.CurrentConfig()!.CountParameters(), lab.ParameterRows.Sum(r => r.Count));
        Assert.Contains("32,005", lab.ParameterTotalText);   // width 32, 2 layers, 4 heads, context 64 on the fables' 69 tokens

        long before = lab.ParameterRows.Sum(r => r.Count);
        lab.LayersText = "3";
        Assert.True(lab.ParameterRows.Sum(r => r.Count) > before);   // updates live
        Assert.Equal(lab.CurrentConfig()!.CountParameters(), lab.ParameterRows.Sum(r => r.Count));
    }

    [Fact]
    public async Task Start_ProducesReports_AndChartPoints()
    {
        var lab = TinyLab();
        var run = lab.StartCommand.ExecuteAsync(null);
        Assert.True(lab.IsTraining);
        await run.WaitAsync(Patience);

        Assert.False(lab.IsTraining);
        Assert.Equal(5, lab.Reports.Count);
        Assert.All(lab.Reports, r => Assert.True(double.IsFinite(r.TrainLoss)));
        Assert.Equal(5, lab.StepsDone);
        Assert.Equal(1.0, lab.Progress);
        var train = lab.LossSeries.Single(s => s.Name == TrainLabViewModel.TrainSeriesName);
        Assert.Equal(new double[] { 1, 2, 3, 4, 5 }, train.Points.Select(p => p.X).ToArray());
        var validation = lab.LossSeries.Single(s => s.Name == TrainLabViewModel.ValidationSeriesName);
        Assert.Equal(1, validation.Points.Single().X);   // measured at step 1, then every 25 steps
        Assert.Matches(@"^about [0-9.,]+ steps per second", lab.SpeedText);
        Assert.Null(lab.Message);
        Assert.True(lab.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Samples_AreWrittenEvery50Steps_200CharactersLong()
    {
        var lab = TinyLab(steps: 100);
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        Assert.Equal(new[] { 100, 50 }, lab.Samples.Select(s => s.Step).ToArray());   // newest first
        Assert.All(lab.Samples, s => Assert.Equal(TrainLabViewModel.SampleLength, s.Text.Length));
    }

    [Fact]
    public async Task Pause_ThenResume_ContinuesStepCount()
    {
        var lab = TinyLab(steps: 100_000);
        var run = lab.StartCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone >= 2);
        lab.PauseCommand.Execute(null);
        await run.WaitAsync(Patience);

        Assert.True(lab.IsPaused);
        Assert.False(lab.IsTraining);
        int paused = lab.StepsDone;
        Assert.True(lab.ResumeCommand.CanExecute(null));
        Assert.True(lab.SaveCommand.CanExecute(null));   // a paused model can be saved

        var resumed = lab.ResumeCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone >= paused + 2);
        lab.StopCommand.Execute(null);
        await resumed.WaitAsync(Patience);

        Assert.False(lab.IsPaused);
        Assert.False(lab.ResumeCommand.CanExecute(null));
        var steps = lab.Reports.Select(r => r.Step).ToList();
        Assert.Equal(Enumerable.Range(1, steps.Count), steps);   // the count carried on from where it paused, with no restart
        Assert.True(steps.Count > paused);
        Assert.True(lab.SaveCommand.CanExecute(null));   // a stopped model can still be saved
    }

    [Fact]
    public async Task Deactivate_CancelsTraining()
    {
        var lab = TinyLab(steps: 100_000);
        var run = lab.StartCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone >= 1);
        lab.Deactivate();
        Assert.False(lab.IsTraining);
        await run.WaitAsync(Patience);
        await lab.WorkCompleted.WaitAsync(Patience);
        Assert.True(lab.StepsDone < 100_000);
    }

    [Fact]
    public async Task Diverged_DisablesSave()
    {
        var lab = TinyLab(steps: 200);
        lab.LearningRateText = "50";
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);

        Assert.True(lab.HasDiverged);
        Assert.Equal(Trainer.DivergedMessage, lab.Message);
        Assert.False(lab.SaveCommand.CanExecute(null));
        Assert.False(lab.UseInLabsCommand.CanExecute(null));
        Assert.False(lab.ResumeCommand.CanExecute(null));
        Assert.True(lab.StepsDone < 200);
    }

    [Fact]
    public async Task UseInLabs_ChangesActiveModel()
    {
        var models = _env.NewActiveModels();
        int refreshed = 0;
        var lab = new TrainLabViewModel(models, () => Fables.Value, () => refreshed++);
        lab.Width = 16; lab.LayersText = "1"; lab.HeadsText = "2"; lab.ContextText = "16"; lab.BatchText = "2"; lab.StepsText = "5";
        lab.Seed = 7;
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = "My tiny model";
        await lab.UseInLabsCommand.ExecuteAsync(null).WaitAsync(Patience);

        var info = models.Current.Info;
        Assert.Equal("My tiny model", info.Name);
        Assert.False(info.IsShipped);
        Assert.Equal(TrainLabViewModel.FablesTextName, info.TrainingTextName);
        Assert.Equal(5, info.StepsTrained);
        Assert.Equal(7, info.Seed);
        Assert.Equal(lab.Reports.Last(r => r.ValidationLoss != null).ValidationLoss, info.ValidationLoss);
        Assert.Equal(16, models.Current.Transformer.Config.ModelWidth);
        Assert.Contains(models.Library.List(), e => e.Name == "My tiny model" && !e.IsShipped);
        Assert.True(refreshed >= 1);
    }

    [Fact]
    public async Task TrainingDoesNotTouchActiveModel()
    {
        var models = _env.NewActiveModels();
        var active = models.Current;
        var before = active.Predict("the fox and the").Forward.Logits.Data.ToArray();
        var weights = active.Transformer.Parameters().Select(p => p.Value.Data.ToArray()).ToList();

        var lab = NewLab(models);
        Assert.True(lab.CanContinueActive);   // the fables model and the fables text share their characters
        lab.ContinueActive = true;
        lab.StepsText = "5";
        lab.BatchText = "2";
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);

        Assert.Equal(5, lab.Reports.Count);
        Assert.Same(active, models.Current);
        Assert.Equal(before, active.Predict("the fox and the").Forward.Logits.Data);
        Assert.Equal(weights, active.Transformer.Parameters().Select(p => p.Value.Data.ToArray()).ToList());
    }

    [Fact]
    public void ContinueActive_OnlyWhenTheTextFitsTheModel()
    {
        var lab = NewLab();
        Assert.True(lab.CanContinueActive);
        lab.UseOwnText = true;
        lab.OwnText = "Ünïcödé text with letters the fables never use";
        Assert.False(lab.CanContinueActive);
        Assert.False(lab.ContinueActive);
        lab.ContinueActive = true;
        Assert.False(lab.ContinueActive);   // refused
        lab.OwnText = "the fox and the crow sat in a tree and talked";
        Assert.True(lab.CanContinueActive);
    }

    [Fact]
    public async Task ContinueActive_SavesTheTotalStepsAndKeepsTheModelsShape()
    {
        var models = _env.NewActiveModels();
        int shipped = models.Current.Info.StepsTrained;
        var lab = NewLab(models);
        lab.ContinueActive = true;
        lab.StepsText = "3";
        lab.BatchText = "2";
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = "Fables, more";
        await lab.UseInLabsCommand.ExecuteAsync(null).WaitAsync(Patience);
        Assert.Equal(shipped + 3, models.Current.Info.StepsTrained);
        Assert.Equal(64, models.Current.Transformer.Config.ModelWidth);
    }

    [Fact]
    public async Task Save_AsksBeforeReplacingAFile_EvenUnderAnotherName()
    {
        var lab = TinyLab();
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = "a/b";
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.Null(lab.OverwriteQuestion);
        Assert.Contains("Saved", lab.SaveStatus);

        lab.SaveName = "a_b";   // a different name, but the same file
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.Equal("A model with this file name already exists. Save over it?", lab.OverwriteQuestion);
        lab.CancelOverwriteCommand.Execute(null);
        Assert.Null(lab.OverwriteQuestion);
        Assert.Equal("a/b", _env.NewLibrary().List().Single(e => !e.IsShipped).Name);

        await lab.SaveCommand.ExecuteAsync(null);
        await lab.ConfirmOverwriteCommand.ExecuteAsync(null);
        Assert.Null(lab.OverwriteQuestion);
        Assert.Equal("a_b", _env.NewLibrary().List().Single(e => !e.IsShipped).Name);
    }

    [Fact]
    public async Task Save_RefusesShippedNamesAndBlankNames()
    {
        var lab = TinyLab();
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = " fables MODEL ";
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.Equal("“fables MODEL” is the name of a model that comes with the app, so it can't be replaced. Please choose another name.", lab.SaveStatus);
        lab.SaveName = "  ";
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Type a name for the model first.", lab.SaveStatus);
        Assert.DoesNotContain(_env.NewLibrary().List(), e => !e.IsShipped);
    }

    [Fact]
    public void BeforeAnyTraining_NothingCanBeSaved()
    {
        var lab = NewLab();
        Assert.False(lab.SaveCommand.CanExecute(null));
        Assert.False(lab.UseInLabsCommand.CanExecute(null));
        Assert.False(lab.PauseCommand.CanExecute(null));
        Assert.False(lab.ResumeCommand.CanExecute(null));
        Assert.False(lab.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task Reset_PutsEverythingBack()
    {
        var lab = TinyLab();
        lab.UseOwnText = true;
        lab.OwnText = "the cat sat on the mat and the dog sat on the log";
        lab.Seed = 5;
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.Reset();

        Assert.False(lab.UseOwnText);
        Assert.Equal("", lab.OwnText);
        Assert.Equal(32, lab.Width);
        Assert.Equal("600", lab.StepsText);
        Assert.Equal(1, lab.Seed);
        Assert.Empty(lab.Reports);
        Assert.Empty(lab.LossSeries);
        Assert.Empty(lab.Samples);
        Assert.Equal(0, lab.StepsDone);
        Assert.False(lab.SaveCommand.CanExecute(null));
        Assert.True(lab.StartCommand.CanExecute(null));
    }

    [Fact]
    public void Speed_IsWrittenPlainly()
    {
        Assert.Equal("about 15 steps per second; about 39 seconds left", TrainLabViewModel.Speed(15.4, 600));
        Assert.Equal("about 0.8 steps per second", TrainLabViewModel.Speed(0.84, 0));
        Assert.Equal("about 2 steps per second; about 8 minutes left", TrainLabViewModel.Speed(2, 1000));
        Assert.Equal("", TrainLabViewModel.Speed(0, 10));
    }

    [Fact]
    public void Factory_RegistersTheTrainLab()
    {
        var main = _env.NewMain();
        main.NavigateToLesson("train");
        Assert.IsType<TrainLabViewModel>(main.CurrentLab);
    }
}
