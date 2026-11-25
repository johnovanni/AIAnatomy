using System.ComponentModel;
using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One row of the comparison table: a network, how its numbers were obtained, and the numbers.</summary>
public sealed record ComparisonRow(string Network, string Kind, string Loss, string Speed, string Size)
{
    public string Description => $"{Network}, {Kind}: validation loss {Loss}, {Speed} characters a second, {Size}";
}

/// <summary>The mean loss of each network over a range of positions, named by how many characters had been read
/// ("5 to 8"); <see cref="From"/> and <see cref="To"/> are 0-based positions, To excluded.</summary>
public sealed record MemoryBand(string CharactersRead, int From, int To, double Transformer, double Memory)
{
    public string TransformerText => NumberText.Short(Transformer);
    public string MemoryText => NumberText.Short(Memory);
    public string Description => $"{CharactersRead} characters read: transformer {TransformerText}, memory network {MemoryText}";
}

/// <summary>Part of the hidden-state map: the same rows (characters) and a block of the memory's units as columns.</summary>
public sealed record HiddenStateBlock(string Title, double[,] Values, IReadOnlyList<string> RowLabels, IReadOnlyList<string> ColumnLabels)
{
    /// <summary>The title, which is what a list of blocks (the lab's picker) shows.</summary>
    public override string ToString() => Title;
}

/// <summary>
/// Lesson 4.8's lab: the shipped memory network (a character-level Elman RNN) beside the shipped Fables transformer.
/// Screens: "compare" (validation loss and training speed, each labelled recorded or live), "memory" (loss by position,
/// measured live on both networks when the lab opens), "hidden" (the memory vector after each character of the text),
/// "samples" (200 characters from each, same start, temperature 0.8, the seed) and "training" (train it again, the memory
/// network only). The transformer is the shipped "Fables model", loaded by name, never the active model, so the comparison
/// is always against the same network.
/// </summary>
public sealed partial class MemoryNetworkLabViewModel : LabViewModel
{
    public const string Id = "MemoryNetworkLab";
    public const string DefaultText = "The Fox";
    public const string EmptyMessage = "Type some text first.";
    public const string LoadingMessage = "Loading the two networks and measuring them…";
    public const string LoadFailedMessage = "The two networks could not be loaded because of an unexpected problem. Reinstalling the app may fix it.";
    public const int SampleLength = 200;
    public const double Temperature = 0.8;

    /// <summary>The hidden-state map shows the memory after at most this many characters (the last ones).</summary>
    public const int MaxHiddenRows = 32;

    /// <summary>The hidden-state map shows this many units at a time (the learner picks which), so each square stays big
    /// enough to point at and every row keeps its character label.</summary>
    public const int UnitsPerBlock = 32;

    public const string RecordedKind = "recorded when it was built";
    public const string LiveKind = "measured just now, on this computer";

    /// <summary>The text box takes at most this many characters, so the memory is worked out at once on every key.</summary>
    public const int MaxTextLength = 200;

    // Measured with the shipped networks when the lab was built; MemoryNetworkLabTests and CheckedQuestionTests recompute them.
    // "Paired": each window's mean loss on one network minus the same window's on the other, then the mean and its standard error.
    /// <summary>The 32 shared windows: transformer minus memory network, paired by window.</summary>
    public const double SharedLossGap = 0.033, SharedLossGapError = 0.012;
    /// <summary>All non-overlapping windows of the held-back text (<see cref="MemoryTraining.HeldBackWindows"/>).</summary>
    public const int HeldBackWindowCount = 292;
    /// <summary>The held-back windows: transformer minus memory network, paired by window.</summary>
    public const double HeldBackLossGap = 0.005, HeldBackLossGapError = 0.007;
    /// <summary>The held-back windows: mean loss with 9 to 16 characters read minus with 33 to 64, per window, for each network.</summary>
    public const double HeldBackTransformerGain = -0.006, HeldBackTransformerGainError = 0.037;
    public const double HeldBackMemoryGain = -0.008, HeldBackMemoryGainError = 0.042;

    public const string LossNote =
        "On validation loss the two shipped networks are about the same. On the 32 windows the memory network's loss is lower by " +
        "0.033 ± 0.012, but on all 292 windows of the held-back text the gap is only 0.005 ± 0.007 (each window compared on both " +
        "networks; mean ± standard error). Each network was trained once, from one seed, so this is no win either way.";

    public const string SpeedNote =
        "Both recorded speeds are wall-clock times on the PC that built the app, over the whole training loop with a validation " +
        "check every 25 steps. They are not a like-for-like count of the arithmetic: the transformer's trainer works on the 8 " +
        "windows of a batch side by side on separate processor cores, while the memory network's runs the whole batch in one pass. " +
        "This app also runs on the processor (CPU), so these numbers can't show the transformer's real advantage: on parallel " +
        "hardware such as graphics chips it trains on every position of a window at once, while a memory network must go one " +
        "character after another.";

    public const string LiveSpeedNote =
        "Your run is timed over its training steps only, with no validation checks along the way, so it is not timed the same way " +
        "as the recorded speeds. Its network is also smaller (hidden size 128), so each step does less arithmetic.";

    public const string MemoryNote =
        "Each point is the average loss over the same 32 windows of the fables (the ones both networks were checked on), so " +
        "single points wobble up and down by a few tenths. The averages over ranges, below, are steadier. A check on all 292 windows " +
        "of the held-back text, with the shipped networks, found no gain past about 8 characters: with 33 to 64 characters read the " +
        "loss was no lower than with 9 to 16 (differences of −0.006 ± 0.037 for the transformer and −0.008 ± 0.042 for the memory " +
        "network; mean ± standard error). So the drop of about 0.1 in the later ranges below is within the wobble of 32 windows.";

    private static readonly (string Name, int From, int To)[] Bands =
        [("1", 0, 1), ("2 to 4", 1, 4), ("5 to 8", 4, 8), ("9 to 16", 8, 16), ("17 to 32", 16, 32), ("33 to 64", 32, 64)];

    private readonly FamilyModels _family;
    private readonly IActiveModelService _models;
    private readonly Func<string> _fablesText;
    private readonly Task _ready;
    private Task _sampling = Task.CompletedTask;

    private LanguageModel? _fables;
    private CharTokenizer? _tokenizer;
    private CorpusBatchSource? _source;
    private ElmanRnn? _shipped, _current;
    private double[] _shippedCurve = [];
    private long _shippedParameters;

    private int _trainGeneration, _sampleGeneration;
    private IDisposable? _run;
    private ElmanRnn? _trainingRnn;
    private RnnTrainer? _trainingTrainer;
    private bool _quiet;
    private int _retrainSteps = MemoryTraining.RetrainSteps;

    /// <param name="fablesText">The fables text (the shared validation windows and the training text come from it).</param>
    public MemoryNetworkLabViewModel(FamilyModels family, IActiveModelService models, Func<string> fablesText)
    {
        _family = family ?? throw new ArgumentNullException(nameof(family));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _fablesText = fablesText ?? throw new ArgumentNullException(nameof(fablesText));
        Runner = new RetrainRunner(CreateTrainer);
        Runner.PropertyChanged += OnRunnerChanged;
        _ready = LoadAsync();
    }

    public override string LabId => Id;

    /// <summary>Loading and the first measurement, and any samples being written.</summary>
    public override Task WorkCompleted => Task.WhenAll(_ready, _sampling);

    /// <summary>Train it again: the memory network only, with <see cref="MemoryTraining.Retrain"/>.</summary>
    public RetrainRunner Runner { get; }

    /// <summary>Steps for train it again: 1 to <see cref="MemoryTraining.RetrainSteps"/> (a test lowers it).</summary>
    public int RetrainSteps
    {
        get => _retrainSteps;
        set => _retrainSteps = Math.Clamp(value, 1, MemoryTraining.RetrainSteps);
    }

    /// <summary>A test hook, called on the worker after each training step with the steps taken.</summary>
    public Action<int>? AfterTrainingStep { get; set; }

    /// <summary>A test hook, called on a worker after a completed run, before its network is measured.</summary>
    public Action? BeforeMeasuringTrained { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(TrainAgainCommand), nameof(GenerateSamplesCommand))]
    private bool isReady;

    /// <summary>True from Train it again until its result is shown (the run and then the measurement of its network).</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(TrainAgainCommand))]
    private bool isTraining;

    private bool CanTrain => IsReady && !IsTraining;

    /// <summary>While loading, what is happening; if loading failed, why. Null once ready.</summary>
    [ObservableProperty] private string? loadMessage = LoadingMessage;

    [ObservableProperty] private string text = DefaultText;

    /// <summary>"Type some text first." for an empty text; otherwise null.</summary>
    [ObservableProperty] private string? message;

    // ---- compare ----
    public double TransformerRecordedLoss => RecordedResults.FablesValidationLoss;
    public double TransformerRecordedSpeed => RecordedResults.FablesCharactersPerSecond;
    [ObservableProperty] private double rnnRecordedLoss;
    [ObservableProperty] private double rnnRecordedSpeed;
    [ObservableProperty] private double? rnnLiveLoss;
    [ObservableProperty] private double? rnnLiveSpeed;
    [ObservableProperty] private IReadOnlyList<ComparisonRow> comparison = [];

    // ---- memory ----
    [ObservableProperty] private double[] transformerCurve = [];
    [ObservableProperty] private double[] rnnCurve = [];
    [ObservableProperty] private IReadOnlyList<LineSeries> memorySeries = [];
    [ObservableProperty] private IReadOnlyList<MemoryBand> memoryBands = [];
    [ObservableProperty] private string memoryFinding = "";

    // ---- hidden ----
    /// <summary>The memory after each shown character: [characters, units]; null with no text.</summary>
    [ObservableProperty] private double[,]? hiddenState;
    [ObservableProperty] private IReadOnlyList<string> hiddenRowLabels = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedBlock))] private IReadOnlyList<HiddenStateBlock> hiddenBlocks = [];

    /// <summary>Which block of units the map shows (0 = the first); kept when the text changes, back to 0 on Reset.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedBlock))] private int selectedBlockIndex;

    /// <summary>The block of units on show, or null with no text.</summary>
    public HiddenStateBlock? SelectedBlock => HiddenBlocks.Count == 0 ? null : HiddenBlocks[Math.Clamp(SelectedBlockIndex, 0, HiddenBlocks.Count - 1)];
    [ObservableProperty] private string hiddenCaption = "";

    // ---- samples ----
    [ObservableProperty] private string transformerSample = "";
    [ObservableProperty] private string rnnSample = "";
    [ObservableProperty] private string sampleStatus = "";

    // ---- training ----
    /// <summary>True once a completed run's network has replaced the shipped one in the lab (Reset brings it back).</summary>
    [ObservableProperty] private bool usingTrainedNetwork;
    /// <summary>The run's progress line (the runner's), cleared when a run is abandoned.</summary>
    [ObservableProperty] private string trainingStatus = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasTrainingSeries))] private IReadOnlyList<LineSeries> trainingSeries = [];
    public bool HasTrainingSeries => TrainingSeries.Count > 0;
    /// <summary>What a completed run gave; empty before one.</summary>
    [ObservableProperty] private string trainingResult = "";

    /// <summary>The memory network the lab uses now (the shipped one, or the one trained again); for tests.</summary>
    internal ElmanRnn? CurrentNetwork => _current;

    /// <summary>Which memory network the hidden state, samples and curve use.</summary>
    public string MemoryNetworkName => UsingTrainedNetwork
        ? $"the memory network you trained (hidden size {MemoryTraining.RetrainHidden})"
        : $"the shipped memory network (hidden size {MemoryTraining.ShippedHidden})";

    partial void OnUsingTrainedNetworkChanged(bool value) => OnPropertyChanged(nameof(MemoryNetworkName));

    public string TrainingNote =>
        $"Train it again trains a new memory network from random starting numbers, with the seed: hidden size {MemoryTraining.RetrainHidden} " +
        $"for {N0(MemoryTraining.RetrainSteps)} steps, about one to one and a half minutes on the PC that built the app. The shipped one is " +
        $"larger and trained for longer (hidden size {MemoryTraining.ShippedHidden}, {N0(MemoryTraining.ShippedSteps)} steps), so expect a " +
        "somewhat higher loss. The transformer is not trained again here: its numbers are the ones recorded when it was built.";

    public bool HasLiveValues => RnnLiveLoss != null;
    partial void OnRnnLiveLossChanged(double? value) => OnPropertyChanged(nameof(HasLiveValues));

    partial void OnTextChanged(string value)
    {
        if (value != null && value.Length > MaxTextLength)
        {
            // Cut to the cap (never through a surrogate pair); setting it again runs this method once more.
            int keep = char.IsHighSurrogate(value[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength;
            Text = value[..keep];
            return;
        }
        if (_quiet) return;
        ClearSamples();
        RefreshHidden();
        ReportScreens();
    }

    protected override void OnSeedEdited()
    {
        if (_quiet) return;
        var run = DetachRun();   // a new seed abandons a run under way
        ClearSamples();
        ReportScreens();
        run?.Dispose();
    }

    public override void Reset()
    {
        var run = DetachRun();
        _quiet = true;
        try
        {
            Seed = 1;
            Text = DefaultText;
        }
        finally { _quiet = false; }
        ClearSamples();
        SelectedBlockIndex = 0;
        if (IsReady)
        {
            _current = _shipped;
            UsingTrainedNetwork = false;
            RnnLiveLoss = null;
            RnnLiveSpeed = null;
            TrainingResult = "";
            TrainingStatus = "";
            TrainingSeries = [];
            ShowCurves(TransformerCurve, _shippedCurve);
            UpdateComparison();
            RefreshHidden();
            ReportScreens();
        }
        run?.Dispose();
    }

    public override void Deactivate()
    {
        DetachRun()?.Dispose();
        _sampleGeneration++;
    }

    /// <summary>Ends a run early; nothing it trained is used.</summary>
    [RelayCommand]
    public void StopTraining() => Runner.Stop();

    /// <summary>Trains a fresh memory network (the Retrain size) with the seed. When it completes, its validation loss and
    /// speed are shown beside the recorded ones, and it replaces the shipped network in the lab. The screens light once, at the end.</summary>
    [RelayCommand(CanExecute = nameof(CanTrain), AllowConcurrentExecutions = true)]
    private async Task TrainAgainAsync()
    {
        if (!CanTrain || Runner.IsRunning) return;
        int generation = ++_trainGeneration;
        int seed = Seed;
        _run?.Dispose();   // never leave an older scope open: the screens would stop lighting
        _run = BeginRun();
        IsTraining = true;
        try
        {
            await Runner.StartAsync(seed);
            if (generation != _trainGeneration) return;   // Reset, a new seed or leaving the lab ended this run
            var trainer = _trainingTrainer;
            var rnn = _trainingRnn;
            if (Runner.Finished != null && trainer != null && rnn != null)
            {
                double seconds = Runner.Elapsed.TotalSeconds;
                var windows = _source!.ValidationWindows;
                var hook = BeforeMeasuringTrained;
                var (loss, curve) = await Task.Run(() =>
                {
                    hook?.Invoke();
                    return (trainer.ValidationLoss(), ContextLoss.LossByPosition(rnn, windows));
                });
                if (generation != _trainGeneration) return;
                _current = rnn;
                UsingTrainedNetwork = true;
                RnnLiveLoss = loss;
                RnnLiveSpeed = seconds > 0 ? trainer.StepsTaken * (double)RnnTrainer.DefaultBatchSize * MemoryTraining.ContextLength / seconds : null;
                TrainingResult = $"Trained again with seed {seed}, for {N0(trainer.StepsTaken)} steps in {Math.Max(1, (int)Math.Round(seconds))} s. Validation loss on the same 32 windows: " +
                    $"{NumberText.Short(loss)}. The lab now uses this network for the memory chart, the hidden state and the samples; Reset brings back the shipped one.";
                ShowCurves(TransformerCurve, curve);
                UpdateComparison();
                ClearSamples();
                RefreshHidden();
            }
            ReportScreens();
        }
        finally
        {
            if (generation == _trainGeneration)
            {
                IsTraining = false;
                var run = _run;
                _run = null;
                run?.Dispose();
            }
        }
    }

    /// <summary>Writes <see cref="SampleLength"/> characters with each network after the text, at temperature 0.8, with the seed.</summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    private Task GenerateSamples()
    {
        if (!IsReady) return Task.CompletedTask;
        if (string.IsNullOrEmpty(Text))
        {
            Message = EmptyMessage;
            return Task.CompletedTask;
        }
        int generation = ++_sampleGeneration;
        var fables = _fables!;
        var rnn = _current!;
        var tokenizer = _tokenizer!;
        string start = Text;
        int seed = Seed;
        TransformerSample = RnnSample = "";
        SampleStatus = $"Writing {SampleLength} characters with each network…";
        return _sampling = WriteAsync();

        async Task WriteAsync()
        {
            try
            {
                var (t, r) = await Task.Run(() => (
                    Sampling.Generate(fables, start, SampleLength, new SamplingSettings(Temperature), new SeededRandom(seed)),
                    RnnSampling.Generate(rnn, tokenizer, start, SampleLength, Temperature, new SeededRandom(seed))));
                if (generation != _sampleGeneration) return;
                TransformerSample = t;
                RnnSample = r;
                SampleStatus = $"{SampleLength} new characters from each network after your text, at temperature {NumberText.ShortWithDecimals(Temperature, 1)} " +
                    $"with seed {seed}. The same text and seed always give the same writing.";
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                if (generation != _sampleGeneration) return;
                ErrorLog.ReportBackground(e);
                SampleStatus = "The writing could not be done. Please try again.";
            }
            ReportSamples();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var memory = _family.MemoryAsync();
            var fables = Task.Run(LoadShippedFables);
            var text = Task.Run(() => _fablesText());
            var (rnn, tokenizer, info) = await memory;
            var transformer = await fables;
            string fablesText = await text;
            if (!tokenizer.Vocabulary.SequenceEqual(transformer.Tokenizer.Vocabulary))
                throw new InvalidDataException("The memory network and the Fables model know different characters, so they cannot be compared.");
            var (source, transformerCurve, rnnCurve) = await Task.Run(() =>
            {
                var s = MemoryTraining.FablesSource(transformer.Tokenizer, fablesText);
                return (s, ContextLoss.LossByPosition(transformer.Transformer, s.ValidationWindows), ContextLoss.LossByPosition(rnn, s.ValidationWindows));
            });

            _fables = transformer;
            _tokenizer = tokenizer;
            _source = source;
            _shipped = _current = rnn;
            _shippedCurve = rnnCurve;
            _shippedParameters = rnn.Config.CountParameters();
            RnnRecordedLoss = info.Results != null && info.Results.TryGetValue("validationLoss", out var recordedLoss) ? recordedLoss : double.NaN;
            RnnRecordedSpeed = info.Results != null && info.Results.TryGetValue("charactersPerSecond", out var recordedSpeed) ? recordedSpeed : double.NaN;
            ShowCurves(transformerCurve, rnnCurve);
            LoadMessage = null;
            IsReady = true;
            UpdateComparison();
            RefreshHidden();
            ReportScreens();   // the baselines: nothing lights for loading
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            LoadMessage = e.Message;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ErrorLog.ReportBackground(e);
            LoadMessage = "The fables text that comes with the app could not be read. Reinstalling the app puts it back.";
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Anything else must not leave "Loading…" on screen for ever.
            ErrorLog.ReportBackground(e);
            LoadMessage = LoadFailedMessage;
        }
    }

    private LanguageModel LoadShippedFables()
    {
        var entry = _models.Library.List().FirstOrDefault(e => e.IsShipped && e.IsReadable && e.Name == ActiveModelService.DefaultModelName)
            ?? throw new InvalidOperationException($"The \"{ActiveModelService.DefaultModelName}\" that comes with the app could not be found. Reinstalling the app puts it back.");
        return _models.Library.Load(entry);
    }

    private ISteppedTrainer CreateTrainer(int seed)
    {
        var rnn = new ElmanRnn(MemoryTraining.Retrain(_tokenizer!.VocabularySize), new SeededRandom(seed));
        var trainer = new RnnTrainer(rnn, _source!, RetrainSteps, seed);
        _trainingRnn = rnn;
        _trainingTrainer = trainer;
        return AfterTrainingStep is { } hook ? new HookedTrainer(trainer, hook) : trainer;
    }

    private void OnRunnerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RetrainRunner.StatusText)) TrainingStatus = Runner.StatusText;
        else if (e.PropertyName == nameof(RetrainRunner.LossSeries)) TrainingSeries = Runner.LossSeries;
        else if (e.PropertyName == nameof(RetrainRunner.IsRunning)) TrainAgainCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Ends the training generation: a run under way is abandoned (nothing it trained is used) and its progress
    /// cleared. Returns the run's screen scope, still open, for the caller to close once its own result is reported.</summary>
    private IDisposable? DetachRun()
    {
        _trainGeneration++;
        IsTraining = false;
        if (Runner.IsRunning)
        {
            Runner.Abandon();
            TrainingStatus = "";
            TrainingSeries = [];
        }
        var run = _run;
        _run = null;
        return run;
    }

    private void ClearSamples()
    {
        _sampleGeneration++;
        TransformerSample = RnnSample = SampleStatus = "";
    }

    private void RefreshHidden()
    {
        if (!IsReady) return;
        if (string.IsNullOrEmpty(Text))
        {
            Message = EmptyMessage;
            HiddenState = null;
            HiddenRowLabels = [];
            HiddenBlocks = [];
            HiddenCaption = "";
            return;
        }
        Message = null;
        var tokenizer = _tokenizer!;
        var rnn = _current!;
        var ids = tokenizer.Encode(Text);
        var memory = rnn.Forward(new[] { ids }).Hidden;
        int n = ids.Count, shown = Math.Min(n, MaxHiddenRows), first = n - shown, units = rnn.Config.Hidden;
        var state = new double[shown, units];
        for (int r = 0; r < shown; r++)
        {
            var data = memory[first + r].Data;
            for (int u = 0; u < units; u++) state[r, u] = data[u];
        }
        var labels = ids.Skip(first).Select(id => LabText.Visible(tokenizer.TokenText(id))).ToArray();
        var blocks = new List<HiddenStateBlock>();
        for (int start = 0; start < units; start += UnitsPerBlock)
        {
            int width = Math.Min(UnitsPerBlock, units - start);
            var values = new double[shown, width];
            for (int r = 0; r < shown; r++)
                for (int u = 0; u < width; u++) values[r, u] = state[r, start + u];
            var columns = Enumerable.Range(start + 1, width).Select(u => u.ToString(CultureInfo.InvariantCulture)).ToArray();
            blocks.Add(new HiddenStateBlock($"Units {start + 1} to {start + width}", values, labels, columns));
        }
        HiddenState = state;
        HiddenRowLabels = labels;
        HiddenBlocks = blocks;
        if (SelectedBlockIndex >= blocks.Count) SelectedBlockIndex = 0;   // a smaller trained network has fewer blocks
        string whose = UsingTrainedNetwork ? "The memory network you trained" : "The shipped memory network";
        HiddenCaption = n > shown
            ? $"{whose} read all {N0(n)} characters; the map shows its memory, {units} numbers between −1 and 1, after each of the last {shown}."
            : $"{whose}'s memory, {units} numbers between −1 and 1, after each character. Each row is what it carries on to the next character.";
    }

    private void ShowCurves(double[] transformer, double[] rnn)
    {
        TransformerCurve = transformer;
        RnnCurve = rnn;
        string memoryName = UsingTrainedNetwork ? "memory network you trained" : "memory network (shipped)";
        MemorySeries =
        [
            new LineSeries("transformer (shipped)", transformer.Select((y, t) => ((double)(t + 1), y)).ToArray()),
            new LineSeries(memoryName, rnn.Select((y, t) => ((double)(t + 1), y)).ToArray()),
        ];
        var bands = Bands.Where(b => b.To <= Math.Min(transformer.Length, rnn.Length))
            .Select(b => new MemoryBand(b.Name, b.From, b.To, transformer[b.From..b.To].Average(), rnn[b.From..b.To].Average())).ToList();
        MemoryBands = bands;
        MemoryFinding = bands.Count < 6 ? "" :
            $"With 1 character read, the loss is {bands[0].TransformerText} for the transformer and {bands[0].MemoryText} for the memory network. " +
            $"With 5 to 8 read it is {bands[2].TransformerText} and {bands[2].MemoryText}, and with 33 to 64 read, {bands[5].TransformerText} and {bands[5].MemoryText}.";
    }

    private void UpdateComparison()
    {
        var rows = new List<ComparisonRow>
        {
            new("Fables transformer (shipped)", RecordedKind, NumberText.Short(TransformerRecordedLoss), N0(TransformerRecordedSpeed),
                $"{N0(_fables!.Transformer.Config.CountParameters())} learned numbers"),
            new($"Memory network (shipped, hidden size {_shipped!.Config.Hidden})", RecordedKind, NumberText.Short(RnnRecordedLoss), N0(RnnRecordedSpeed),
                $"{N0(_shippedParameters)} learned numbers"),
        };
        if (RnnLiveLoss is { } loss && _current is { } trained && UsingTrainedNetwork)
            rows.Add(new($"Memory network you trained (hidden size {trained.Config.Hidden})", LiveKind, NumberText.Short(loss),
                RnnLiveSpeed is { } speed ? N0(speed) : "too quick to time", $"{N0(trained.Config.CountParameters())} learned numbers"));
        Comparison = rows;
    }

    private static string N0(double value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private void ReportScreens()
    {
        if (!IsReady) return;
        ReportScreen("compare", Comparison.Select(r => r.Description).ToArray());
        ReportScreen("memory", new object[] { MemoryFinding, MemorySeries.Select(s => s.Name).ToArray(), TransformerCurve, RnnCurve });
        ReportScreen("hidden", new object?[] { Message, HiddenCaption, MemoryNetworkName, HiddenRowLabels.ToArray() });
        ReportSamples();
        ReportScreen("training", new object[] { TrainingResult, TrainingStatus });
    }

    private void ReportSamples() => ReportScreen("samples", new object[] { SampleStatus, TransformerSample, RnnSample });

    /// <summary>Passes each step to the trainer, then tells the test hook how many steps have been taken.</summary>
    private sealed class HookedTrainer(ISteppedTrainer inner, Action<int> after) : ISteppedTrainer
    {
        public int TotalSteps => inner.TotalSteps;
        public int StepsTaken => inner.StepsTaken;

        public double Step()
        {
            double loss = inner.Step();
            after(inner.StepsTaken);
            return loss;
        }
    }
}
