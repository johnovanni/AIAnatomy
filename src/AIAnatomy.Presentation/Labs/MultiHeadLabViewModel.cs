using AIAnatomy.Models.Language;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One attention head of the chosen layer: its attention squares, its on/off box and its output at the chosen position.</summary>
public sealed partial class HeadPanelViewModel : ObservableObject
{
    private readonly Action<HeadPanelViewModel> _changed;

    public HeadPanelViewModel(int number, Action<HeadPanelViewModel> changed)
    {
        Number = number;
        _changed = changed;
    }

    /// <summary>1-based.</summary>
    public int Number { get; }

    /// <summary>The name of this head's screen in the lab (one screen per head, so switching one off lights only its own).</summary>
    public string ScreenName => $"head-{Number}";

    /// <summary>The check box: ticked means the head works; cleared switches it off (its output becomes zeros).</summary>
    [ObservableProperty] private bool isEnabled = true;
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string status = "";
    /// <summary>[T,T] attention weights; squares above the diagonal are not a number (blocked).</summary>
    [ObservableProperty] private double[,]? weights;
    /// <summary>This head's output at the chosen position (HeadWidth numbers).</summary>
    [ObservableProperty] private double[]? output;

    partial void OnIsEnabledChanged(bool value) => _changed(this);
}

/// <summary>
/// All the heads of one layer side by side, with a check box for each that switches it off; the top five guesses follow
/// at once. At a chosen position it shows each head's output, the heads joined together, and the result after the
/// output projection.
/// </summary>
public sealed partial class MultiHeadLabViewModel : LabViewModel
{
    public const string Id = "MultiHeadLab";
    private const int TopCount = 5;

    public const string JoinNote =
        "The heads' outputs are joined side by side into one long vector, then a learned matrix (the output projection) mixes them into a vector of the model's width. " +
        "That vector is what attention adds to the residual stream.";

    private readonly IActiveModelService _models;
    private readonly HashSet<(int Layer, int Head)> _disabled = [];   // 0-based
    private string _lastDefault;
    private bool _syncing;
    private bool _moveToEnd = true;

    public MultiHeadLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _lastDefault = text = LabText.StartingText(models.Current);
        Refresh();
    }

    public override string LabId => Id;

    [ObservableProperty] private string text;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    /// <summary>1-based.</summary>
    [ObservableProperty] private int layer = 1;
    [ObservableProperty] private IReadOnlyList<int> layerNumbers = [];
    /// <summary>0-based position of the token whose vectors are shown.</summary>
    [ObservableProperty] private int positionIndex;
    [ObservableProperty] private IReadOnlyList<string> positionChoices = [];
    [ObservableProperty] private IReadOnlyList<string> tokenLabels = [];
    /// <summary>One panel per head of the chosen layer. The list is rebuilt only when the number of heads changes.</summary>
    [ObservableProperty] private IReadOnlyList<HeadPanelViewModel> heads = [];
    [ObservableProperty] private string layerCaption = "";
    [ObservableProperty] private string disabledSummary = "";
    [ObservableProperty] private IReadOnlyList<LabeledValue> predictions = [];
    [ObservableProperty] private IReadOnlyList<LabeledValue> basePredictions = [];
    [ObservableProperty] private string predictionCaption = "";
    [ObservableProperty] private string vectorCaption = "";
    /// <summary>The heads' outputs joined side by side at the chosen position (ModelWidth numbers).</summary>
    [ObservableProperty] private double[]? concatenatedVector;
    /// <summary>The result of the output projection at the chosen position (ModelWidth numbers).</summary>
    [ObservableProperty] private double[]? projectedVector;

    partial void OnTextChanged(string value) { _moveToEnd = true; Refresh(); }
    partial void OnLayerChanged(int value) => Refresh();
    partial void OnPositionIndexChanged(int value) => Refresh();

    public IReadOnlySet<(int Layer, int Head)> DisabledHeads => _disabled;

    public override void Reset()
    {
        _disabled.Clear();
        _syncing = true;
        Layer = 1;
        _syncing = false;
        _lastDefault = LabText.StartingText(_models.Current);
        _moveToEnd = true;
        if (Text == _lastDefault) Refresh(); else Text = _lastDefault;
    }

    public override void OnActiveModelChanged()
    {
        _disabled.Clear();   // another model has other heads
        if (Text == _lastDefault) { _lastDefault = LabText.StartingText(_models.Current); Text = _lastDefault; }
        _moveToEnd = true;
        Refresh();
    }

    private void Refresh()
    {
        if (_syncing) return;
        Show();
        ReportScreens();
    }

    // Snapshots (the lists are replaced, never edited, but a fingerprint is taken now all the same).
    private void ReportScreens()
    {
        var labels = TokenLabels.ToArray();
        foreach (var head in Heads)
            ReportScreen(head.ScreenName, new object?[] { head.Title, head.Status, head.Weights, labels });
        ReportScreen("guesses", new object[] { PredictionCaption, Predictions.ToArray(), BasePredictions.ToArray() });
        ReportScreen("joined", new object?[] { VectorCaption, Heads.Select(h => h.Output).ToArray(), ConcatenatedVector, ProjectedVector });
    }

    private void Show()
    {
        var model = _models.Current;
        var config = model.Transformer.Config;
        LayerNumbers = Enumerable.Range(1, config.Layers).ToList();
        if (config.Layers == 0)
        {
            Clear("This model has no attention layers, so there is nothing to show here.");
            return;
        }
        _syncing = true;
        Layer = Math.Clamp(Layer, 1, config.Layers);
        _syncing = false;

        if (string.IsNullOrEmpty(Text))
        {
            Clear(LabText.EmptyPrompt);
            return;
        }
        Message = null;
        int l = Layer - 1;
        var options = new ForwardOptions { DisabledHeads = _disabled.ToHashSet() };
        var prediction = model.Predict(Text, options);
        var plain = _disabled.Count == 0 ? prediction : model.Predict(Text, ForwardOptions.None);
        Notice = prediction.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        var ids = prediction.UsedTokenIds;
        ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(ids));
        TokenLabels = ids.Select((id, i) => $"{i + 1}:{LabText.Visible(model.Tokenizer.TokenText(id))}").ToList();
        PositionChoices = TokenLabels;

        int position = _moveToEnd ? ids.Count - 1 : Math.Clamp(PositionIndex, 0, ids.Count - 1);
        _moveToEnd = false;
        if (position != PositionIndex) { _syncing = true; PositionIndex = position; _syncing = false; }

        var block = prediction.Forward.Trace.Blocks[l];
        EnsurePanels(config.Heads);
        _syncing = true;
        for (int h = 0; h < config.Heads; h++)
        {
            var panel = Heads[h];
            bool off = _disabled.Contains((l, h));
            panel.IsEnabled = !off;
            panel.Title = off ? $"Head {h + 1}: SWITCHED OFF" : $"Head {h + 1}: on";
            panel.Status = off
                ? "Switched off: this head's output is replaced by zeros. The squares show where it would have looked."
                : "Working normally.";
            panel.Weights = InductionPanelViewModel.Grid(block.Heads[h].Weights);
            panel.Output = block.Heads[h].Output.Row(position);
        }
        _syncing = false;

        LayerCaption = $"Layer {Layer} of {config.Layers}: {config.Heads} heads; each reads the whole {config.ModelWidth}-number vector and works with {config.HeadWidth} numbers of its own (query, key, value).";
        DisabledSummary = _disabled.Count == 0
            ? "No head is switched off."
            : "Switched off: " + string.Join(", ", _disabled.OrderBy(d => d.Layer).ThenBy(d => d.Head).Select(d => $"layer {d.Layer + 1} head {d.Head + 1}")) + ".";
        Predictions = Top(model, prediction);
        BasePredictions = Top(model, plain);
        PredictionCaption = _disabled.Count == 0
            ? "Top 5 guesses for the next character, with every head working."
            : "Top 5 guesses for the next character, with the heads named above switched off.";

        ConcatenatedVector = block.ConcatenatedHeads.Row(position);
        ProjectedVector = block.AttentionOut.Row(position);
        VectorCaption = $"At token {TokenLabels[position]}: {config.Heads} head outputs of {config.HeadWidth} numbers join into {config.ModelWidth} numbers; the projection gives {config.ModelWidth} numbers again.";
    }

    private void Clear(string message)
    {
        Message = message;
        Notice = null;
        ShownText = "";
        TokenLabels = PositionChoices = [];
        Heads = [];
        Predictions = BasePredictions = [];
        ConcatenatedVector = ProjectedVector = null;
        LayerCaption = DisabledSummary = PredictionCaption = VectorCaption = "";
    }

    /// <summary>Keeps the same panel objects while the head count is unchanged, so a focused check box keeps its focus.</summary>
    private void EnsurePanels(int count)
    {
        if (Heads.Count == count) return;
        Heads = Enumerable.Range(1, count).Select(n => new HeadPanelViewModel(n, HeadToggled)).ToList();
    }

    private void HeadToggled(HeadPanelViewModel panel)
    {
        if (_syncing) return;
        var key = (Layer - 1, panel.Number - 1);
        if (panel.IsEnabled) _disabled.Remove(key); else _disabled.Add(key);
        Refresh();
    }

    private static IReadOnlyList<LabeledValue> Top(LanguageModel model, PredictionResult result)
    {
        var logits = result.Forward.Logits;
        var p = Sampling.Probabilities(logits.Row(logits.Shape[0] - 1), new SamplingSettings(1.0));
        return Enumerable.Range(0, p.Length).OrderByDescending(i => p[i]).ThenBy(i => i).Take(TopCount)
            .Select(i => new LabeledValue(LabText.Visible(model.Tokenizer.TokenText(i)), p[i])).ToList();
    }
}
