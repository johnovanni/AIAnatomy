using AIAnatomy.Core;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One letter of the experiment's sequence, with which copy of the repeated segment it belongs to (if any).</summary>
/// <summary>Where a copy of the repeated segment sits: 0-based first column, how many letters, and its name.</summary>
public sealed record CopySpan(int Column, int Length, string Name);

public sealed record SequenceCell(int Position, string Letter, string Copy)
{
    /// <summary>The 0-based column of this letter.</summary>
    public int Index => Position - 1;
}

/// <summary>How strongly one head behaves like an induction head and like a previous-token head (both are averages from 0 to 1).</summary>
public sealed record HeadScore(int Layer, int Head, double Induction, double PreviousToken)
{
    public string Name => $"Layer {Layer}, head {Head}";
}

/// <summary>
/// The induction-head experiment. The tiny two-layer model is either the shipped, already trained demo or one the user
/// trains here (in the background, with a live loss chart and the head scores refreshed every 100 steps). The heat map
/// shows one head's attention over one example sequence, with the "token after the earlier copy" squares outlined.
/// </summary>
public sealed partial class InductionPanelViewModel : ObservableObject
{
    public const string DemoName = "Induction demo";
    public const int ScoreEvery = 100;
    private const int LossEvery = 10;
    private const int ShownTokens = 16;   // the model's context length

    public const string LossNote =
        "When the model trains, the loss often drops sharply partway through. This is similar to the sudden improvement " +
        "researchers saw when induction heads formed (Olsson et al., 2022). It is not guaranteed to look the same, or to happen at the same step, in every run.";

    public const string CircuitNote =
        "An induction head needs help from an earlier layer. A previous-token head in layer 1 copies “what came before me” into each token, " +
        "and an induction head in layer 2 uses that to find the earlier copy and look at the token after it (Olsson et al., 2022). " +
        "The previous-token score shows how strongly each head looks one token back.";

    public const string OutlineNote =
        "Outlined squares: for each letter in the second copy, the letter that came after its twin in the first copy. " +
        "An induction head puts a lot of attention there.";

    private readonly IActiveModelService _models;
    private readonly object _gate = new();
    private CancellationTokenSource? _training;
    private Task _work = Task.CompletedTask;
    private Task _background = Task.CompletedTask;
    private int _generation;
    private bool _applying;
    private int _seed = 1;
    private InductionSequence _sequence = InductionExperiment.MakeSequence(new SeededRandom(1));
    private Snapshot? _snapshot;
    private readonly List<(double X, double Y)> _loss = new();

    public InductionPanelViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        LayerNumbers = Enumerable.Range(1, InductionExperiment.Config.Layers).ToList();
        HeadNumbers = Enumerable.Range(1, InductionExperiment.Config.Heads).ToList();
        BuildSequence();
    }

    /// <summary>The training length; the lesson uses <see cref="InductionExperiment.DefaultSteps"/>, tests may lower it.</summary>
    public int Steps { get; set; } = InductionExperiment.DefaultSteps;

    /// <summary>
    /// The seed shared with the lab: it picks the example sequence, the sequences the scores average over and starts the training.
    /// Changing it while a model is training only takes effect for the next run.
    /// </summary>
    public int Seed
    {
        get => _seed;
        set
        {
            if (IsTraining)
            {
                OnPropertyChanged(nameof(Seed));   // refused: the number shown goes back to the one in use
                return;
            }
            if (!SetProperty(ref _seed, value)) return;
            BuildSequence();
            if (_snapshot?.Model is { } model) Remeasure(model);
        }
    }

    public Task WorkCompleted => Task.WhenAll(_work, _background, ShowDemoCommand.ExecutionTask ?? Task.CompletedTask);

    public IReadOnlyList<int> LayerNumbers { get; }
    public IReadOnlyList<int> HeadNumbers { get; }

    [ObservableProperty] private IReadOnlyList<SequenceCell> sequenceCells = [];
    [ObservableProperty] private IReadOnlyList<CopySpan> copySpans = [];
    [ObservableProperty] private IReadOnlyList<HeadScore> scores = [];
    [ObservableProperty] private IReadOnlyList<LabeledValue> inductionBars = [];
    [ObservableProperty] private IReadOnlyList<LabeledValue> previousTokenBars = [];
    [ObservableProperty] private string scoreSummary = "";
    /// <summary>What the scores and the heat map come from.</summary>
    [ObservableProperty] private string source = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool isTraining;
    [ObservableProperty] private double progress;
    [ObservableProperty] private string progressText = "";
    [ObservableProperty] private IReadOnlyList<LineSeries> lossSeries = [];
    [ObservableProperty] private double[,]? heatMap;
    [ObservableProperty] private IReadOnlyList<string> tokenLabels = [];
    [ObservableProperty] private IReadOnlyList<GridCell> outlinedCells = [];
    [ObservableProperty] private int layer = 2;
    [ObservableProperty] private int head = 1;
    [ObservableProperty] private bool hasModel;

    partial void OnLayerChanged(int value) => ShowHeat();
    partial void OnHeadChanged(int value) => ShowHeat();

    /// <summary>Loads the shipped trained model (it does not become the app's active model). The measuring runs off the UI thread.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ShowDemoAsync()
    {
        Deactivate();   // any training stops and its later reports are not shown
        int generation;
        lock (_gate) generation = _generation;
        var entry = _models.Library.List().FirstOrDefault(e => e.Name == DemoName && e.IsReadable);
        if (entry == null)
        {
            Message = "The trained demo model could not be found, so it cannot be shown. You can still train one yourself.";
            return;
        }
        var sequence = _sequence;
        int seed = Seed;
        try
        {
            var (info, snapshot) = await Task.Run(() =>
            {
                var model = _models.Library.Load(entry);   // this model is not shared with anything else
                return (model.Info, Measure(model.Transformer, sequence, seed, retain: true));
            });
            if (generation != _generation) return;
            Message = null;
            ClearTraining();
            Source = $"The trained demo ({NumberText.Short(info.StepsTrained)} training steps)";
            Apply(snapshot, chooseBestHead: true);
        }
        catch (InvalidDataException e)
        {
            if (generation == _generation) Message = $"The trained demo model could not be read: {e.Message}";
        }
    }

    /// <summary>Trains a new model from scratch on the induction task, off the UI thread, until done or stopped.</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task TrainAsync(CancellationToken token)
    {
        StopTraining();
        int generation;
        lock (_gate) generation = ++_generation;
        var context = SynchronizationContext.Current;
        void OnUi(Action action)
        {
            if (context == null) { if (generation == _generation) action(); }
            else context.Post(_ => { if (generation == _generation) action(); }, null);
        }

        int steps = Math.Max(1, Steps);
        int seed = Seed;
        var sequence = _sequence;
        ClearTraining();
        Message = null;
        IsTraining = true;
        Source = "The model you are training";
        ProgressText = $"Step 0 of {NumberText.Short(steps)}";

        // The trainer reports between steps, on its own thread, while the model is not changing. The scores are
        // measured right there, so nothing reads the model while it is being updated.
        Transformer? model = null;
        var reporter = new StepReporter(report =>
        {
            if (report.Step % LossEvery != 0 && report.Step != steps && report.StoppedReason == null) return;
            var point = (X: (double)report.Step, Y: report.TrainLoss);
            var snapshot = report.Step % ScoreEvery == 0 ? Measure(model!, sequence, seed, retain: false) : null;
            OnUi(() =>
            {
                if (double.IsFinite(point.Y)) _loss.Add(point);
                Progress = (double)report.Step / steps;
                ProgressText = $"Step {NumberText.Short(report.Step)} of {NumberText.Short(steps)}, loss {NumberText.Short(point.Y)}";
                LossSeries = [new LineSeries("Loss", _loss.ToList())];
                if (snapshot != null) Apply(snapshot);
                if (report.StoppedReason != null) Message = report.StoppedReason;
            });
        });

        var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        _training = linked;
        try
        {
            var run = InductionExperiment.TrainAsync(steps, seed, reporter, linked.Token, m => model = m);
            _work = run;
            var trained = await run;
            if (generation != _generation) return;
            var final = await Task.Run(() => Measure(trained, sequence, seed, retain: true));
            if (generation != _generation) return;
            Apply(final);
            if (linked.IsCancellationRequested) Message = "Training was stopped. What is shown is the model as it was then.";
            else Progress = 1;
        }
        catch (Exception e)
        {
            if (generation == _generation) Message = "Training stopped: " + e.Message;
        }
        finally
        {
            if (generation == _generation) IsTraining = false;
            if (ReferenceEquals(_training, linked)) _training = null;
        }
    }

    /// <summary>Stops any training in progress; what it reports afterwards is not shown.</summary>
    public void Deactivate()
    {
        StopTraining();
        lock (_gate) _generation++;
        IsTraining = false;
    }

    public void Reset()
    {
        Deactivate();
        Seed = 1;
        ClearTraining();
        Message = null;
        Source = "";
        Scores = [];
        InductionBars = PreviousTokenBars = [];
        ScoreSummary = "";
        HasModel = false;
        _snapshot = null;
        Layer = 2;
        Head = 1;
        ShowHeat();
    }

    private void StopTraining() => _training?.Cancel();

    private void ClearTraining()
    {
        _loss.Clear();
        LossSeries = [];
        Progress = 0;
        ProgressText = "";
    }

    private void BuildSequence()
    {
        _sequence = InductionExperiment.MakeSequence(new SeededRandom(Seed));
        SequenceCells = Cells(_sequence);
        CopySpans = Spans(_sequence);
        TokenLabels = Labels(_sequence);
    }

    private static IReadOnlyList<CopySpan> Spans(InductionSequence s) =>
        [new CopySpan(s.FirstStart, s.Length, "first copy"), new CopySpan(s.SecondStart, s.Length, "second copy")];

    private static IReadOnlyList<SequenceCell> Cells(InductionSequence s) =>
        s.Tokens.Select((id, i) => new SequenceCell(i + 1, InductionExperiment.Tokenizer.TokenText(id),
            i >= s.FirstStart && i < s.FirstStart + s.Length ? "first copy"
            : i >= s.SecondStart && i < s.SecondStart + s.Length ? "second copy" : "")).ToList();

    private static IReadOnlyList<string> Labels(InductionSequence s) =>
        s.Tokens.Take(ShownTokens).Select((id, i) => $"{i + 1}:{InductionExperiment.Tokenizer.TokenText(id)}").ToList();

    /// <summary>One measurement of a model: its scores and every head's attention over the example sequence.</summary>
    private sealed record Snapshot(Transformer? Model, InductionSequence Sequence, int Seed, IReadOnlyList<HeadScore> Scores, double[][][,] Heat);

    /// <summary>
    /// Measures a model whose weights are not changing right now. <paramref name="retain"/> keeps the model in the snapshot so a
    /// seed change can measure it again; a model that is still training is never kept.
    /// </summary>
    private static Snapshot Measure(Transformer model, InductionSequence sequence, int seed, bool retain)
    {
        var config = model.Config;
        var scores = new List<HeadScore>();
        for (int l = 0; l < config.Layers; l++)
            for (int h = 0; h < config.Heads; h++)
                scores.Add(new HeadScore(l + 1, h + 1,
                    InductionExperiment.InductionScore(model, l, h, seed), InductionExperiment.PreviousTokenScore(model, l, h, seed)));
        var trace = model.Forward(sequence.Tokens.Take(ShownTokens).ToArray()).Trace;
        var heat = trace.Blocks.Select(b => b.Heads.Select(hd => Grid(hd.Weights)).ToArray()).ToArray();
        return new Snapshot(retain ? model : null, sequence, seed, scores, heat);
    }

    /// <summary>Measures a finished model again (after a seed change) off the UI thread, then shows it unless something newer happened.</summary>
    private void Remeasure(Transformer model)
    {
        int generation;
        lock (_gate) generation = _generation;
        var sequence = _sequence;
        int seed = Seed;
        _background = RemeasureAsync(model, sequence, seed, generation);
    }

    private async Task RemeasureAsync(Transformer model, InductionSequence sequence, int seed, int generation)
    {
        var snapshot = await Task.Run(() => Measure(model, sequence, seed, retain: true));
        if (generation == _generation && seed == Seed) Apply(snapshot);
    }

    private void Apply(Snapshot snapshot, bool chooseBestHead = false)
    {
        _snapshot = snapshot;
        Scores = snapshot.Scores;
        InductionBars = snapshot.Scores.Select(s => new LabeledValue(s.Name, s.Induction)).ToList();
        PreviousTokenBars = snapshot.Scores.Select(s => new LabeledValue(s.Name, s.PreviousToken)).ToList();
        HasModel = true;
        var bestInduction = snapshot.Scores.OrderByDescending(s => s.Induction).First();
        var bestPrevious = snapshot.Scores.Where(s => s.Layer == 1).OrderByDescending(s => s.PreviousToken).FirstOrDefault();
        ScoreSummary = $"Highest induction score: layer {bestInduction.Layer}, head {bestInduction.Head} ({NumberText.Short(bestInduction.Induction)})."
                       + (bestPrevious == null ? "" : $" Highest previous-token score in layer 1: head {bestPrevious.Head} ({NumberText.Short(bestPrevious.PreviousToken)}).");
        if (chooseBestHead)
        {
            _applying = true;
            Layer = bestInduction.Layer;
            Head = bestInduction.Head;
            _applying = false;
        }
        ShowHeat();
    }

    private void ShowHeat()
    {
        if (_applying) return;
        if (_snapshot is not { } snapshot)
        {
            HeatMap = null;
            OutlinedCells = [];
            return;
        }
        int l = Math.Clamp(Layer, 1, snapshot.Heat.Length) - 1;
        int h = Math.Clamp(Head, 1, snapshot.Heat[l].Length) - 1;
        HeatMap = snapshot.Heat[l][h];
        SequenceCells = Cells(snapshot.Sequence);
        CopySpans = Spans(snapshot.Sequence);
        TokenLabels = Labels(snapshot.Sequence);
        OutlinedCells = InductionCells(snapshot.Sequence);
    }

    /// <summary>The squares an induction head fills: row SecondStart + j, column FirstStart + j + 1, for rows inside the context (R16).</summary>
    public static IReadOnlyList<GridCell> InductionCells(InductionSequence sequence)
    {
        var cells = new List<GridCell>();
        for (int j = 0; j < sequence.Length; j++)
        {
            int row = sequence.SecondStart + j;
            if (row >= ShownTokens) break;
            cells.Add(new GridCell(row, sequence.FirstStart + j + 1));
        }
        return cells;
    }

    /// <summary>Attention weights with the squares above the diagonal (future positions) marked blocked (not a number).</summary>
    public static double[,] Grid(Tensor weights)
    {
        int n = weights.Shape[0];
        var grid = new double[n, n];
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++) grid[r, c] = c > r ? double.NaN : weights.Data[r * n + c];
        return grid;
    }

    /// <summary>An <see cref="IProgress{T}"/> that runs its action on the calling (training) thread, between steps.</summary>
    private sealed class StepReporter(Action<TrainingStepReport> action) : IProgress<TrainingStepReport>
    {
        public void Report(TrainingStepReport value) => action(value);
    }
}
