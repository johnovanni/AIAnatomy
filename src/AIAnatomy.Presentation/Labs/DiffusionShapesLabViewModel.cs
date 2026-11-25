using System.ComponentModel;
using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Training;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// Lesson 4.9's lab: a cloud of <see cref="SampleCount"/> random points becomes a shape step by step. The cloud is
/// <see cref="DiffusionSampler.Sample"/> with the seed: frame 0 is pure noise and each of the 100 steps takes some of the
/// noise out, so <see cref="Frames"/> has 101 frames and <see cref="Step"/> picks one. The network is the shipped one for
/// a preset shape (Spiral, Circle, Two moons, Letter S, each loaded through <see cref="FamilyModels.DiffusionAsync"/>), or
/// one trained here by Train it again (<see cref="DiffusionTraining.Retrain"/>, <see cref="DiffusionTraining.RetrainSteps"/>
/// steps, the seed), on the preset or on a drawn shape. A drawn shape is the most recent line on the pad only
/// (<see cref="Shapes.FromStroke"/> takes one line); a line it refuses shows why and the previous shape stays. A drawn
/// shape has no network, so no cloud, until it is trained. Sampling and the distance measure run off the UI thread.
/// <para>Play advances one step every <see cref="PlayInterval"/> to the last step and stops (an <see cref="IPlayClock"/>
/// drives it, so a test ticks by hand); Pause, or moving the step by hand, stops it. Nothing moves by itself.</para>
/// <para>The step line is a live region only while Play is not running (<see cref="IsStepLineLive"/>), so a screen reader
/// is not given a new line every 50 ms; when Play reaches the end it stops before the line changes to the last step, so
/// that step is announced, and a step moved by hand is announced as usual. Screens: "cloud" (the shape, the network, the
/// quality line and the finished cloud: not the step on show, so Play lights nothing) and "training".</para>
/// </summary>
public sealed partial class DiffusionShapesLabViewModel : LabViewModel
{
    public const string Id = "DiffusionShapesLab";
    public const int SampleCount = 500;
    public static readonly TimeSpan PlayInterval = TimeSpan.FromMilliseconds(50);

    public const string DefaultPreset = "Spiral";
    public const string DrawnShapeName = "Your drawing";

    public const string LoadingMessage = "Loading the shipped network…";
    public const string LoadFailedMessage = "The diffusion network could not be loaded because of an unexpected problem. Reinstalling the app may fix it.";
    public const string SamplingMessage = "Working out the 100 steps for 500 points…";
    public const string AcceptedMessage = "Your line is the shape now. No network has learned it yet, so press Train it again, below, to teach one.";
    public const string NeedsTrainingMessage =
        "No cloud yet: no network has learned your drawing. Press Train it again, below, to teach one; the rings show your shape.";
    public const string SampleFailedMessage = "The cloud could not be worked out. Please try again.";

    /// <summary>The preset shapes, each with a shipped network: "Spiral", "Circle", "Two moons", "Letter S".</summary>
    public static IReadOnlyList<string> PresetNames { get; } = Shapes.Presets.Select(p => p.Name).ToArray();

    private readonly FamilyModels _family;
    private readonly IPlayClock _clock;
    private Task _work = Task.CompletedTask;

    private DiffusionNet? _net;
    private NoiseSchedule? _schedule;
    private int _shippedSteps, _trainedSteps, _trainedSeed;
    private int _step;
    private IDisposable? _playing;

    private int _sampleGeneration, _trainGeneration;
    private IDisposable? _run;
    private DiffusionNet? _trainingNet;
    private NoiseSchedule? _trainingSchedule;
    private IReadOnlyList<(double X, double Y)> _trainingShape = [];
    private bool _quiet;
    private int _retrainSteps = DiffusionTraining.RetrainSteps;

    public DiffusionShapesLabViewModel(FamilyModels family) : this(family, new TimerPlayClock()) { }

    /// <param name="clock">What drives Play (a test passes one it ticks by hand).</param>
    public DiffusionShapesLabViewModel(FamilyModels family, IPlayClock clock)
    {
        _family = family ?? throw new ArgumentNullException(nameof(family));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Runner = new RetrainRunner(CreateTrainer);
        Runner.PropertyChanged += OnRunnerChanged;
        _work = ShowPresetAsync(DefaultPreset);
    }

    public override string LabId => Id;

    /// <summary>Loading a network and working out its cloud (the latest such work).</summary>
    public override Task WorkCompleted => _work;

    /// <summary>Train it again, with <see cref="DiffusionTraining.Retrain"/>.</summary>
    public RetrainRunner Runner { get; }

    /// <summary>Steps for train it again: 1 to <see cref="DiffusionTraining.RetrainSteps"/> (a test lowers it).</summary>
    public int RetrainSteps
    {
        get => _retrainSteps;
        set => _retrainSteps = Math.Clamp(value, 1, DiffusionTraining.RetrainSteps);
    }

    /// <summary>A test hook, called on the worker after each training step with the steps taken.</summary>
    public Action<int>? AfterTrainingStep { get; set; }

    /// <summary>A test hook, called on a worker after a completed run, before its cloud is worked out and measured.</summary>
    public Action? BeforeMeasuringTrained { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(TrainAgainCommand))]
    private bool isReady;

    /// <summary>True from Train it again until its result is shown (the run, then its cloud and its measurement).</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(TrainAgainCommand))]
    private bool isTraining;

    private bool CanTrain => IsReady && !IsTraining && TargetPoints.Count > 0;

    /// <summary>While loading, what is happening; if loading failed, why. Null once loaded.</summary>
    [ObservableProperty] private string? loadMessage = LoadingMessage;

    // ---- the shape ----

    /// <summary>The preset's name, or <see cref="DrawnShapeName"/>.</summary>
    [ObservableProperty] private string shapeName = DefaultPreset;

    [ObservableProperty] private bool isDrawnShape;

    /// <summary>The shape being made, 1,000 points in [-1, 1] (y up); drawn faintly behind the cloud.</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(TrainAgainCommand))]
    private IReadOnlyList<(double X, double Y)> targetPoints = [];

    /// <summary>A refused line's reason, or <see cref="AcceptedMessage"/> for an accepted one; empty otherwise.</summary>
    [ObservableProperty] private string drawingMessage = "";

    // ---- the cloud ----

    /// <summary>The sampler's frames: 0 is pure noise, the last is the finished cloud; empty while there is no cloud.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MaxStep), nameof(HasCloud)), NotifyCanExecuteChangedFor(nameof(TogglePlayCommand))]
    private IReadOnlyList<double[,]> frames = [];

    public bool HasCloud => Frames.Count > 0;

    /// <summary>The last step (100), or 0 with no cloud.</summary>
    public int MaxStep => Math.Max(0, Frames.Count - 1);

    /// <summary>The step on show, 0 to <see cref="MaxStep"/> (clamped). Setting it by hand stops Play.</summary>
    public int Step
    {
        get => _step;
        set => SetStep(value, byHand: true);
    }

    /// <summary>The points of the step on show.</summary>
    [ObservableProperty] private IReadOnlyList<(double X, double Y)> currentPoints = [];

    /// <summary>"Step 37 of 100: …", or empty with no cloud.</summary>
    [ObservableProperty] private string stepText = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsStepLineLive), nameof(PlayButtonText))]
    private bool isPlaying;

    /// <summary>Whether the step line is a live region: not while Play runs.</summary>
    public bool IsStepLineLive => !IsPlaying;

    public string PlayButtonText => IsPlaying ? "Pause" : "Play";

    /// <summary>"Average distance from the shape after all 100 steps: 0.031 (lower is better).", measured on the cloud
    /// shown; empty with no cloud.</summary>
    [ObservableProperty] private string qualityText = "";

    /// <summary>Which network makes the cloud, and for a shipped one its recorded result.</summary>
    [ObservableProperty] private string networkText = "";

    /// <summary>Why there is no cloud (working it out, or a drawing not yet trained); null when there is one.</summary>
    [ObservableProperty] private string? cloudNote = SamplingMessage;

    /// <summary>The shipped network's recorded average distance (500 points, seed 1); NaN for a drawing.</summary>
    [ObservableProperty] private double recordedDistance = double.NaN;

    // ---- training ----

    /// <summary>True once a completed run's network makes the cloud (Reset or choosing a preset ends it).</summary>
    [ObservableProperty] private bool usingTrainedNetwork;

    /// <summary>The run's progress line (the runner's), cleared when a run is abandoned.</summary>
    [ObservableProperty] private string trainingStatus = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasTrainingSeries))] private IReadOnlyList<LineSeries> trainingSeries = [];

    public bool HasTrainingSeries => TrainingSeries.Count > 0;

    /// <summary>What a completed run gave; empty before one.</summary>
    [ObservableProperty] private string trainingResult = "";

    /// <summary>The network making the cloud now; for tests.</summary>
    internal DiffusionNet? CurrentNetwork => _net;

    public string TrainingNote =>
        $"Train it again trains a new network of the same size from random starting numbers, with the seed, on the shape shown, for {N0(DiffusionTraining.RetrainSteps)} steps " +
        $"(66 to 84 seconds on the PC that built the app). The shipped networks were trained for {N0(DiffusionTraining.ShippedSteps)} steps, three times as long, " +
        "so the cloud you get is usually less tight. In a check with seed 1, the spiral's points ended 0.069 from the shape on average after " +
        $"{N0(DiffusionTraining.RetrainSteps)} steps, against 0.031 for the shipped network.";

    /// <summary>"Step 37 of 100: the network has taken noise out 37 times; 63 steps to go."</summary>
    public static string StepTextFor(int step, int steps)
    {
        if (step <= 0) return $"Step 0 of {steps}: {SampleCount} points of pure random noise. No noise has been taken out yet.";
        if (step >= steps) return $"Step {steps} of {steps}: the finished cloud, after taking noise out {steps} times.";
        string times = step == 1 ? "once" : $"{step} times";
        int left = steps - step;
        string toGo = left == 1 ? "1 step to go" : $"{left} steps to go";
        return $"Step {step} of {steps}: the network has taken noise out {times}; {toGo}.";
    }

    /// <summary>"Average distance from the shape after all 100 steps: 0.031 (lower is better)."</summary>
    public static string QualityFor(double distance, int steps) =>
        $"Average distance from the shape after all {steps} steps: {distance.ToString("0.000", CultureInfo.InvariantCulture)} (lower is better).";

    /// <summary>Shows a preset with its shipped network, and works out its cloud with the seed (off the UI thread). The chips
    /// stay enabled while a cloud is worked out: a later choice wins (the generation counter drops the earlier one).</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task ChoosePresetAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!PresetNames.Contains(name)) throw new ArgumentException($"There is no preset shape called '{name}'.", nameof(name));
        return _work = ShowPresetAsync(name);
    }

    /// <summary>Uses the most recent line on the pad (pad units: 0..1 each way, y down) as the shape. An empty pad changes
    /// nothing; a line <see cref="Shapes.FromStroke"/> refuses shows its reason and the shape stays as it was.</summary>
    public void UseDrawing(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        if (!IsReady || strokes.Count == 0 || strokes[^1] is not { } line) return;
        var result = Shapes.FromStroke(line, 1);
        if (result.Points is null)
        {
            DrawingMessage = result.Error ?? Shapes.TooShortMessage;
            return;
        }
        var run = DetachRun();   // a new shape abandons a run under way
        StopPlaying();
        _sampleGeneration++;     // and any cloud still being worked out
        _net = null;
        _schedule = null;
        IsDrawnShape = true;
        ShapeName = DrawnShapeName;
        TargetPoints = result.Points;
        UsingTrainedNetwork = false;
        RecordedDistance = double.NaN;
        NetworkText = "";
        ClearTraining();
        ClearCloud(NeedsTrainingMessage);
        DrawingMessage = AcceptedMessage;
        ReportScreens();
        run?.Dispose();
    }

    public void Play()
    {
        if (IsPlaying || !HasCloud) return;
        if (_step >= MaxStep) SetStep(0, byHand: false);
        IsPlaying = true;
        _playing = _clock.Start(PlayInterval, OnTick);
    }

    public void Pause() => StopPlaying();

    [RelayCommand(CanExecute = nameof(HasCloud))]
    private void TogglePlay()
    {
        if (IsPlaying) Pause();
        else Play();
    }

    public override void Reset()
    {
        var run = DetachRun();
        StopPlaying();
        _quiet = true;
        try { Seed = 1; }
        finally { _quiet = false; }
        DrawingMessage = "";
        _work = ShowPresetAsync(DefaultPreset);
        run?.Dispose();
    }

    public override void Deactivate()
    {
        DetachRun()?.Dispose();
        StopPlaying();
        _sampleGeneration++;
    }

    protected override void OnSeedEdited()
    {
        if (_quiet) return;
        var run = DetachRun();   // a new seed abandons a run under way
        StopPlaying();
        if (_net is { } net && _schedule is { } schedule)
        {
            int generation = ++_sampleGeneration;
            ClearCloud(SamplingMessage);
            _work = SampleAsync(net, schedule, TargetPoints, Seed, generation);
        }
        run?.Dispose();
    }

    /// <summary>Ends a run early; nothing it trained is used.</summary>
    [RelayCommand]
    public void StopTraining() => Runner.Stop();

    /// <summary>Trains a fresh network (the Retrain config) on the shape shown, with the seed. When it completes, its
    /// cloud is worked out and measured with the seed, and it makes the cloud until Reset or another shape. The screens
    /// light once, at the end.</summary>
    [RelayCommand(CanExecute = nameof(CanTrain), AllowConcurrentExecutions = true)]
    private async Task TrainAgainAsync()
    {
        if (!CanTrain || Runner.IsRunning) return;
        StopPlaying();
        int generation = ++_trainGeneration;
        int seed = Seed;
        var shape = TargetPoints;
        _trainingShape = shape;
        _run?.Dispose();   // never leave an older scope open: the screens would stop lighting
        _run = BeginRun();
        IsTraining = true;
        try
        {
            await Runner.StartAsync(seed);
            if (generation != _trainGeneration) return;   // Reset, a new seed or shape, or leaving the lab ended this run
            var net = _trainingNet;
            var schedule = _trainingSchedule;
            if (Runner.Finished is { } trainer && net != null && schedule != null)
            {
                double seconds = Runner.Elapsed.TotalSeconds;
                var hook = BeforeMeasuringTrained;
                var (frames, distance) = await Task.Run(() =>
                {
                    hook?.Invoke();
                    return SampleAndMeasure(net, schedule, shape, seed);
                });
                if (generation != _trainGeneration) return;
                _sampleGeneration++;   // a cloud still being worked out for the old network is not shown
                StopPlaying();
                _net = net;
                _schedule = schedule;
                _trainedSteps = trainer.StepsTaken;
                _trainedSeed = seed;
                UsingTrainedNetwork = true;
                NetworkText = TrainedNetworkText();
                TrainingResult = $"Trained again with seed {seed}, for {N0(trainer.StepsTaken)} steps in {Math.Max(1, (int)Math.Round(seconds))} s. " +
                    (IsDrawnShape
                        ? "The cloud above is now made by this network."
                        : "The cloud above is now made by this network; Reset, or choosing the shape again, brings back the shipped one.");
                ShowCloud(frames, distance);
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

    /// <summary>A finished run's result, line and chart belong to the shape it trained on, so a new shape clears them.</summary>
    private void ClearTraining()
    {
        TrainingResult = "";
        TrainingStatus = "";
        TrainingSeries = [];
    }

    private void OnTick()
    {
        if (!IsPlaying) return;
        int next = _step + 1;
        if (next >= MaxStep)
        {
            StopPlaying();   // first, so the step line is live again when it says the last step
            SetStep(MaxStep, byHand: false);
        }
        else SetStep(next, byHand: false);
    }

    private void StopPlaying()
    {
        var playing = _playing;
        _playing = null;
        playing?.Dispose();
        IsPlaying = false;
    }

    private void SetStep(int value, bool byHand)
    {
        if (byHand && IsPlaying) StopPlaying();
        int clamped = Math.Clamp(value, 0, MaxStep);
        if (_step != clamped)
        {
            _step = clamped;
            OnPropertyChanged(nameof(Step));
        }
        else if (value != clamped) OnPropertyChanged(nameof(Step));   // a slider pushed past the end goes back
        UpdateStepView();
    }

    private void UpdateStepView()
    {
        CurrentPoints = HasCloud ? ToPoints(Frames[_step]) : [];
        StepText = HasCloud ? StepTextFor(_step, MaxStep) : "";
    }

    private void ClearCloud(string? note)
    {
        Frames = [];
        _step = 0;
        OnPropertyChanged(nameof(Step));
        UpdateStepView();
        QualityText = "";
        CloudNote = note;
    }

    private void ShowCloud(IReadOnlyList<double[,]> frames, double distance)
    {
        Frames = frames;
        _step = 0;
        OnPropertyChanged(nameof(Step));
        UpdateStepView();
        QualityText = QualityFor(distance, MaxStep);
        CloudNote = null;
    }

    /// <summary>Shows the preset at once (its shape, no cloud yet), then loads its shipped network and works out the cloud.</summary>
    private Task ShowPresetAsync(string name)
    {
        var run = DetachRun();   // a new shape abandons a run under way
        StopPlaying();
        int generation = ++_sampleGeneration;
        int seed = Seed;
        var shape = Shapes.Presets.First(p => p.Name == name).Points;
        _net = null;
        _schedule = null;
        IsDrawnShape = false;
        ShapeName = name;
        TargetPoints = shape;
        DrawingMessage = "";
        UsingTrainedNetwork = false;
        RecordedDistance = double.NaN;
        NetworkText = "";
        ClearTraining();
        ClearCloud(SamplingMessage);
        run?.Dispose();
        return LoadPresetAsync(name, shape, seed, generation);
    }

    private async Task LoadPresetAsync(string name, IReadOnlyList<(double X, double Y)> shape, int seed, int generation)
    {
        try
        {
            var (net, schedule, shapeName, info) = await _family.DiffusionAsync(name);
            if (shapeName != name)
                throw new InvalidDataException($"The network file for the {name} learned a different shape ({shapeName}). Reinstalling the app puts it back.");
            var (frames, distance) = await Task.Run(() => SampleAndMeasure(net, schedule, shape, seed));
            if (generation != _sampleGeneration) return;
            _net = net;
            _schedule = schedule;
            _shippedSteps = info.StepsTrained;
            RecordedDistance = info.Results != null && info.Results.TryGetValue("meanDistance", out var recorded) ? recorded : double.NaN;
            NetworkText = ShippedNetworkText();
            LoadMessage = null;
            IsReady = true;
            ShowCloud(frames, distance);
            ReportScreens();   // the first time, the baselines: nothing lights for loading
        }
        catch (InvalidDataException e)
        {
            if (generation != _sampleGeneration) return;
            LoadMessage = e.Message;
            CloudNote = null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Anything else must not leave "Loading…" on screen for ever.
            ErrorLog.ReportBackground(e);
            if (generation != _sampleGeneration) return;
            LoadMessage = LoadFailedMessage;
            CloudNote = null;
        }
    }

    private async Task SampleAsync(DiffusionNet net, NoiseSchedule schedule, IReadOnlyList<(double X, double Y)> shape, int seed, int generation)
    {
        try
        {
            var (frames, distance) = await Task.Run(() => SampleAndMeasure(net, schedule, shape, seed));
            if (generation != _sampleGeneration) return;
            ShowCloud(frames, distance);
            ReportScreens();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            ErrorLog.ReportBackground(e);
            if (generation != _sampleGeneration) return;
            CloudNote = SampleFailedMessage;
        }
    }

    /// <summary>The sampler's frames for the seed, and the finished cloud's average distance from the shape.</summary>
    private static (IReadOnlyList<double[,]> Frames, double Distance) SampleAndMeasure(DiffusionNet net, NoiseSchedule schedule,
        IReadOnlyList<(double X, double Y)> shape, int seed)
    {
        var frames = DiffusionSampler.Sample(net, schedule, SampleCount, new SeededRandom(seed));
        return (frames, Shapes.MeanDistanceToShape(ToPoints(frames[^1]), shape));
    }

    private string ShippedNetworkText()
    {
        string text = $"Made by the shipped network that learned this shape ({N0(_shippedSteps)} training steps).";
        if (double.IsFinite(RecordedDistance))
            text += $" Recorded when it was built, with seed 1: {RecordedDistance.ToString("0.000", CultureInfo.InvariantCulture)}.";
        return text;
    }

    private string TrainedNetworkText() => $"Made by the network you trained ({N0(_trainedSteps)} training steps, seed {_trainedSeed}).";

    private static IReadOnlyList<(double X, double Y)> ToPoints(double[,] frame)
    {
        var points = new (double X, double Y)[frame.GetLength(0)];
        for (int i = 0; i < points.Length; i++) points[i] = (frame[i, 0], frame[i, 1]);
        return points;
    }

    private ISteppedTrainer CreateTrainer(int seed)
    {
        var schedule = new NoiseSchedule();
        var net = new DiffusionNet(DiffusionTraining.Retrain, new SeededRandom(seed));
        var trainer = new DiffusionTrainer(net, schedule, _trainingShape, RetrainSteps, seed);
        _trainingNet = net;
        _trainingSchedule = schedule;
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

    private void ReportScreens()
    {
        if (!IsReady) return;
        var cloud = new List<object> { ShapeName, NetworkText, QualityText, CloudNote ?? "" };
        if (HasCloud) cloud.AddRange(Frames[^1].Cast<object>());
        ReportScreen("cloud", cloud.ToArray());
        ReportScreen("training", new object[] { TrainingResult, TrainingStatus });
    }

    private static string N0(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

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
