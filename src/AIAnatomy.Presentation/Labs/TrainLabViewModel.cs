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

/// <summary>One part of the model and how many learnable numbers it has.</summary>
public sealed record ParameterRow(string Part, long Count, string CountText)
{
    public string Description => $"{Part}: {CountText}";
}

/// <summary>Text the model wrote after a number of training steps (newlines shown as ↵).</summary>
public sealed record TrainingSample(int Step, string Text)
{
    public string Title => $"After step {NumberText.Short(Step)}";
}

/// <summary>
/// Train your own tiny transformer: on the fables or on pasted text, from scratch or carrying on from a copy of the active
/// model (never the active model itself). Training runs in the background with a live loss chart, a 200-character sample every
/// 50 steps and the measured speed; it can be paused, resumed and stopped. The result can be saved to the library and used in
/// every other lab.
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
    public const int SampleEvery = 50;
    public const int SampleLength = 200;
    public const int MaxLayers = 4, MaxHeads = 8, MinContext = 16, MaxContext = 128, MaxSteps = 100_000, MaxBatch = 64;
    public const double MaxLearningRate = 100;
    private const int MaxChartPoints = 1000;

    public const int DefaultWidth = 32;
    public const string DefaultLayers = "2", DefaultHeads = "4", DefaultContext = "64", DefaultLearningRate = "0.003",
        DefaultSteps = "600", DefaultBatch = "8";

    public const string SampleNote =
        "Every 50 steps the model writes 200 characters, starting from a new line, with the same seed each time, so the " +
        "changes come from the training and not from luck. At first they are random characters; as the loss falls, letter patterns and short words appear.";

    public const string LossChartNote =
        "Training loss is measured on the pieces of text used for each step. Validation loss is measured on the end of the text, which is kept out of " +
        "training (for a very short text the two can overlap). Lower is better. If validation loss stops falling while training loss keeps falling, " +
        "the model is starting to memorise its text instead of learning patterns that carry over.";

    /// <summary>The order the parts are listed in (ruling R12), the same as the lessons describe them.</summary>
    public static IReadOnlyList<string> PartOrder { get; } =
        ["Token embeddings", "Positions", "Attention", "Feed-forward", "Layer norms", "Output layer"];

    public static IReadOnlyList<int> Widths { get; } = [16, 32, 48, 64, 96];

    private readonly IActiveModelService _models;
    private readonly Func<string> _fables;
    private readonly Action? _libraryChanged;
    private readonly Lazy<CharTokenizer> _fablesTokenizer;
    private readonly Lazy<HashSet<string>> _shippedNames;
    private readonly object _pendingGate = new();
    private readonly object _applyGate = new();
    private readonly List<(int Generation, Action Apply)> _pending = new();
    private readonly List<TrainingStepReport> _reports = new();
    private readonly List<(double X, double Y)> _trainPoints = new();
    private readonly List<(double X, double Y)> _validationPoints = new();
    private CharTokenizer? _ownTokenizer;
    private string? _ownTokenizerText;
    private TransformerConfig? _config;
    private (TransformerConfig Config, IReadOnlyDictionary<string, long> Counts)? _countsCache;
    private Run? _run;
    private CancellationTokenSource? _cts;
    private bool _pauseRequested;
    private Task _work = Task.CompletedTask;
    private int _generation;
    private bool _syncing;
    private double? _lastValidation;
    private (Run Run, int Step, string Name, ModelEntry Entry)? _saved;
    private (string Name, bool Activate)? _pendingSave;

    public TrainLabViewModel(IActiveModelService models, Func<string> fables, Action? libraryChanged = null)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _fables = fables ?? throw new ArgumentNullException(nameof(fables));
        _libraryChanged = libraryChanged;
        _fablesTokenizer = new Lazy<CharTokenizer>(() => CharTokenizer.FromText(_fables()));
        _shippedNames = new Lazy<HashSet<string>>(() =>
            _models.Library.List().Where(e => e.IsShipped).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
        Refresh();
    }

    public override string LabId => Id;
    public override Task WorkCompleted => _work;

    // ---- the text ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseFables))]
    private bool useOwnText;
    [ObservableProperty] private string ownText = "";
    [ObservableProperty] private string? textProblem;
    [ObservableProperty] private string textSummary = "";

    // ---- the model's shape and the training settings (typed text, read after every key) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FreshModel), nameof(CanEditShape))]
    private bool continueActive;
    [ObservableProperty] private bool canContinueActive;
    [ObservableProperty] private string continueNote = "";
    [ObservableProperty] private int width = DefaultWidth;
    [ObservableProperty] private string layersText = DefaultLayers;
    [ObservableProperty] private string headsText = DefaultHeads;
    [ObservableProperty] private string contextText = DefaultContext;
    [ObservableProperty] private string learningRateText = DefaultLearningRate;
    [ObservableProperty] private string stepsText = DefaultSteps;
    [ObservableProperty] private string batchText = DefaultBatch;
    [ObservableProperty] private string? settingsProblem;

    /// <summary>Why Start can't be pressed (shown in its place), or null.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? startProblem;

    [ObservableProperty] private IReadOnlyList<ParameterRow> parameterRows = [];
    [ObservableProperty] private string parameterTotalText = "";

    // ---- the run ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings), nameof(CanEditShape))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(PauseCommand), nameof(StopCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
    private bool isTraining;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings), nameof(CanEditShape))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand), nameof(StopCommand))]
    private bool isPaused;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand), nameof(StopCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
    private bool hasModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
    private bool hasDiverged;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(SaveCommand), nameof(UseInLabsCommand))]
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

    /// <summary>The shape boxes are used only for a new model.</summary>
    public bool CanEditShape => CanEditSettings && !ContinueActive;

    /// <summary>The other choice of <see cref="UseOwnText"/> (for a pair of radio buttons).</summary>
    public bool UseFables
    {
        get => !UseOwnText;
        set => UseOwnText = !value;
    }

    /// <summary>The other choice of <see cref="ContinueActive"/>: a new model built from the boxes (the default).</summary>
    public bool FreshModel
    {
        get => !ContinueActive;
        set => ContinueActive = !value;
    }

    /// <summary>Every report of the current run, in order.</summary>
    public IReadOnlyList<TrainingStepReport> Reports => _reports;

    partial void OnUseOwnTextChanged(bool value) => Refresh();
    partial void OnOwnTextChanged(string value) => Refresh();
    partial void OnContinueActiveChanged(bool value) => Refresh();
    partial void OnWidthChanged(int value) => Refresh();
    partial void OnLayersTextChanged(string value) => Refresh();
    partial void OnHeadsTextChanged(string value) => Refresh();
    partial void OnContextTextChanged(string value) => Refresh();
    partial void OnLearningRateTextChanged(string value) => Refresh();
    partial void OnStepsTextChanged(string value) => Refresh();
    partial void OnBatchTextChanged(string value) => Refresh();

    public override void OnActiveModelChanged() => Refresh();

    /// <summary>The shape a press of Start would train (the active model's own when carrying on), or null when a box is wrong.</summary>
    public TransformerConfig? CurrentConfig() => _config;

    public override void Reset()
    {
        Deactivate();
        _syncing = true;
        UseOwnText = false;
        OwnText = "";
        ContinueActive = false;
        Width = DefaultWidth;
        LayersText = DefaultLayers;
        HeadsText = DefaultHeads;
        ContextText = DefaultContext;
        LearningRateText = DefaultLearningRate;
        StepsText = DefaultSteps;
        BatchText = DefaultBatch;
        Seed = 1;
        SaveName = DefaultSaveName;
        _syncing = false;
        ClearRun();
        Refresh();
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

    private bool CanStart() => StartProblem == null && !IsTraining && !IsSaving;
    private bool CanPause() => IsTraining && HasModel;
    private bool CanResume() => IsPaused && !HasDiverged;
    private bool CanStop() => HasModel && (IsTraining || IsPaused);
    private bool CanSave() => HasModel && !HasDiverged && !IsTraining && !IsSaving;

    /// <summary>Builds a new model (or a copy of the active one) and trains it in the background for the number of steps asked.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (StartProblem != null || _config is not { } config) return;
        var active = _models.Current;
        var setup = new Setup(ContinueActive ? active : null, UseOwnText, UseOwnText ? OwnText : _fables(), config,
            ReadDouble(LearningRateText)!.Value, ReadInt(StepsText, 1, MaxSteps)!.Value, ReadInt(BatchText, 1, MaxBatch)!.Value, Seed);
        int generation = Interlocked.Increment(ref _generation);
        _cts?.Cancel();
        ClearRun();
        IsTraining = true;
        Status = "Getting the model ready…";
        Run run;
        try
        {
            run = await Task.Run(() => Prepare(setup));
        }
        catch (ArgumentException e)   // e.g. a text that is too short once its line endings are counted as the tokenizer counts them
        {
            if (IsCurrent(generation)) { IsTraining = false; Status = ""; Message = e.Message; }
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
        _run is { } run && IsPaused ? RunStepsAsync(run, Volatile.Read(ref _generation)) : Task.CompletedTask;

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

    /// <summary>Saves the model (unless it is already saved as it is now, under this name), then makes it the model every lab uses.</summary>
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

    private sealed record Setup(LanguageModel? Continue, bool OwnText, string Text, TransformerConfig Config, double LearningRate, int Steps, int Batch, int Seed);

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

    /// <summary>Off the UI thread: the tokenizer, the model (fresh, or a copy of the active one) and its trainer.</summary>
    private static Run Prepare(Setup s)
    {
        var tokenizer = s.Continue?.Tokenizer ?? CharTokenizer.FromText(s.Text);
        var model = s.Continue?.Transformer.Clone() ?? new Transformer(s.Config, new SeededRandom(s.Seed));
        var trainer = new Trainer(model, tokenizer.Encode(s.Text), new TrainingSettings(s.Batch, s.LearningRate, s.Seed));
        return new Run(trainer, model, tokenizer, s.OwnText ? OwnTextName : FablesTextName, s.Seed, s.Steps, s.Continue?.Info.StepsTrained ?? 0);
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
        else if (_pauseRequested && done < run.Target) { IsPaused = true; Status = $"Paused at step {NumberText.Short(done)} of {NumberText.Short(run.Target)}. Resume carries on from here."; }
        else if (done < run.Target) Status = StoppedStatus(run);
        else Status = $"Finished: {NumberText.Short(done)} steps. You can save the model and use it in the other labs.";
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
        if (double.IsFinite(report.TrainLoss) && (report.Step % every == 0 || report.Step == run.Target))
        {
            _trainPoints.Add((report.Step, report.TrainLoss));
            changed = true;
        }
        if (report.ValidationLoss is double v && double.IsFinite(v))
        {
            _validationPoints.Add((report.Step, v));
            _lastValidation = v;
            changed = true;
        }
        if (changed) LossSeries = [new LineSeries(TrainSeriesName, _trainPoints.ToList()), new LineSeries(ValidationSeriesName, _validationPoints.ToList())];

        StepsDone = report.Step;
        Progress = Math.Min(1, (double)report.Step / run.Target);
        ProgressText = $"Step {NumberText.Short(report.Step)} of {NumberText.Short(run.Target)}: training loss {NumberText.Short(report.TrainLoss)}"
                       + (_lastValidation is double last ? $", validation loss {NumberText.Short(last)}" : "") + ".";
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
        string left = seconds < 90 ? $"{Math.Max(1, Math.Ceiling(seconds)):0} seconds" : $"{Math.Round(seconds / 60):#,0} minutes";
        return $"{text}; about {left} left";
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
        catch (ArgumentException e) { SaveStatus = e.Message; return; }
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
            SaveStatus = $"Saved as “{entry.Name}”. Every lab now uses this model; you can switch back with the model picker.";
        }
        catch (InvalidOperationException e) { SaveStatus = e.Message; }
    }

    // ---- reading the boxes ----

    private void Refresh()
    {
        if (_syncing) return;
        var active = _models.Current;
        string text = UseOwnText ? OwnText : _fables();
        var vocabulary = UseOwnText ? OwnTokenizer() : _fablesTokenizer.Value;

        var known = active.Tokenizer.Vocabulary.ToHashSet();
        CanContinueActive = vocabulary.Vocabulary.All(known.Contains);
        if (ContinueActive && !CanContinueActive)
        {
            _syncing = true;
            ContinueActive = false;
            _syncing = false;
        }
        ContinueNote = CanContinueActive
            ? $"Carries on from a copy of “{active.Info.Name}” (width {active.Transformer.Config.ModelWidth}, {active.Transformer.Config.Layers} layers, " +
              $"{active.Transformer.Config.Heads} heads, context {active.Transformer.Config.ContextLength}). The active model itself is not changed."
            : $"Not possible with this text: it has characters that “{active.Info.Name}” doesn't know.";

        var problems = new List<string>();
        int? layers = ReadInt(LayersText, 1, MaxLayers);
        int? heads = ReadInt(HeadsText, 1, MaxHeads);
        int? context = ReadInt(ContextText, MinContext, MaxContext);
        if (!ContinueActive)
        {
            if (layers == null) problems.Add($"The number of layers must be a whole number from 1 to {MaxLayers}.");
            if (heads == null) problems.Add($"The number of heads must be a whole number from 1 to {MaxHeads}.");
            else if (Width % heads != 0) problems.Add($"The number of heads must divide the width evenly: {Width} can't be split into {heads} heads.");
            if (context == null) problems.Add($"The context length must be a whole number from {MinContext} to {MaxContext}.");
        }
        double? rate = ReadDouble(LearningRateText);
        if (!(rate > 0 && rate <= MaxLearningRate))
            problems.Add($"The learning rate must be a number above 0 and no more than {MaxLearningRate:0}, such as 0.003.");
        if (ReadInt(StepsText, 1, MaxSteps) == null) problems.Add($"The number of steps must be a whole number from 1 to {MaxSteps:#,0}.");
        if (ReadInt(BatchText, 1, MaxBatch) == null) problems.Add($"The batch size must be a whole number from 1 to {MaxBatch}.");
        SettingsProblem = problems.Count == 0 ? null : string.Join("\n", problems);

        _config = ContinueActive ? active.Transformer.Config
            : layers is int l && heads is int h && context is int c && Width % h == 0
                ? new TransformerConfig(vocabulary.VocabularySize, c, Width, l, h, 4 * Width)
                : null;

        TextProblem = UseOwnText && OwnText.Length == 0 ? LabText.EmptyPrompt
            : _config is { } config ? CorpusValidator.Problem(text, config.ContextLength)
            : null;
        TextSummary = UseOwnText && OwnText.Length == 0 ? ""
            : $"{NumberText.Short(text.Length)} characters, {NumberText.Short(vocabulary.VocabularySize)} different tokens (counting one for “unknown”).";
        var all = new[] { TextProblem, SettingsProblem }.Where(p => p != null).ToList();
        StartProblem = all.Count == 0 ? null : string.Join("\n", all);

        var counts = _config == null ? null : ContinueActive ? active.Transformer.ParameterCountsByPart() : CountsFor(_config);
        ParameterRows = counts == null ? [] : PartOrder.Select(p => new ParameterRow(p, counts[p], NumberText.Short(counts[p]))).ToList();
        ParameterTotalText = counts == null ? "The number of parameters appears when the shape is valid."
            : $"Total: {NumberText.Short(counts.Values.Sum())} learnable numbers (parameters).";
    }

    private CharTokenizer OwnTokenizer()
    {
        if (_ownTokenizer == null || !ReferenceEquals(_ownTokenizerText, OwnText))
        {
            _ownTokenizer = CharTokenizer.FromText(OwnText);
            _ownTokenizerText = OwnText;
        }
        return _ownTokenizer;
    }

    /// <summary>The counts of a model of this shape, read from a freshly built one (the lab does not redo the arithmetic).</summary>
    private IReadOnlyDictionary<string, long> CountsFor(TransformerConfig config)
    {
        if (_countsCache is { } cache && cache.Config == config) return cache.Counts;
        var counts = new Transformer(config, new SeededRandom(0)).ParameterCountsByPart();
        _countsCache = (config, counts);
        return counts;
    }

    private static int? ReadInt(string? text, int min, int max) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= min && v <= max ? v : null;

    private static double? ReadDouble(string? text) =>
        double.TryParse(text?.Trim().Replace('−', '-'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v) ? v : null;

    /// <summary>An <see cref="IProgress{T}"/> that runs its action on the calling (training) thread, between steps.</summary>
    private sealed class StepReporter(Action<TrainingStepReport> action) : IProgress<TrainingStepReport>
    {
        public void Report(TrainingStepReport value) => action(value);
    }
}
