using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>Text the model wrote after a number of training steps (newlines shown as ↵).</summary>
public sealed record TrainingSample(int Step, string Text)
{
    public string Title => $"After step {NumberText.Short(Step)}";
}

/// <summary>
/// Train your own tiny transformer: on the fables or on pasted text, from scratch or carrying on from a copy of the active
/// model (never the active model itself). The form is <see cref="Settings"/>; this class runs the training in the background
/// with a live loss chart, a 200-character sample every 50 steps and the measured speed, with Pause, Resume and Stop. The result
/// can be saved to the library and used in every lab that uses a model.
/// </summary>
public sealed partial class TrainLabViewModel : LabViewModel
{
    public const string Id = "TrainLab";
    public const string FablesTextName = "Aesop's Fables (Townsend)";
    public const string OwnTextName = "Your own text";
    public const string TrainSeriesName = "Training loss";
    public const string ValidationSeriesName = "Validation loss";
    public const string DefaultSaveName = "My model";
    public const string OverwriteText = "A model with this file name already exists. Save over it?";
    public const string CouldNotStartMessage =
        "Training could not start: a model could not be built from this text and these settings. Please check them and try again.";
    public const int SampleEvery = 50;
    public const int SampleLength = 200;
    private const int MaxChartPoints = 1000;

    public const string SampleNote =
        "Every 50 steps the model writes 200 characters, starting from a new line, with the same seed each time, so the " +
        "changes come from the training and not from luck. At first they are random characters; as the loss falls, letter patterns and short words appear.";

    public const string LossChartNote =
        "Training loss is measured on the pieces of text used for each step. Validation loss is measured every 25 steps on the end of the text, which is kept out of " +
        "training (for a very short text the two can overlap). Lower is better. If validation loss stops falling while training loss keeps falling, " +
        "the model is starting to memorise its text instead of learning patterns that carry over.";

    private readonly IActiveModelService _models;
    private readonly Action? _libraryChanged;
    private readonly Lazy<HashSet<string>> _shippedNames;
    private readonly object _pendingGate = new();
    private readonly object _applyGate = new();
    private readonly List<(int Generation, Action Apply)> _pending = new();
    private readonly List<TrainingStepReport> _reports = new();
    private readonly List<(double X, double Y)> _trainPoints = new();
    private readonly List<(double X, double Y)> _validationPoints = new();
    private Run? _run;
    private CancellationTokenSource? _cts;
    private bool _pauseRequested;
    private Task _work = Task.CompletedTask;
    private int _generation;
    private (double Loss, int Step)? _lastValidation;
    private (Run Run, int Step, string Name, ModelEntry Entry)? _saved;
    private (string Name, bool Activate)? _pendingSave;

    public TrainLabViewModel(IActiveModelService models, Func<string> fables, Action? libraryChanged = null)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _libraryChanged = libraryChanged;
        _shippedNames = new Lazy<HashSet<string>>(() =>
            _models.Library.List().Where(e => e.IsShipped).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
        Settings = new TrainSettingsPanelViewModel(models, fables);
        Settings.PropertyChanged += OnSettingsChanged;
    }

    public override string LabId => Id;

    /// <summary>The training run (if any) and any background read of a large pasted text.</summary>
    public override Task WorkCompleted => Task.WhenAll(_work, Settings.Reading);

    /// <summary>The form: text, model shape and training settings.</summary>
    public TrainSettingsPanelViewModel Settings { get; }

    // ---- the run ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(PauseCommand), nameof(StopCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
    private bool isTraining;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(ResumeCommand), nameof(StopCommand))]
    private bool isPaused;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand), nameof(StopCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
    private bool hasModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
    private bool hasDiverged;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(ResumeCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
    private bool isSaving;

    [ObservableProperty] private int stepsDone;
    [ObservableProperty] private double progress;
    [ObservableProperty] private string progressText = "";
    [ObservableProperty] private string speedText = "";
    [ObservableProperty] private string status = "";
    /// <summary>Why training stopped early (the divergence message exactly as the trainer gives it), or null.</summary>
    [ObservableProperty] private string? message;
    [ObservableProperty] private IReadOnlyList<LineSeries> lossSeries = [];
    [ObservableProperty] private IReadOnlyList<TrainingSample> samples = [];

    // ---- saving ----
    [ObservableProperty] private string saveName = DefaultSaveName;
    [ObservableProperty] private string? saveStatus;
    [ObservableProperty] private string? overwriteQuestion;

    /// <summary>The settings can be changed while no run is going on or paused.</summary>
    public bool CanEditSettings => !IsTraining && !IsPaused;

    /// <summary>Every report of the current run, in order.</summary>
    public IReadOnlyList<TrainingStepReport> Reports => _reports;

    partial void OnIsTrainingChanged(bool value) => Settings.IsEditable = CanEditSettings;
    partial void OnIsPausedChanged(bool value) => Settings.IsEditable = CanEditSettings;

    /// <summary>A different name may mean a different file, so an open overwrite question no longer applies.</summary>
    partial void OnSaveNameChanged(string value)
    {
        _pendingSave = null;
        OverwriteQuestion = null;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrainSettingsPanelViewModel.StartProblem)) StartCommand.NotifyCanExecuteChanged();
    }

    public override void OnActiveModelChanged() => Settings.Refresh();

    public override void Reset()
    {
        Deactivate();
        Settings.Reset();
        Seed = 1;
        SaveName = DefaultSaveName;
        ClearRun();
    }

    /// <summary>Stops any training; what it reports afterwards is not shown. Leaving the lab calls this.</summary>
    public override void Deactivate()
    {
        Interlocked.Increment(ref _generation);
        _cts?.Cancel();
        _pendingSave = null;
        OverwriteQuestion = null;
        if (IsTraining || IsPaused) Status = "Training was stopped.";
        IsTraining = false;
        IsPaused = false;
    }

    // ---- commands ----

    // A paused model is never discarded by Start: the user stops it first.
    private bool CanStart() => Settings.StartProblem == null && !IsTraining && !IsPaused && !IsSaving;
    private bool CanPause() => IsTraining && HasModel;
    // Not while saving: the save reads the weights that training would change.
    private bool CanResume() => IsPaused && !HasDiverged && !IsSaving;
    private bool CanStop() => HasModel && (IsTraining || IsPaused);
    private bool CanSave() => HasModel && !HasDiverged && !IsTraining && !IsSaving;

    /// <summary>Builds a new model (or a copy of the active one) and trains it in the background for the number of steps asked.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!CanStart() || Settings.Read() is not { } choice) return;
        int seed = Seed;
        int generation = Interlocked.Increment(ref _generation);
        _cts?.Cancel();
        ClearRun();
        IsTraining = true;
        Status = "Getting the model ready…";
        Run run;
        try
        {
            run = await Task.Run(() => Prepare(choice, seed));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (IsCurrent(generation)) { IsTraining = false; Status = ""; Message = CouldNotStartMessage; }
            return;
        }
        if (!IsCurrent(generation)) return;
        _run = run;
        HasModel = true;
        await RunStepsAsync(run, generation);
    }

    /// <summary>Stops after the step in progress; the model, its optimizer and the step count are kept for Resume.</summary>
    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        _pauseRequested = true;
        _cts?.Cancel();
    }

    /// <summary>Carries on training the same model with the same optimizer, from the step where it paused.</summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task ResumeAsync() =>
        _run is { } run && CanResume() ? RunStepsAsync(run, Volatile.Read(ref _generation)) : Task.CompletedTask;

    /// <summary>Ends the run for good. The model as it is now can still be saved.</summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        if (IsTraining)
        {
            _pauseRequested = false;
            _cts?.Cancel();
        }
        else if (IsPaused && _run is { } run)
        {
            IsPaused = false;
            Status = StoppedStatus(run);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => SaveOrUseAsync(activate: false);

    /// <summary>Saves the model (unless it is already saved as it is now, under this name), then makes it the active model.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task UseInLabsAsync() => SaveOrUseAsync(activate: true);

    [RelayCommand]
    private Task ConfirmOverwriteAsync()
    {
        if (_pendingSave is not { } pending || !CanSave()) return Task.CompletedTask;
        _pendingSave = null;
        OverwriteQuestion = null;
        return SaveNowAsync(pending.Name, pending.Activate);
    }

    [RelayCommand]
    private void CancelOverwrite()
    {
        _pendingSave = null;
        OverwriteQuestion = null;
        SaveStatus = "Not saved.";
    }

    // ---- training ----

    private sealed class Run(Trainer trainer, Transformer model, CharTokenizer tokenizer, string textName, int seed, int target, int earlierSteps)
    {
        public Trainer Trainer { get; } = trainer;
        public Transformer Model { get; } = model;
        public CharTokenizer Tokenizer { get; } = tokenizer;
        public string TextName { get; } = textName;
        public int Seed { get; } = seed;
        public int Target { get; } = target;
        /// <summary>Steps the model had before this run (when carrying on from the active model).</summary>
        public int EarlierSteps { get; } = earlierSteps;
        public LanguageModel Sampler { get; } = new(model, tokenizer, new ModelInfo("Training", textName, 0, null, seed, false));
    }

    /// <summary>Off the UI thread: the model (fresh, or a copy of the active one) and its trainer.</summary>
    private static Run Prepare(TrainingChoice c, int seed)
    {
        var model = c.Continue?.Transformer.Clone() ?? new Transformer(c.Config, new SeededRandom(seed));
        var trainer = new Trainer(model, c.Tokenizer.Encode(c.Text), new TrainingSettings(c.Batch, c.LearningRate, seed));
        return new Run(trainer, model, c.Tokenizer, c.OwnText ? OwnTextName : FablesTextName, seed, c.Steps, c.Continue?.Info.StepsTrained ?? 0);
    }

    private async Task RunStepsAsync(Run run, int generation)
    {
        var cts = new CancellationTokenSource();
        _cts = cts;
        _pauseRequested = false;
        _pendingSave = null;
        OverwriteQuestion = null;
        Message = null;
        IsPaused = false;
        IsTraining = true;
        Status = $"Training: {NumberText.Short(run.Target)} steps.";

        var ui = SynchronizationContext.Current;
        int first = run.Trainer.StepsTaken;
        var clock = Stopwatch.StartNew();
        // The trainer reports between steps, on its own thread, while the weights are not changing, so the sample is written
        // right there and never reads a model that is being updated. Results go to the UI thread.
        var reporter = new StepReporter(report =>
        {
            string? sample = report.StoppedReason == null && report.Step % SampleEvery == 0
                ? Sampling.Generate(run.Sampler, "", SampleLength, new SamplingSettings(), new SeededRandom(run.Seed))
                : null;
            double rate = (report.Step - first) / Math.Max(clock.Elapsed.TotalSeconds, 1e-6);
            Enqueue(ui, generation, () => Apply(run, report, sample, rate));
        });

        try
        {
            var task = run.Trainer.RunAsync(run.Target - run.Trainer.StepsTaken, reporter, cts.Token);
            _work = task;
            await task;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (IsCurrent(generation)) Message = "Training stopped: " + e.Message;
        }
        finally
        {
            Flush();
            if (ReferenceEquals(_cts, cts)) _cts = null;
        }

        if (!IsCurrent(generation)) return;   // the lab was left or reset, or a new run started
        IsTraining = false;
        int done = run.Trainer.StepsTaken;
        if (run.Trainer.HasDiverged) { HasDiverged = true; Status = $"Stopped at step {NumberText.Short(done)}."; }
        else if (_pauseRequested && done < run.Target)
        {
            IsPaused = true;
            Status = $"Paused at step {NumberText.Short(done)} of {NumberText.Short(run.Target)}. Resume carries on from here; Stop ends this run, so a new one can start.";
        }
        else if (done < run.Target) Status = StoppedStatus(run);
        else Status = $"Finished: {NumberText.Short(done)} steps. You can save the model and use it in the labs.";
    }

    private static string StoppedStatus(Run run) =>
        $"Stopped at step {NumberText.Short(run.Trainer.StepsTaken)} of {NumberText.Short(run.Target)}. You can still save the model as it is now.";

    /// <summary>On the UI thread: one report into the chart, the progress line, the speed and (every 50 steps) the samples.</summary>
    private void Apply(Run run, TrainingStepReport report, string? sample, double rate)
    {
        if (!ReferenceEquals(run, _run)) return;
        _reports.Add(report);
        int every = Math.Max(1, (int)Math.Ceiling((double)run.Target / MaxChartPoints));
        bool changed = false;
        // The step that diverged is not drawn: its loss is the runaway value that stopped training.
        if (report.StoppedReason == null && double.IsFinite(report.TrainLoss) && (report.Step % every == 0 || report.Step == run.Target))
        {
            _trainPoints.Add((report.Step, report.TrainLoss));
            changed = true;
        }
        if (report.ValidationLoss is double v && double.IsFinite(v))
        {
            _validationPoints.Add((report.Step, v));
            _lastValidation = (v, report.Step);
            changed = true;
        }
        if (changed) LossSeries = [new LineSeries(TrainSeriesName, _trainPoints.ToList()), new LineSeries(ValidationSeriesName, _validationPoints.ToList())];

        StepsDone = report.Step;
        Progress = Math.Min(1, (double)report.Step / run.Target);
        ProgressText = $"Step {NumberText.Short(report.Step)} of {NumberText.Short(run.Target)}: training loss {NumberText.Short(report.TrainLoss)}"
                       + (_lastValidation is var (loss, at) ? $", validation loss {NumberText.Short(loss)} (measured at step {NumberText.Short(at)})" : "") + ".";
        SpeedText = Speed(rate, run.Target - report.Step);
        if (sample != null) Samples = [new TrainingSample(report.Step, LabText.ShowNewlines(sample)), .. Samples];
        if (report.StoppedReason != null)
        {
            Message = report.StoppedReason;
            HasDiverged = true;
        }
    }

    /// <summary>"about 15 steps per second; about 40 seconds left".</summary>
    public static string Speed(double stepsPerSecond, int stepsLeft)
    {
        if (!(stepsPerSecond > 0) || !double.IsFinite(stepsPerSecond)) return "";
        string rate = stepsPerSecond >= 2
            ? Math.Round(stepsPerSecond).ToString("#,0", CultureInfo.InvariantCulture)
            : stepsPerSecond.ToString("0.0", CultureInfo.InvariantCulture);
        string text = $"about {rate} steps per second";
        if (stepsLeft <= 0) return text;
        double seconds = stepsLeft / stepsPerSecond;
        string left = seconds < 90 ? Count(Math.Max(1, Math.Ceiling(seconds)), "second") : Count(Math.Round(seconds / 60), "minute");
        return $"{text}; about {left} left";

        static string Count(double n, string unit) => $"{n.ToString("#,0", CultureInfo.InvariantCulture)} {unit}{(n == 1 ? "" : "s")}";
    }

    private void Enqueue(SynchronizationContext? ui, int generation, Action apply)
    {
        lock (_pendingGate) _pending.Add((generation, apply));
        if (ui == null) Flush();
        else ui.Post(_ => Flush(), null);
    }

    /// <summary>Applies the waiting reports in order. Called from each post and once more when a run ends, so none is left behind.</summary>
    private void Flush()
    {
        lock (_applyGate)
        {
            (int Generation, Action Apply)[] items;
            lock (_pendingGate)
            {
                items = _pending.ToArray();
                _pending.Clear();
            }
            foreach (var (generation, apply) in items)
                if (IsCurrent(generation)) apply();
        }
    }

    private bool IsCurrent(int generation) => generation == Volatile.Read(ref _generation);

    private void ClearRun()
    {
        _run = null;
        _saved = null;
        _pendingSave = null;
        _lastValidation = null;
        _reports.Clear();
        _trainPoints.Clear();
        _validationPoints.Clear();
        LossSeries = [];
        Samples = [];
        StepsDone = 0;
        Progress = 0;
        ProgressText = SpeedText = Status = "";
        Message = SaveStatus = OverwriteQuestion = null;
        HasModel = HasDiverged = IsPaused = false;
    }

    // ---- saving ----

    private async Task SaveOrUseAsync(bool activate)
    {
        SaveStatus = null;
        OverwriteQuestion = null;
        _pendingSave = null;
        if (_run is not { } run || !CanSave()) return;
        string name = (SaveName ?? "").Trim();
        if (name.Length == 0) { SaveStatus = "Type a name for the model first."; return; }
        if (_shippedNames.Value.Contains(name))
        {
            SaveStatus = $"“{name}” is the name of a model that comes with the app, so it can't be replaced. Please choose another name.";
            return;
        }
        if (activate && _saved is { } saved && ReferenceEquals(saved.Run, run) && saved.Step == run.Trainer.StepsTaken && saved.Name == name)
        {
            Activate(saved.Entry);
            return;
        }
        string path;
        try { path = _models.Library.PathFor(name); }
        catch (ArgumentException) { SaveStatus = "Type a name for the model first."; return; }
        if (File.Exists(path))
        {
            _pendingSave = (name, activate);
            OverwriteQuestion = OverwriteText;
            return;
        }
        await SaveNowAsync(name, activate);
    }

    private async Task SaveNowAsync(string name, bool activate)
    {
        if (_run is not { } run) return;
        int generation = Volatile.Read(ref _generation);
        IsSaving = true;
        SaveStatus = $"Saving “{name}”…";
        try
        {
            try { await _work; }
            catch (Exception e) when (e is not OutOfMemoryException) { /* already reported; all that matters is that the trainer has stopped */ }
            int steps = run.Trainer.StepsTaken;
            var info = new ModelInfo(name, run.TextName, run.EarlierSteps + steps, run.Trainer.LatestValidationLoss, run.Seed, false);
            var model = new LanguageModel(run.Model, run.Tokenizer, info);
            var entry = await Task.Run(() => _models.Library.Save(model, name));
            _saved = (run, steps, name, entry);
            SaveStatus = $"Saved as “{name}” in your model library.";
            _libraryChanged?.Invoke();
            // Back on the UI thread: the active model's Changed handlers change UI collections.
            if (activate && IsCurrent(generation)) Activate(entry);
        }
        catch (InvalidOperationException e) { SaveStatus = e.Message; }   // weights that are not finite
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SaveStatus = "The model couldn't be saved: " + e.Message;
        }
        finally { IsSaving = false; }
    }

    private void Activate(ModelEntry entry)
    {
        try
        {
            _models.SetActive(entry);
            SaveStatus = $"Saved as “{entry.Name}”. Every lab that uses a model now uses this one; you can switch back with the model picker.";
        }
        catch (InvalidOperationException e) { SaveStatus = e.Message; }
    }

    /// <summary>An <see cref="IProgress{T}"/> that runs its action on the calling (training) thread, between steps.</summary>
    private sealed class StepReporter(Action<TrainingStepReport> action) : IProgress<TrainingStepReport>
    {
        public void Report(TrainingStepReport value) => action(value);
    }
}
