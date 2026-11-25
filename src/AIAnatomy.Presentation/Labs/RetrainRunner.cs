using System.Diagnostics;
using AIAnatomy.Models.Training;
using AIAnatomy.Presentation.Services;
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
    public const string UnexpectedMessage = "Training stopped because of an unexpected problem.";
    public static readonly TimeSpan DefaultUpdateInterval = TimeSpan.FromMilliseconds(50);
    private const int MaxPoints = 400;

    private readonly Func<int, ISteppedTrainer> _createTrainer;
    private readonly Func<TimeSpan> _now;
    private List<(double X, double Y)> _points = [];
    private CancellationTokenSource? _cts;
    private int _generation;
    private TimeSpan _startedAt, _frozen;
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

    /// <summary>Raised after <see cref="Finished"/> is set, once per completed run (see <see cref="StartAsync"/> for the thread).</summary>
    public event EventHandler? Completed;

    /// <summary>Time spent on the current (or last) run.</summary>
    public TimeSpan Elapsed => IsRunning ? _now() - _startedAt : _frozen;

    /// <summary>
    /// Starts a run (ignored while one is running). With a UI SynchronizationContext (the app) every update and
    /// <see cref="Completed"/> happen on that thread. With none (a self-test or unit test, a thread-pool caller) they happen
    /// on the thread that produced them: the worker for updates, a thread-pool thread for Completed. That is deliberate, so
    /// labs and the self-test can run without a window.
    /// </summary>
    public async Task StartAsync(int seed)
    {
        if (IsRunning) return;
        var trainer = _createTrainer(seed);   // before any state changes: if it throws, nothing is half-set
        int generation = Interlocked.Increment(ref _generation);
        var cts = new CancellationTokenSource();
        _cts = cts;
        var ui = SynchronizationContext.Current;
        var throttle = new Throttle();
        _points = [];
        _stride = 1; _seen = 0;
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
            if (!throttle.TryAccept(now, last ? TimeSpan.Zero : UpdateInterval)) return;
            Post(ui, generation, () => Apply(p, now));
        });

        string? problem;
        try { problem = await SteppedTraining.RunAsync(trainer, reporter, cts.Token); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            ErrorLog.ReportBackground(e);
            problem = UnexpectedMessage;
        }

        bool current = generation == Volatile.Read(ref _generation);
        if (current && _cts == cts) _cts = null;
        cts.Dispose();
        if (!current) return;   // abandoned: show nothing more
        _frozen = _now() - _startedAt;
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
    public void Stop()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
    }

    /// <summary>Cancels the run and drops every report still to come (Reset, leaving the lab, a new seed).</summary>
    public void Abandon()
    {
        Interlocked.Increment(ref _generation);
        var cts = _cts;
        _cts = null;
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }   // the run disposes it when its task ends
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

    /// <summary>One run's update throttle, so an old run can never hold back a new run's first report.</summary>
    private sealed class Throttle
    {
        private TimeSpan _last;
        private bool _any;

        public bool TryAccept(TimeSpan now, TimeSpan interval)
        {
            if (_any && now - _last < interval) return false;
            _any = true; _last = now;
            return true;
        }
    }

    private sealed class Progress(Action<TrainingProgress> report) : IProgress<TrainingProgress>
    {
        public void Report(TrainingProgress value) => report(value);
    }
}
