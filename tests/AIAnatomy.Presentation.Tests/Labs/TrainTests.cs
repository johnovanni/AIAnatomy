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
        lab.Settings.Width = 16;
        lab.Settings.LayersText = "1";
        lab.Settings.HeadsText = "2";
        lab.Settings.ContextText = "16";
        lab.Settings.BatchText = "2";
        lab.Settings.StepsText = steps.ToString();
        Assert.Null(lab.Settings.StartProblem);
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
        Assert.False(lab.Settings.UseOwnText);
        Assert.False(lab.Settings.ContinueActive);
        Assert.Equal(32, lab.Settings.Width);
        Assert.Equal("2", lab.Settings.LayersText);
        Assert.Equal("4", lab.Settings.HeadsText);
        Assert.Equal("64", lab.Settings.ContextText);
        Assert.Equal("0.003", lab.Settings.LearningRateText);
        Assert.Equal("8", lab.Settings.BatchText);
        Assert.Equal("600", lab.Settings.StepsText);
        Assert.Equal(1, lab.Seed);
        Assert.Null(lab.Settings.StartProblem);
        Assert.True(lab.StartCommand.CanExecute(null));
    }

    [Fact]
    public void InvalidHeads_ShowsMessage()
    {
        var lab = NewLab();
        lab.Settings.HeadsText = "3";
        Assert.Equal("The number of heads must divide the width evenly: 32 can't be split into 3 heads.", lab.Settings.SettingsProblem);
        Assert.Equal(lab.Settings.SettingsProblem, lab.Settings.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));
        Assert.Empty(lab.Settings.ParameterRows);

        lab.Settings.HeadsText = "9";
        Assert.Equal("The number of heads must be a whole number from 1 to 8.", lab.Settings.SettingsProblem);
        lab.Settings.HeadsText = "8";
        Assert.Null(lab.Settings.SettingsProblem);
        Assert.True(lab.StartCommand.CanExecute(null));
    }

    [Fact]
    public void OtherBadSettings_ShowPlainMessages()
    {
        var lab = NewLab();
        lab.Settings.LayersText = "5";
        lab.Settings.ContextText = "abc";
        lab.Settings.LearningRateText = "0";
        lab.Settings.StepsText = "-1";
        lab.Settings.BatchText = "";
        Assert.Equal(string.Join("\n",
            "The number of layers must be a whole number from 1 to 4.",
            "The context length must be a whole number from 16 to 128.",
            "The learning rate must be a number above 0 and no more than 100, such as 0.003.",
            "The number of steps must be a whole number from 1 to 100,000.",
            "The batch size must be a whole number from 1 to 64."), lab.Settings.SettingsProblem);
        lab.Settings.LearningRateText = "3e-3";
        Assert.DoesNotContain("learning rate", lab.Settings.SettingsProblem);
    }

    [Fact]
    public void TooShortText_DisablesStart()
    {
        var lab = NewLab();
        lab.Settings.UseOwnText = true;
        Assert.Equal("Type some text to begin", lab.Settings.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));

        lab.Settings.OwnText = "short";
        Assert.Equal(CorpusValidator.Problem("short", 64), lab.Settings.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));

        lab.Settings.ContextText = "16";
        lab.Settings.OwnText = new string('a', 17);
        Assert.Equal(CorpusValidator.Problem(lab.Settings.OwnText, 16), lab.Settings.StartProblem);
        lab.Settings.OwnText = "the cat sat on the mat";
        Assert.Null(lab.Settings.StartProblem);
        Assert.True(lab.StartCommand.CanExecute(null));

        // The box gives "\r\n" for Enter, but the model reads one new line: 18 characters typed, 17 read, so it is too short.
        lab.Settings.OwnText = "abcdefghi\r\nabcdefg";
        Assert.Equal(CorpusValidator.ProblemForLength(17, 16), lab.Settings.StartProblem);
        Assert.Contains("this has 17", lab.Settings.StartProblem);
        Assert.StartsWith("17 characters", lab.Settings.TextSummary);
        Assert.False(lab.StartCommand.CanExecute(null));
        lab.Settings.OwnText = "abcdefghi\r\nabcdefgh";
        Assert.Null(lab.Settings.StartProblem);
    }

    [Fact]
    public async Task LargePastedText_IsReadInTheBackground_ThenChecked()
    {
        var lab = NewLab();
        lab.Settings.UseOwnText = true;
        lab.Settings.ContextText = "16";
        lab.Settings.OwnText = new string('a', CorpusValidator.MaxCharacters + 1);
        Assert.Equal(TrainSettingsPanelViewModel.ReadingMessage, lab.Settings.StartProblem);
        Assert.False(lab.StartCommand.CanExecute(null));
        await lab.WorkCompleted.WaitAsync(Patience);
        Assert.Equal(CorpusValidator.ProblemForLength(CorpusValidator.MaxCharacters + 1, 16), lab.Settings.StartProblem);

        lab.Settings.OwnText = string.Concat(Enumerable.Repeat("the fox and the crow. ", 3000));   // 66,000 characters
        await lab.WorkCompleted.WaitAsync(Patience);
        Assert.Null(lab.Settings.StartProblem);
        Assert.StartsWith("66,000 characters", lab.Settings.TextSummary);
    }

    [Fact]
    public void RefusedPaste_SaysWhy_AndTheNoteClearsWhenTheTextChanges()
    {
        var lab = NewLab();
        lab.Settings.UseOwnText = true;
        lab.Settings.RefusePaste(CorpusValidator.MaxCharacters + 5);
        Assert.Equal("That text was not pasted. " + CorpusValidator.ProblemForLength(CorpusValidator.MaxCharacters + 5, 16), lab.Settings.PasteNote);
        Assert.Contains("too long", lab.Settings.PasteNote);
        lab.Settings.OwnText = "x";
        Assert.Null(lab.Settings.PasteNote);
    }

    /// <summary>Ruling X: the paste note is live text, read only when its words change, so a second refusal in the same
    /// words is cleared first and is read again.</summary>
    [Fact]
    public void RefusedPasteTwice_ClearsTheNoteInBetween_SoItIsReadAgain()
    {
        var lab = NewLab();
        lab.Settings.UseOwnText = true;
        var seen = new List<string?>();
        lab.Settings.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(lab.Settings.PasteNote)) seen.Add(lab.Settings.PasteNote); };
        lab.Settings.RefusePaste(CorpusValidator.MaxCharacters + 5);
        lab.Settings.RefusePaste(CorpusValidator.MaxCharacters + 5);
        string note = "That text was not pasted. " + CorpusValidator.ProblemForLength(CorpusValidator.MaxCharacters + 5, 16);
        Assert.Equal(new[] { note, null, note }, seen);
    }

    [Fact]
    public void CarryingOn_DescribesTheActiveModelsOwnFeedForwardAndSeed()
    {
        var models = _env.NewActiveModels();
        var lab = NewLab(models);
        Assert.Contains("(128 numbers)", lab.Settings.FeedForwardNote);   // 4 x 32
        Assert.Contains("starting weights", lab.Settings.SeedHint);
        lab.Settings.ContinueActive = true;
        Assert.Equal($"The active model's feed-forward layers are {models.Current.Transformer.Config.FeedForwardWidth} numbers wide.", lab.Settings.FeedForwardNote);
        Assert.DoesNotContain("starting weights", lab.Settings.SeedHint);

        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));   // letters A to O, attention only
        lab.OnActiveModelChanged();
        lab.Settings.UseOwnText = true;
        lab.Settings.OwnText = "ABCDEFGHIJKLMNOABCDEFGHIJKLMNO";
        lab.Settings.ContinueActive = true;
        Assert.True(lab.Settings.ContinueActive);
        Assert.Equal("The active model has no feed-forward layers.", lab.Settings.FeedForwardNote);
    }

    [Fact]
    public void ParameterCount_IsByPartInTheFixedOrder_AndAddsUpToTheConfig()
    {
        var lab = NewLab();
        Assert.Equal(TrainSettingsPanelViewModel.PartOrder, lab.Settings.ParameterRows.Select(r => r.Part).ToList());
        Assert.Equal(lab.Settings.CurrentConfig()!.CountParameters(), lab.Settings.ParameterRows.Sum(r => r.Count));
        Assert.Contains("32,005", lab.Settings.ParameterTotalText);   // width 32, 2 layers, 4 heads, context 64 on the fables' 69 tokens

        long before = lab.Settings.ParameterRows.Sum(r => r.Count);
        lab.Settings.LayersText = "3";
        Assert.True(lab.Settings.ParameterRows.Sum(r => r.Count) > before);   // updates live
        Assert.Equal(lab.Settings.CurrentConfig()!.CountParameters(), lab.Settings.ParameterRows.Sum(r => r.Count));
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
        Assert.Contains("(measured at step 1)", lab.ProgressText);   // the validation loss shown says when it was measured
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
        Assert.False(lab.StartCommand.CanExecute(null));   // Start would throw the paused model away: Stop first
        int paused = lab.StepsDone;
        Assert.True(lab.ResumeCommand.CanExecute(null));
        Assert.True(lab.SaveCommand.CanExecute(null));   // a paused model can be saved

        var resumed = lab.ResumeCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone >= paused + 2);
        lab.StopCommand.Execute(null);
        await resumed.WaitAsync(Patience);

        Assert.False(lab.IsPaused);
        Assert.False(lab.ResumeCommand.CanExecute(null));
        Assert.True(lab.StartCommand.CanExecute(null));
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
        lab.Settings.LearningRateText = "50";
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);

        Assert.True(lab.HasDiverged);
        Assert.Equal(Trainer.DivergedMessage, lab.Message);
        Assert.False(lab.SaveCommand.CanExecute(null));
        Assert.False(lab.UseInLabsCommand.CanExecute(null));
        Assert.False(lab.ResumeCommand.CanExecute(null));
        Assert.True(lab.StepsDone < 200);
        var drawn = lab.LossSeries.Single(x => x.Name == TrainLabViewModel.TrainSeriesName).Points;
        Assert.All(drawn, p => Assert.True(p.X < lab.StepsDone));   // the runaway loss of the step that diverged is not drawn
    }

    [Fact]
    public async Task Resume_IsNotAllowedWhileSaving()
    {
        var lab = TinyLab(steps: 100_000);
        var run = lab.StartCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone >= 1);
        lab.PauseCommand.Execute(null);
        await run.WaitAsync(Patience);
        Assert.True(lab.ResumeCommand.CanExecute(null));

        lab.SaveName = "Paused model";
        var save = lab.SaveCommand.ExecuteAsync(null);
        Assert.True(lab.IsSaving);
        Assert.False(lab.ResumeCommand.CanExecute(null));   // the save is reading the weights that training would change
        await save.WaitAsync(Patience);
        Assert.False(lab.IsSaving);
        Assert.True(lab.ResumeCommand.CanExecute(null));
        lab.StopCommand.Execute(null);
    }

    [Fact]
    public async Task ChangingTheName_ClearsTheOverwriteQuestion()
    {
        var lab = TinyLab();
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = "twice";
        await lab.SaveCommand.ExecuteAsync(null);
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(lab.OverwriteQuestion);
        lab.SaveName = "something else";
        Assert.Null(lab.OverwriteQuestion);
        await lab.ConfirmOverwriteCommand.ExecuteAsync(null);   // nothing is waiting any more
        Assert.Single(_env.NewLibrary().List(), e => !e.IsShipped);
    }

    [Fact]
    public async Task Main_TrainLabSave_ShowsTheNewModelInThePicker()
    {
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        lab.Settings.Width = 16; lab.Settings.LayersText = "1"; lab.Settings.HeadsText = "1"; lab.Settings.ContextText = "16";
        lab.Settings.BatchText = "1"; lab.Settings.StepsText = "2";
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = "Picker test";
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.Contains(vm.ModelEntries, e => e.Name == "Picker test");
        Assert.True(vm.SelectedModelEntry!.IsShipped);   // saving alone does not change the active model

        await lab.UseInLabsCommand.ExecuteAsync(null);
        Assert.Equal("Picker test", vm.SelectedModelEntry!.Name);
    }

    [Fact]
    public async Task UseInLabs_ChangesActiveModel()
    {
        var models = _env.NewActiveModels();
        int refreshed = 0;
        var lab = new TrainLabViewModel(models, () => Fables.Value, () => refreshed++);
        lab.Settings.Width = 16; lab.Settings.LayersText = "1"; lab.Settings.HeadsText = "2"; lab.Settings.ContextText = "16"; lab.Settings.BatchText = "2"; lab.Settings.StepsText = "5";
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
        Assert.True(lab.Settings.CanContinueActive);   // the fables model and the fables text share their characters
        lab.Settings.ContinueActive = true;
        lab.Settings.StepsText = "5";
        lab.Settings.BatchText = "2";
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
        Assert.True(lab.Settings.CanContinueActive);
        lab.Settings.UseOwnText = true;
        lab.Settings.OwnText = "Ünïcödé text with letters the fables never use";
        Assert.False(lab.Settings.CanContinueActive);
        Assert.False(lab.Settings.ContinueActive);
        lab.Settings.ContinueActive = true;
        Assert.False(lab.Settings.ContinueActive);   // refused
        lab.Settings.OwnText = "the fox and the crow sat in a tree and talked";
        Assert.True(lab.Settings.CanContinueActive);
    }

    [Fact]
    public async Task ContinueActive_SavesTheTotalStepsAndKeepsTheModelsShape()
    {
        var models = _env.NewActiveModels();
        int shipped = models.Current.Info.StepsTrained;
        var lab = NewLab(models);
        lab.Settings.ContinueActive = true;
        lab.Settings.StepsText = "3";
        lab.Settings.BatchText = "2";
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
        lab.Settings.UseOwnText = true;
        lab.Settings.OwnText = "the cat sat on the mat and the dog sat on the log";
        lab.Seed = 5;
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.Reset();

        Assert.False(lab.Settings.UseOwnText);
        Assert.Equal("", lab.Settings.OwnText);
        Assert.Equal(32, lab.Settings.Width);
        Assert.Equal("600", lab.Settings.StepsText);
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
        Assert.Equal("about 60 steps per second; about 1 second left", TrainLabViewModel.Speed(60, 30));
    }

    private static void MakeTiny(TrainLabViewModel lab, int steps)
    {
        lab.Settings.Width = 16; lab.Settings.LayersText = "1"; lab.Settings.HeadsText = "2"; lab.Settings.ContextText = "16";
        lab.Settings.BatchText = "2"; lab.Settings.StepsText = steps.ToString();
        Assert.Null(lab.Settings.StartProblem);
    }

    [Fact]
    public async Task Main_LeavingTheTrainLab_KeepsTheRunGoing_AndShowsItInTheHeader()
    {
        // Final review I9 (ruling R35): one Train lab per window; leaving it does not stop the run, and the header says so.
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        MakeTiny(lab, 100_000);
        var run = lab.StartCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone > 0);
        Assert.Null(vm.BackgroundStatus);   // not while the Train lab is on show

        await vm.NavigateToLesson("attention");
        Assert.True(lab.IsTraining);
        await WaitUntil(() => vm.BackgroundStatus?.StartsWith("Training in the background: step ") == true);
        Assert.EndsWith(" of 100,000.", vm.BackgroundStatus);
        int before = lab.StepsDone;
        await WaitUntil(() => lab.StepsDone > before);   // it really carries on

        await vm.NavigateToLesson("train");
        Assert.Same(lab, vm.CurrentLab);   // the same lab, with its chart, samples and model
        Assert.True(lab.IsTraining);
        Assert.Null(vm.BackgroundStatus);

        vm.ResetLabCommand.Execute(null);   // Reset still stops it
        await run.WaitAsync(Patience);
        Assert.False(lab.IsTraining);
        Assert.Equal(0, lab.StepsDone);
        Assert.False(lab.HasModel);
    }

    [Fact]
    public async Task Main_ARunThatEndsWhileAway_IsReportedInTheHeader_UntilTheLabIsOpened()
    {
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        MakeTiny(lab, 20);
        var run = lab.StartCommand.ExecuteAsync(null);
        await vm.NavigateToLesson("tokens");
        await run.WaitAsync(Patience);
        Assert.Equal(20, lab.StepsDone);
        Assert.Equal("Training finished in the background: 20 steps. Open “Training a model” to save the model.", vm.BackgroundStatus);
        await vm.NavigateToLesson("train");
        Assert.Null(vm.BackgroundStatus);
        await vm.NavigateToLesson("tokens");
        Assert.Null(vm.BackgroundStatus);   // already seen
    }

    [Fact]
    public async Task Main_SourcesOrSettingsCoveringTheTrainLab_AlsoShowTheBackgroundLine()
    {
        // Residual batch 3a: the Train lab is still the current lab, but out of sight.
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        MakeTiny(lab, 100_000);
        var run = lab.StartCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone > 0);
        vm.ShowSourcesCommand.Execute(null);
        await WaitUntil(() => vm.BackgroundStatus?.StartsWith("Training in the background: step ") == true);
        vm.ShowSettingsCommand.Execute(null);
        Assert.StartsWith("Training in the background: step ", vm.BackgroundStatus);
        vm.BackToLessonCommand.Execute(null);
        Assert.Null(vm.BackgroundStatus);
        vm.ResetLabCommand.Execute(null);
        await run.WaitAsync(Patience);
    }

    [Fact]
    public async Task Main_ARunThatCannotStartWhileAway_SaysSoInTheHeader()
    {
        // Residual batch 3b. A failure in "Getting the model ready" cannot be caused from outside, so this plays the lab's
        // own sequence for it (StartAsync's catch): training on, then off, then the could-not-start message.
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        lab.IsTraining = true;
        await vm.NavigateToLesson("tokens");
        Assert.Equal("Training in the background: getting the model ready.", vm.BackgroundStatus);
        lab.IsTraining = false;
        lab.Status = "";
        lab.Message = TrainLabViewModel.CouldNotStartMessage;
        Assert.Equal("Training could not start. Open “Training a model” to see why.", vm.BackgroundStatus);
        await vm.NavigateToLesson("train");
        Assert.Null(vm.BackgroundStatus);
        await vm.NavigateToLesson("tokens");
        Assert.Null(vm.BackgroundStatus);
    }

    [Fact]
    public async Task Main_Shutdown_StopsABackgroundRun()
    {
        var vm = _env.NewMain();
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        MakeTiny(lab, 100_000);
        var run = lab.StartCommand.ExecuteAsync(null);
        await vm.NavigateToLesson("attention");
        vm.Shutdown();
        await run.WaitAsync(Patience);
        Assert.False(lab.IsTraining);
    }

    [Fact]
    public async Task Main_SavingOverTheActiveModel_ReloadsIt()
    {
        // Final review I10: "My model" is active; a newer run saved over its file must become what the labs use.
        var models = _env.NewActiveModels();
        var vm = _env.NewMain(models);
        await vm.NavigateToLesson("train");
        var lab = Assert.IsType<TrainLabViewModel>(vm.CurrentLab);
        MakeTiny(lab, 3);
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = "My model";
        await lab.UseInLabsCommand.ExecuteAsync(null).WaitAsync(Patience);
        Assert.Equal("My model", models.Current.Info.Name);
        var first = models.Current;

        lab.Seed = 2;   // a different run
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        await lab.SaveCommand.ExecuteAsync(null);
        Assert.Equal(TrainLabViewModel.OverwriteText, lab.OverwriteQuestion);
        await lab.ConfirmOverwriteCommand.ExecuteAsync(null).WaitAsync(Patience);

        Assert.NotSame(first, models.Current);
        Assert.Equal(2, models.Current.Info.Seed);
        var saved = models.Library.Load(models.Library.List().Single(e => e.Name == "My model"));
        Assert.Equal(saved.Transformer.Parameters().Select(p => p.Value.Data.ToArray()).ToList(),
            models.Current.Transformer.Parameters().Select(p => p.Value.Data.ToArray()).ToList());
        Assert.Equal("My model", vm.SelectedModelEntry!.Name);
        Assert.Contains("the labs now use this version", lab.SaveStatus);
    }

    [Fact]
    public async Task SavingOverAnotherModel_DoesNotChangeTheActiveOne()
    {
        var models = _env.NewActiveModels();
        var lab = TinyLab(models, steps: 2);
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.SaveName = "Other";
        await lab.SaveCommand.ExecuteAsync(null);
        await lab.SaveCommand.ExecuteAsync(null);
        await lab.ConfirmOverwriteCommand.ExecuteAsync(null).WaitAsync(Patience);
        Assert.Equal("Fables model", models.Current.Info.Name);
        Assert.DoesNotContain("the labs now use", lab.SaveStatus);
    }

    [Fact]
    public void Factory_RegistersTheTrainLab()
    {
        var main = _env.NewMain();
        main.NavigateToLesson("train");
        Assert.IsType<TrainLabViewModel>(main.CurrentLab);
    }

    // ---- screens (UI rework, Task 15) ----

    private static List<string> Listen(TrainLabViewModel lab, List<bool>? whileTraining = null)
    {
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) =>
        {
            lock (raised) { raised.Add(e.Screen); whileTraining?.Add(lab.IsTraining); }
        };
        return raised;
    }

    [Fact]
    public void TheParameterCountsLightAfterAnEditedShape_NotWhenTheSameValueIsSetAgain()
    {
        var lab = NewLab();
        var raised = Listen(lab);
        Assert.Empty(raised);   // opening is the baseline
        lab.Settings.Width = 48;
        Assert.Equal(["parameters"], raised);
        raised.Clear();
        lab.Settings.Width = 48;
        Assert.Empty(raised);
        lab.Settings.LayersText = "3";
        Assert.Equal(["parameters"], raised);
    }

    [Fact]
    public async Task ARun_LightsNothingWhileItGoes_AndEachChangedScreenOnceAtItsEnd()
    {
        var lab = TinyLab(steps: 60);
        var during = new List<bool>();
        var raised = Listen(lab, during);
        await lab.StartCommand.ExecuteAsync(null);
        await lab.WorkCompleted;
        lock (raised)
        {
            Assert.All(during, d => Assert.False(d));   // every light-up came after the run had ended
            Assert.Equal(1, raised.Count(s => s == "training"));
            Assert.Equal(1, raised.Count(s => s == "samples"));   // the writing at step 50
            Assert.Equal(raised.Count, raised.Distinct().Count());
        }
    }

    [Fact]
    public async Task Reset_DuringARun_LightsNothingLater_AndTheNextRunStillWorks()
    {
        var lab = TinyLab(steps: 100_000);
        var raised = Listen(lab);
        var run = lab.StartCommand.ExecuteAsync(null);
        await WaitUntil(() => lab.StepsDone > 0);
        lab.Reset();
        await run.WaitAsync(Patience);
        await lab.WorkCompleted;
        lock (raised) Assert.DoesNotContain("training", raised);   // the screens are back where the run found them

        lab.Settings.StepsText = "5";
        lab.Settings.Width = 16;
        lab.Settings.LayersText = "1";
        lab.Settings.HeadsText = "2";
        lab.Settings.ContextText = "16";
        lab.Settings.BatchText = "2";
        lock (raised) raised.Clear();
        await lab.StartCommand.ExecuteAsync(null);
        await lab.WorkCompleted;
        lock (raised) Assert.Equal(1, raised.Count(s => s == "training"));
    }

    [Fact]
    public async Task ResetAfterARun_LightsTheClearedScreens_AndAModelSwitchTheParameters()
    {
        var models = _env.NewActiveModels();
        var lab = TinyLab(models, steps: 60);
        await lab.StartCommand.ExecuteAsync(null);
        await lab.WorkCompleted;
        var raised = Listen(lab);
        lab.Reset();
        lock (raised)
        {
            Assert.Contains("training", raised);
            Assert.Contains("samples", raised);
            Assert.Contains("parameters", raised);
        }

        lab.Settings.ContinueActive = true;   // the parameters now come from the active model
        lock (raised) raised.Clear();
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        lock (raised) Assert.Contains("parameters", raised);
    }
}
