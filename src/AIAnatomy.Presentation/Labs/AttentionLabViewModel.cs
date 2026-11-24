using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// Attention in three parts: a two-dimensional toy you can compute by hand, the real attention of the active model for
/// your text, and the induction-head experiment.
/// </summary>
public sealed partial class AttentionLabViewModel : LabViewModel
{
    public const string Id = "AttentionLab";
    public const string DefaultText = "the fox and the fox";
    public const string DefaultLetters = "ABCDEFABCDE";

    public const string FutureNote =
        "Each row is one token looking back; its squares add up to 1. Hatched squares are blocked: a token cannot look at positions after it.";

    private readonly IActiveModelService _models;
    private string _lastDefault;
    private bool _refreshing;
    private readonly Stack<IDisposable> _runs = new();   // one scope per induction run that is still going (they may overlap)

    public AttentionLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        Toy = new AttentionToyViewModel();
        Induction = new InductionPanelViewModel(models);
        Toy.Recomputed += (_, _) => ReportToy();
        Induction.Updated += ReportInduction;
        Induction.RunStarted += () => { lock (_runs) _runs.Push(BeginRun()); };
        Induction.RunEnded += () =>
        {
            IDisposable? run;
            lock (_runs) run = _runs.Count > 0 ? _runs.Pop() : null;
            run?.Dispose();
        };
        ReportToy();
        _lastDefault = text = DefaultFor(models.Current);
        Refresh();
        Induction.ShowDemoCommand.Execute(null);   // the lab opens with the trained demo on show, so there is something to look at at once
    }

    public override string LabId => Id;

    public AttentionToyViewModel Toy { get; }
    public InductionPanelViewModel Induction { get; }

    [ObservableProperty] private string text;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    [ObservableProperty] private int layer = 1;
    [ObservableProperty] private int head = 1;
    [ObservableProperty] private IReadOnlyList<int> layerNumbers = [];
    [ObservableProperty] private IReadOnlyList<int> headNumbers = [];
    /// <summary>[T,T] attention weights of the chosen head; squares above the diagonal are not a number (blocked).</summary>
    [ObservableProperty] private double[,]? realWeights;
    [ObservableProperty] private IReadOnlyList<string> tokenLabels = [];
    [ObservableProperty] private string realCaption = "";

    partial void OnTextChanged(string value) => Refresh();
    partial void OnLayerChanged(int value) => Refresh();
    partial void OnHeadChanged(int value) => Refresh();

    protected override void OnSeedEdited()
    {
        Induction.Seed = Seed;
        if (Seed != Induction.Seed) Seed = Induction.Seed;   // refused while training
    }

    public override Task WorkCompleted => Induction.WorkCompleted;

    public override void Reset()
    {
        Toy.Reset();
        // Resetting the experiment and showing the demo again is one run: its screens light once, when the demo is back.
        var run = BeginRun();
        Induction.Reset();
        Induction.ShowDemoCommand.Execute(null);   // the measuring runs in the background
        run.Dispose();
        _refreshing = true;
        Seed = 1;
        Layer = 1;
        Head = 1;
        _refreshing = false;
        Text = DefaultFor(_models.Current);
        _lastDefault = Text;
        Refresh();
    }

    public override void OnActiveModelChanged()
    {
        // A text the user typed stays; the untouched starting text follows the model (letters for the letters-only demo).
        if (Text == _lastDefault) { _lastDefault = DefaultFor(_models.Current); Text = _lastDefault; }
        Refresh();
    }

    public override void Deactivate() => Induction.Deactivate();

    private static string DefaultFor(LanguageModel model) =>
        model.Tokenizer.Encode(DefaultText).Contains(model.Tokenizer.UnknownId) ? DefaultLetters : DefaultText;

    private void Refresh()
    {
        if (_refreshing) return;
        Show();
        ReportScreen("real-heads", new object?[] { Message, Notice, RealCaption, RealWeights, TokenLabels.ToArray() });
    }

    // Snapshots: the lists are replaced, never edited, but the induction numbers can arrive from the training thread.
    private void ReportToy() =>
        ReportScreen("toy", new object[] { Toy.Rows.ToArray(), Toy.ScaleText, Toy.SoftmaxText, Toy.AnswerText });

    private void ReportInduction()
    {
        var i = Induction;
        ReportScreen("sequence", new object[] { i.SequenceCells.ToArray(), i.CopySpans.ToArray() });
        ReportScreen("scores", new object[] { i.ScoreSummary, i.HasModel, i.InductionBars.ToArray(), i.PreviousTokenBars.ToArray() });
        ReportScreen("heat", new object?[] { i.HeatMap, i.OutlinedCells.ToArray(), i.TokenLabels.ToArray() });
        ReportScreen("loss", new object[] { i.ProgressText, i.LossSeries.SelectMany(s => s.Points).SelectMany(p => new[] { p.X, p.Y }).ToArray() });
    }

    private void Show()
    {
        var model = _models.Current;
        var config = model.Transformer.Config;
        LayerNumbers = Enumerable.Range(1, config.Layers).ToList();
        HeadNumbers = Enumerable.Range(1, config.Heads).ToList();
        if (config.Layers == 0)
        {
            Message = "This model has no attention layers, so there is nothing to show here.";
            RealWeights = null;
            Notice = null;
            ShownText = "";
            TokenLabels = [];
            return;
        }
        _refreshing = true;
        Layer = Math.Clamp(Layer, 1, config.Layers);
        Head = Math.Clamp(Head, 1, config.Heads);
        _refreshing = false;

        if (string.IsNullOrEmpty(Text))
        {
            Message = LabText.EmptyPrompt;
            Notice = null;
            ShownText = "";
            RealWeights = null;
            TokenLabels = [];
            RealCaption = "";
            return;
        }
        Message = null;
        var prediction = model.Predict(Text);
        Notice = prediction.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        var ids = prediction.UsedTokenIds;
        ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(ids));
        TokenLabels = ids.Select((id, i) => $"{i + 1}:{LabText.Visible(model.Tokenizer.TokenText(id))}").ToList();
        var weights = prediction.Forward.Trace.Blocks[Layer - 1].Heads[Head - 1].Weights;
        RealWeights = InductionPanelViewModel.Grid(weights);
        RealCaption = $"Layer {Layer}, head {Head} of {config.Layers} layers with {config.Heads} heads each.";
    }
}
