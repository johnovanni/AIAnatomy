using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// Lesson 4.10's lab: inside the steps of the shipped spiral network (loaded through <see cref="FamilyModels.DiffusionAsync"/>).
/// <para><b>Forward:</b> <see cref="ForwardStep"/> t (0 to 100) shows the spiral's 1,000 points after t steps of noise,
/// <see cref="DiffusionSampler.AddNoise"/> with the seed, in one jump. A fresh <see cref="SeededRandom"/> each time, so every
/// point keeps the same noise at every t and only its share grows. The schedule chart shows ᾱ (0 to 100) and β (1 to 100),
/// with a line up from the axis to ᾱ at the step shown.</para>
/// <para><b>Prediction:</b> at the same t, 20 marked points (every 50th of the spiral's, so spread along it) get two arrows from
/// the noisy point: the noise really added (ε) and the network's guess (ε̂), both as they are, not scaled. The text gives the
/// mean squared error between them over the 40 numbers (as lesson 4.9's loss), and what guessing no noise would score. At
/// t = 0 there is no noise, and the network was never trained on t = 0, so there is nothing to predict.</para>
/// <para><b>Reverse:</b> a choice of <see cref="StepChoices"/> runs <see cref="DiffusionSampler.Sample"/> (the seed) on the
/// full schedule for 100 steps and on <see cref="NoiseSchedule.Respaced"/> otherwise, off the UI thread, and measures the
/// finished cloud. Results are kept per number of steps for the seed shown, so going back is instant; a new seed forgets
/// them and works out the current choice again. A seed change while a run (or the first run, while loading) is under way
/// starts it again with the new seed. <see cref="Frame"/> steps through the run.</para>
/// <para>Screens: "forward" (the noisy points and the step line), "prediction" (the arrows and error line) and "reverse"
/// (the choice, its result and finished cloud: not the frame shown, so the frame slider lights nothing).</para>
/// </summary>
public sealed partial class DiffusionStepsLabViewModel : LabViewModel
{
    public const string Id = "DiffusionStepsLab";
    public const string ShapeName = "Spiral";
    public const int SampleCount = 500;
    public const int DefaultForwardStep = 20;
    public const int DefaultSteps = 100;
    public const int MarkedCount = 20;

    /// <summary>The radius for <see cref="Shapes.Coverage"/>: a part of the shape counts as covered when a point of the
    /// cloud lies within 0.05 of it (the measure recorded for the shipped networks, docs/decisions.md Task 10).</summary>
    public const double CoverageRadius = 0.05;

    public const string LoadingMessage = "Loading the shipped spiral network…";
    public const string LoadFailedMessage = "The diffusion network could not be loaded because of an unexpected problem. Reinstalling the app may fix it.";
    public const string SampleFailedMessage = "The cloud could not be worked out. Please try again.";
    public const string NothingToPredictMessage =
        "At step 0 no noise has been added, so there is nothing to predict. Move the step to 1 or more.";

    /// <summary>The numbers of steps the reverse process can take: 100 (the full schedule), then fewer, bigger steps.</summary>
    public static IReadOnlyList<int> StepChoices { get; } = [100, 50, 20, 10, 5];

    private static readonly IReadOnlyList<int> Marked = Enumerable.Range(0, MarkedCount).Select(i => i * 50).ToArray();

    private readonly FamilyModels _family;
    private readonly int _sampleCount;
    private Task _work = Task.CompletedTask;
    private DiffusionNet? _net;
    private NoiseSchedule? _schedule;
    private int _forwardStep = DefaultForwardStep;
    private int _frame;
    private int _generation;      // the run on show; a later choice, a new seed, Reset or leaving drops an earlier one
    private int _seedGeneration;  // kept results belong to one seed
    private readonly Dictionary<int, RunResult> _kept = [];
    private int _keptSeed = 1;
    private bool _quiet;

    private sealed record RunResult(IReadOnlyList<double[,]> Frames, double Distance, double Coverage);

    public DiffusionStepsLabViewModel(FamilyModels family) : this(family, SampleCount) { }

    /// <param name="sampleCount">Points in the reverse process (a test lowers it where its check allows).</param>
    internal DiffusionStepsLabViewModel(FamilyModels family, int sampleCount)
    {
        _family = family ?? throw new ArgumentNullException(nameof(family));
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleCount, 1);
        _sampleCount = sampleCount;
        TargetPoints = Shapes.Presets.First(p => p.Name == ShapeName).Points;
        _work = LoadAsync(++_generation);
    }

    public override string LabId => Id;

    /// <summary>Loading the network and working out the latest run.</summary>
    public override Task WorkCompleted => _work;

    /// <summary>A test hook, called on the worker with the number of steps just before a run is sampled.</summary>
    internal Action<int>? BeforeSampling { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ChooseStepsCommand))]
    private bool isReady;

    /// <summary>While loading, what is happening; if loading failed, why. Null once loaded.</summary>
    [ObservableProperty] private string? loadMessage = LoadingMessage;

    /// <summary>The spiral's 1,000 points, drawn faintly behind each cloud.</summary>
    public IReadOnlyList<(double X, double Y)> TargetPoints { get; }

    // ---- forward ----

    public int MaxForwardStep => _schedule?.Steps ?? 100;

    /// <summary>The noise step t on show, 0 to <see cref="MaxForwardStep"/> (clamped).</summary>
    public int ForwardStep
    {
        get => _forwardStep;
        set
        {
            int clamped = Math.Clamp(value, 0, MaxForwardStep);
            if (_forwardStep != clamped)
            {
                _forwardStep = clamped;
                OnPropertyChanged();
                UpdateForward();
                ReportForwardScreens();
            }
            else if (value != clamped) OnPropertyChanged();   // a slider pushed past the end goes back
        }
    }

    /// <summary>The spiral's points after <see cref="ForwardStep"/> steps of noise (the seed); empty until loaded.</summary>
    [ObservableProperty] private IReadOnlyList<(double X, double Y)> forwardPoints = [];

    /// <summary>"Step 40 of 100: ᾱ = 0.398, so each point is …".</summary>
    [ObservableProperty] private string forwardText = "";

    /// <summary>ᾱ (t = 0 to 100), β (t = 1 to 100) and a line from the axis up to ᾱ at the step shown. The names are short,
    /// so the chart's legend (which does not wrap) fits a narrow pane at 24 pt; the caption under the chart says them in words.</summary>
    [ObservableProperty] private IReadOnlyList<LineSeries> scheduleSeries = [];

    // ---- prediction ----

    /// <summary>Which of the spiral's points are marked: every 50th, so they are spread along it.</summary>
    public IReadOnlyList<int> MarkedIndices => Marked;

    /// <summary>For each marked point, from the noisy point to the noisy point plus the noise really added.</summary>
    [ObservableProperty] private IReadOnlyList<((double X, double Y) From, (double X, double Y) To)> trueNoiseArrows = [];

    /// <summary>For each marked point, from the noisy point to the noisy point plus the network's guess of the noise.</summary>
    [ObservableProperty] private IReadOnlyList<((double X, double Y) From, (double X, double Y) To)> predictedNoiseArrows = [];

    /// <summary>The mean squared error between the true and guessed noise over the marked points; NaN at step 0.</summary>
    [ObservableProperty] private double predictionError = double.NaN;

    [ObservableProperty] private string predictionText = "";

    // ---- reverse ----

    /// <summary>The number of steps chosen for the reverse process.</summary>
    [ObservableProperty] private int sampleSteps = DefaultSteps;

    /// <summary>The run's frames: 0 is pure noise, the last the finished cloud; empty while it is worked out.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MaxFrame), nameof(HasRun))]
    private IReadOnlyList<double[,]> frames = [];

    public bool HasRun => Frames.Count > 0;

    public int MaxFrame => Math.Max(0, Frames.Count - 1);

    /// <summary>The frame on show, 0 to <see cref="MaxFrame"/> (clamped); a new run opens on its last frame.</summary>
    public int Frame
    {
        get => _frame;
        set
        {
            int clamped = Math.Clamp(value, 0, MaxFrame);
            if (_frame != clamped)
            {
                _frame = clamped;
                OnPropertyChanged();
            }
            else if (value != clamped) OnPropertyChanged();
            UpdateFrameView();
        }
    }

    [ObservableProperty] private IReadOnlyList<(double X, double Y)> currentPoints = [];

    /// <summary>"Step 3 of 5: the network has taken noise out 3 times; 2 steps to go."</summary>
    [ObservableProperty] private string frameText = "";

    /// <summary>The chosen run's measured result (live); empty while it is worked out.</summary>
    [ObservableProperty] private string resultText = "";

    /// <summary>Each number of steps measured so far with this seed, one line each, in the order of the choices.</summary>
    [ObservableProperty] private string measuredText = "";

    /// <summary>"Working out …" while a run is sampled, or why it failed; null when its result is shown, and while the
    /// network loads (the load message says so, and the two lines are both live, so they never hold the same words).</summary>
    [ObservableProperty] private string? reverseNote;

    /// <summary>"Each of the 5 steps jumps 20 steps of the 100-step schedule."</summary>
    [ObservableProperty] private string jumpText = JumpTextFor(DefaultSteps);

    public static string JumpTextFor(int steps) => steps >= 100
        ? "All 100 steps of the schedule, one at a time, as in the last lesson."
        : $"Each of the {steps} steps jumps {100 / steps} steps of the 100-step schedule, with the same network.";

    public static string WorkingMessageFor(int steps, int points) =>
        $"Working out {steps} steps for {points.ToString("N0", CultureInfo.InvariantCulture)} points…";

    /// <summary>"5 steps. Average distance from the shape: 0.142 (lower is better). Shape covered: 28% (higher is better)."</summary>
    public static string ResultFor(int steps, double distance, double coverage) =>
        $"{steps} steps. Average distance from the shape: {F3(distance)} (lower is better). " +
        $"Shape covered: {Percent(coverage)} (higher is better).";

    public static string PredictionTextFor(int t, double error, double noGuessError) =>
        t <= 0 ? NothingToPredictMessage
            : $"Mean squared error between the true and the guessed noise of the {MarkedCount} marked points: {F3(error)}. " +
              $"Guessing no noise at all would score {F3(noGuessError)} here; 0 would be perfect.";

    public static string FrameTextFor(int frame, int steps, int points)
    {
        if (frame <= 0) return $"Step 0 of {steps}: {points.ToString("N0", CultureInfo.InvariantCulture)} points of pure random noise. No noise has been taken out yet.";
        if (frame >= steps) return $"Step {steps} of {steps}: the finished cloud, after taking noise out {steps} times.";
        string times = frame == 1 ? "once" : $"{frame} times";
        int left = steps - frame;
        return $"Step {frame} of {steps}: the network has taken noise out {times}; {(left == 1 ? "1 step" : $"{left} steps")} to go.";
    }

    /// <summary>Runs the reverse process with <paramref name="steps"/> steps (one of <see cref="StepChoices"/>), or shows its
    /// kept result at once. The choices stay enabled while a run is worked out: a later choice wins.</summary>
    [RelayCommand(CanExecute = nameof(IsReady), AllowConcurrentExecutions = true)]
    public Task ChooseStepsAsync(int steps)
    {
        if (!StepChoices.Contains(steps)) throw new ArgumentOutOfRangeException(nameof(steps), steps, "Choose 100, 50, 20, 10 or 5 steps.");
        if (!IsReady) return Task.CompletedTask;
        SampleSteps = steps;
        return _work = ShowRunAsync(steps, ++_generation);
    }

    public override void Reset()
    {
        _quiet = true;
        try { Seed = 1; }
        finally { _quiet = false; }
        _forwardStep = DefaultForwardStep;
        OnPropertyChanged(nameof(ForwardStep));
        SampleSteps = DefaultSteps;
        if (_net == null) return;   // still loading (the load reads these), or loading failed
        int generation = ++_generation;
        ForgetIfSeedChanged();
        UpdateForward();
        ReportForwardScreens();
        _work = ShowRunAsync(DefaultSteps, generation);
    }

    public override void Deactivate() => _generation++;

    protected override void OnSeedEdited()
    {
        if (_quiet || _net == null) return;   // still loading: the load reads the seed once the network is in
        int generation = ++_generation;       // drops a run under way for the old seed
        ForgetIfSeedChanged();
        UpdateForward();
        ReportForwardScreens();
        _work = ShowRunAsync(SampleSteps, generation);
    }

    private void ForgetIfSeedChanged()
    {
        if (_keptSeed == Seed) return;
        _kept.Clear();
        _keptSeed = Seed;
        _seedGeneration++;
        MeasuredText = "";
    }

    private async Task LoadAsync(int generation)
    {
        try
        {
            var (net, schedule, shapeName, _) = await _family.DiffusionAsync(ShapeName);
            if (shapeName != ShapeName)
                throw new InvalidDataException($"The network file for the {ShapeName} learned a different shape ({shapeName}). Reinstalling the app puts it back.");
            _net = net;
            _schedule = schedule;
            LoadMessage = null;
            ForgetIfSeedChanged();
            OnPropertyChanged(nameof(MaxForwardStep));
            UpdateForward();
            IsReady = true;
            ReportForwardScreens();   // the first time, the baselines: nothing lights for loading
            // A seed changed (or Reset) before the network was in is read here; from now on a new seed starts its own run.
            // Only leaving the lab (Deactivate) moves the generation on while loading.
            if (generation == _generation) await ShowRunAsync(SampleSteps, generation);
        }
        catch (InvalidDataException e)
        {
            LoadMessage = e.Message;
            ReverseNote = null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Anything else must not leave "Loading…" on screen for ever.
            ErrorLog.ReportBackground(e);
            LoadMessage = LoadFailedMessage;
            ReverseNote = null;
        }
    }

    private async Task ShowRunAsync(int steps, int generation)
    {
        if (_net is not { } net || _schedule is not { } schedule) return;
        if (_kept.TryGetValue(steps, out var kept))
        {
            ShowRun(steps, kept);
            return;
        }
        Frames = [];
        _frame = 0;
        OnPropertyChanged(nameof(Frame));
        UpdateFrameView();
        ResultText = "";
        JumpText = JumpTextFor(steps);
        ReverseNote = WorkingMessageFor(steps, _sampleCount);
        int seed = Seed, seedGeneration = _seedGeneration, count = _sampleCount;
        var hook = BeforeSampling;
        try
        {
            var result = await Task.Run(() =>
            {
                hook?.Invoke(steps);
                var frames = DiffusionSampler.Sample(net, steps == schedule.Steps ? schedule : schedule.Respaced(steps), count, new SeededRandom(seed));
                var last = ToPoints(frames[^1]);
                return new RunResult(frames, Shapes.MeanDistanceToShape(last, TargetPoints), Shapes.Coverage(last, TargetPoints, CoverageRadius));
            });
            if (seedGeneration == _seedGeneration) _kept[steps] = result;   // still this seed's: keep it even if no longer shown
            if (generation != _generation) return;
            ShowRun(steps, result);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            ErrorLog.ReportBackground(e);
            if (generation != _generation) return;
            ReverseNote = SampleFailedMessage;
        }
    }

    private void ShowRun(int steps, RunResult run)
    {
        Frames = run.Frames;
        _frame = MaxFrame;
        OnPropertyChanged(nameof(Frame));
        UpdateFrameView();
        JumpText = JumpTextFor(steps);
        ResultText = ResultFor(steps, run.Distance, run.Coverage);
        ReverseNote = null;
        MeasuredText = MeasuredTextFor();
        var report = new List<object> { steps, ResultText };
        report.AddRange(run.Frames[^1].Cast<object>());
        ReportScreen("reverse", report.ToArray());
    }

    private string MeasuredTextFor()
    {
        var lines = StepChoices.Where(_kept.ContainsKey)
            .Select(s => $"{s} steps: {F3(_kept[s].Distance)} from the shape on average; {Percent(_kept[s].Coverage)} of it covered");
        return $"Measured so far with seed {_keptSeed}:\n" + string.Join("\n", lines);
    }

    private void UpdateForward()
    {
        if (_net is not { } net || _schedule is not { } schedule) return;
        int t = _forwardStep;
        var (noisy, noise) = DiffusionSampler.AddNoise(TargetPoints, t, schedule, new SeededRandom(Seed));
        ForwardPoints = ToPoints(noisy);
        double alphaBar = schedule.AlphaBar(t);
        ForwardText = t == 0
            ? $"Step 0 of {schedule.Steps}: the shape itself. No noise has been added yet."
            : $"Step {t} of {schedule.Steps}: ᾱ = {F3(alphaBar)}, so each point is {F3(Math.Sqrt(alphaBar))} × its place on the shape plus " +
              $"{F3(Math.Sqrt(1 - alphaBar))} × its noise. This step's β is {schedule.Beta(t).ToString("0.0000", CultureInfo.InvariantCulture)}.";
        ScheduleSeries =
        [
            new LineSeries("ᾱ", Enumerable.Range(0, schedule.Steps + 1).Select(i => ((double)i, schedule.AlphaBar(i))).ToArray()),
            new LineSeries("β", Enumerable.Range(1, schedule.Steps).Select(i => ((double)i, schedule.Beta(i))).ToArray()),
            new LineSeries($"t = {t}", [(t, 0.0), (t, alphaBar)]),
        ];

        if (t == 0)
        {
            TrueNoiseArrows = [];
            PredictedNoiseArrows = [];
            PredictionError = double.NaN;
            PredictionText = PredictionTextFor(0, double.NaN, double.NaN);
            return;
        }
        var input = new double[Marked.Count * 2];
        for (int k = 0; k < Marked.Count; k++)
        {
            input[2 * k] = noisy[Marked[k], 0];
            input[2 * k + 1] = noisy[Marked[k], 1];
        }
        var guess = net.Forward(Tensor.FromArray(input, Marked.Count, 2), Enumerable.Repeat(t, Marked.Count).ToArray(), schedule.OriginalSteps).Data;
        var trueArrows = new ((double X, double Y) From, (double X, double Y) To)[Marked.Count];
        var guessArrows = new ((double X, double Y) From, (double X, double Y) To)[Marked.Count];
        double squared = 0, baseline = 0;
        for (int k = 0; k < Marked.Count; k++)
        {
            var from = (input[2 * k], input[2 * k + 1]);
            double ex = noise[Marked[k], 0], ey = noise[Marked[k], 1];
            trueArrows[k] = (from, (from.Item1 + ex, from.Item2 + ey));
            guessArrows[k] = (from, (from.Item1 + guess[2 * k], from.Item2 + guess[2 * k + 1]));
            squared += (ex - guess[2 * k]) * (ex - guess[2 * k]) + (ey - guess[2 * k + 1]) * (ey - guess[2 * k + 1]);
            baseline += ex * ex + ey * ey;
        }
        TrueNoiseArrows = trueArrows;
        PredictedNoiseArrows = guessArrows;
        PredictionError = squared / (2 * Marked.Count);
        PredictionText = PredictionTextFor(t, PredictionError, baseline / (2 * Marked.Count));
    }

    private void UpdateFrameView()
    {
        CurrentPoints = HasRun ? ToPoints(Frames[_frame]) : [];
        FrameText = HasRun ? FrameTextFor(_frame, MaxFrame, _sampleCount) : "";
    }

    private void ReportForwardScreens()
    {
        if (!IsReady) return;
        var forward = new List<object> { ForwardText };
        forward.AddRange(ForwardPoints.Select(p => (object)p.X).Concat(ForwardPoints.Select(p => (object)p.Y)));
        ReportScreen("forward", forward.ToArray());
        var prediction = new List<object> { PredictionText };
        prediction.AddRange(PredictedNoiseArrows.SelectMany(a => new object[] { a.From.X, a.From.Y, a.To.X, a.To.Y }));
        ReportScreen("prediction", prediction.ToArray());
    }

    private static IReadOnlyList<(double X, double Y)> ToPoints(double[,] frame)
    {
        var points = new (double X, double Y)[frame.GetLength(0)];
        for (int i = 0; i < points.Length; i++) points[i] = (frame[i, 0], frame[i, 1]);
        return points;
    }

    private static string F3(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
