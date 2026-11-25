using System.Diagnostics;
using AIAnatomy.Models.Training;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// The "train it again" button of a lab: builds a fresh trainer for a seed, trains it on a worker thread and shows the
/// step, the loss and the time, at most 20 times a second. A completed run sets <see cref="Finished"/> and raises
/// <see cref="Completed"/>; Stop keeps what was trained but leaves Finished null; Abandon cancels and drops every late report.
/// </summary>
public sealed partial class RetrainRunner : ObservableObject
{
    public const string LossName = "training loss";
    public static readonly TimeSpan DefaultUpdateInterval = TimeSpan.FromMilliseconds(50);
    private const int MaxPoints = 400;

    private readonly Func<int, ISteppedTrainer> _createTrainer;
    private readonly Func<TimeSpan> _now;
    private readonly object _gate = new();
    private List<(double X, double Y)> _points = [];
    private CancellationTokenSource? _cts;
    private int _generation;
    private TimeSpan _startedAt, _lastApplied, _frozen;
    private bool _hasApplied;
    private int _stride = 1, _seen;

    public RetrainRunner(Func<int, ISteppedTrainer> createTrainer) : this(createTrainer, null) { }

    /// <summary>A test may give its own clock (time since any fixed moment), so nothing waits on real time.</summary>
    public RetrainRunner(Func<int, ISteppedTrainer> createTrainer, Func<TimeSpan>? clock)
    {
        _createTrainer = createTrainer ?? throw new ArgumentNullException(nameof(createTrainer));
        if (clock != null) _now = clock;
        else { var watch = Stopwatch.StartNew(); _now = () => watch.Elapsed; }
    }

    /// <summary>The least time between two updates (a test may set it to zero).</summary>
    public TimeSpan UpdateInterval { get; set; } = DefaultUpdateInterval;

    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private IReadOnlyList<LineSeries> lossSeries = [];
    [ObservableProperty] private ISteppedTrainer? finished;

    public event EventHandler? Completed;

    /// <summary>Time spent on the current (or last) run.</summary>
    public TimeSpan Elapsed => IsRunning ? _now() - _startedAt : _frozen;

    public async Task StartAsync(int seed)
    {
        if (IsRunning) return;
        int generation = Interlocked.Increment(ref _generation);
        var cts = new CancellationTokenSource();
        _cts = cts;
        var ui = SynchronizationContext.Current;
        var trainer = _createTrainer(seed);
        _points = [];
        _stride = 1; _seen = 0; _hasApplied = false;
        _startedAt = _now(); _frozen = TimeSpan.Zero;
        Finished = null;
        LossSeries = [];
        StatusText = "";
        IsRunning = true;

        var reporter = new Progress(p =>
        {
            // On the worker thread: decide here whether this report is shown (the last one always is).
            if (generation != Volatile.Read(ref _generation)) return;
            var now = _now();
            bool last = p.Step >= p.TotalSteps;
            lock (_gate)
            {
                if (!last && _hasApplied && now - _lastApplied < UpdateInterval) return;
                _hasApplied = true; _lastApplied = now;
            }
            Post(ui, generation, () => Apply(p, now));
        });

        string? problem;
        try { problem = await SteppedTraining.RunAsync(trainer, reporter, cts.Token); }
        catch (Exception e) when (e is not OutOfMemoryException) { problem = "The training went wrong: " + e.Message; }

        if (generation != Volatile.Read(ref _generation)) return;   // abandoned: show nothing more
        _frozen = _now() - _startedAt;
        if (_cts == cts) _cts = null;
        IsRunning = false;
        if (problem != null) StatusText = problem;
        else if (trainer.StepsTaken >= trainer.TotalSteps)
        {
            Finished = trainer;
            Completed?.Invoke(this, EventArgs.Empty);
        }
        else StatusText = $"Stopped after {NumberText.Short(trainer.StepsTaken)} steps.";
    }

    /// <summary>Ends the run early, keeping what was trained so far; <see cref="Finished"/> stays null.</summary>
    public void Stop() => _cts?.Cancel();

    /// <summary>Cancels the run and drops every report still to come (Reset, leaving the lab, a new seed).</summary>
    public void Abandon()
    {
        Interlocked.Increment(ref _generation);
        _cts?.Cancel();
        _cts = null;
        if (IsRunning) { _frozen = _now() - _startedAt; IsRunning = false; }
    }

    private void Post(SynchronizationContext? ui, int generation, Action apply)
    {
        void Run() { if (generation == Volatile.Read(ref _generation)) apply(); }
        if (ui == null) Run(); else ui.Post(_ => Run(), null);
    }

    private void Apply(TrainingProgress p, TimeSpan now)
    {
        _seen++;
        if (_seen % _stride == 0 || p.Step >= p.TotalSteps)
        {
            _points.Add((p.Step, p.Loss));
            if (_points.Count > MaxPoints)
            {
                _points = _points.Where((_, i) => i % 2 == 0 || i == _points.Count - 1).ToList();
                _stride *= 2;
            }
            LossSeries = [new LineSeries(LossName, _points.ToArray())];
        }
        int seconds = (int)Math.Max(0, (now - _startedAt).TotalSeconds);
        StatusText = $"Step {NumberText.Short(p.Step)} of {NumberText.Short(p.TotalSteps)} · loss {NumberText.Short(p.Loss)} · {seconds} s";
    }

    private sealed class Progress(Action<TrainingProgress> report) : IProgress<TrainingProgress>
    {
        public void Report(TrainingProgress value) => report(value);
    }
}
