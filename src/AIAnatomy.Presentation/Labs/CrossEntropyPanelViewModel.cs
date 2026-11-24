using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// Part (d) of the learning lab: for each place in the typed text, how much probability the active model gave to the
/// character that really came next (p), and the loss −ln p. Probabilities come from <see cref="Sampling.Probabilities"/>
/// at temperature 1; the mean of the losses is what training lowers.
/// </summary>
public sealed partial class CrossEntropyPanelViewModel : ObservableObject
{
    public const int BarCount = 10;
    public const string NeedTwo = "Type at least two characters: each place needs a next character to compare with.";

    public const string LossNote =
        "The loss at one place is −ln p, where p is the probability the model gave to the character that really came next. " +
        "If p is 1 the loss is 0; the smaller p is, the bigger the loss. Training adjusts the model's numbers to lower the mean of these losses over lots of text.";

    private readonly IActiveModelService _models;
    private string _lastDefault;
    private bool _syncing;
    private double[][] _probabilities = [];
    private IReadOnlyList<int> _ids = [];

    public CrossEntropyPanelViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _lastDefault = text = LabText.StartingText(models.Current);
        Refresh(moveToEnd: true);
    }

    [ObservableProperty] private string text;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    /// <summary>One entry for each place that has a next character, numbered from 1 ("1:t" is the place after the first token).</summary>
    [ObservableProperty] private IReadOnlyList<string> positionChoices = [];
    /// <summary>0-based into <see cref="PositionChoices"/>.</summary>
    [ObservableProperty] private int positionIndex;
    [ObservableProperty] private string contextText = "";
    [ObservableProperty] private string actualText = "";
    [ObservableProperty] private double probability;
    [ObservableProperty] private double loss;
    [ObservableProperty] private string probabilityText = "";
    [ObservableProperty] private string lossText = "";
    [ObservableProperty] private string formulaText = "";
    [ObservableProperty] private IReadOnlyList<LabeledValue> bars = [];
    [ObservableProperty] private string? highlightedBar;
    [ObservableProperty] private string barsCaption = "";
    /// <summary>The loss at every place, in order.</summary>
    [ObservableProperty] private double[]? losses;
    [ObservableProperty] private string lossKey = "";
    [ObservableProperty] private double meanLoss;
    [ObservableProperty] private string meanText = "";

    /// <summary>Raised at the end of each recalculation or choice of place, once every value the part shows is current, so the lab can report its screen.</summary>
    public event Action? Recalculated;

    partial void OnTextChanged(string value) { if (!_syncing) Refresh(moveToEnd: true); }

    partial void OnPositionIndexChanged(int value)
    {
        if (_syncing) return;
        Select();
        Recalculated?.Invoke();
    }

    public void Reset()
    {
        _lastDefault = LabText.StartingText(_models.Current);
        if (Text == _lastDefault) Refresh(moveToEnd: true); else Text = _lastDefault;
    }

    public void OnActiveModelChanged()
    {
        if (Text == _lastDefault)
        {
            _lastDefault = LabText.StartingText(_models.Current);
            _syncing = true;
            Text = _lastDefault;
            _syncing = false;
        }
        Refresh(moveToEnd: true);
    }

    private void Refresh(bool moveToEnd)
    {
        var model = _models.Current;
        var config = model.Transformer.Config;
        _probabilities = [];
        _ids = [];
        Losses = null;
        MeanLoss = 0;
        MeanText = LossKey = ContextText = ActualText = ProbabilityText = LossText = FormulaText = BarsCaption = "";
        Probability = Loss = 0;
        Bars = [];
        HighlightedBar = null;
        Notice = null;
        ShownText = "";
        if (string.IsNullOrEmpty(Text) || LabText.CountScalars(Text) < 2)
        {
            Message = string.IsNullOrEmpty(Text) ? LabText.EmptyPrompt : NeedTwo;
            SetChoices([]);
            Recalculated?.Invoke();
            return;
        }
        Message = null;
        var prediction = model.Predict(Text);
        Notice = prediction.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        var ids = _ids = prediction.UsedTokenIds;
        ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(ids));
        var logits = prediction.Forward.Logits;
        int places = ids.Count - 1;
        var plain = new SamplingSettings(1.0);
        _probabilities = Enumerable.Range(0, places).Select(i => Sampling.Probabilities(logits.Row(i), plain)).ToArray();
        var losses = Enumerable.Range(0, places).Select(i => LossOf(_probabilities[i][ids[i + 1]])).ToArray();
        Losses = losses;
        MeanLoss = losses.Average();
        MeanText = $"Mean loss over your {places} {(places == 1 ? "place" : "places")}: {NumberText.Short(MeanLoss)}. This mean is the number training tries to lower.";
        LossKey = string.Join("   ", Enumerable.Range(0, places).Select(i => $"{i + 1}:{Name(model, ids[i])}"));
        SetChoices(Enumerable.Range(0, places).Select(i => $"{i + 1}:{Name(model, ids[i])}").ToList());

        int position = moveToEnd ? places - 1 : Math.Clamp(PositionIndex, 0, places - 1);
        if (position != PositionIndex) { _syncing = true; PositionIndex = position; _syncing = false; }
        Select();
        Recalculated?.Invoke();
    }

    private void SetChoices(IReadOnlyList<string> choices)
    {
        // The list is replaced only when its entries change, so a drop-down being used is not reset.
        if (!PositionChoices.SequenceEqual(choices)) PositionChoices = choices;
    }

    private void Select()
    {
        if (_probabilities.Length == 0) return;
        var model = _models.Current;
        int i = Math.Clamp(PositionIndex, 0, _probabilities.Length - 1);
        var p = _probabilities[i];
        int actual = _ids[i + 1];
        Probability = p[actual];
        Loss = LossOf(Probability);
        string before = string.Concat(_ids.Take(i + 1).Select(id => Name(model, id)));
        ContextText = $"After place {i + 1} ({ActualOf(model, i)}), the text so far is: {before}";
        ActualText = Name(model, actual);
        ProbabilityText = NumberText.Percent(Probability);
        LossText = NumberText.Short(Loss);
        FormulaText = $"p = {NumberText.Short(Probability)}, so the loss is −ln p = −ln({NumberText.Short(Probability)}) = {LossText}";

        var order = Enumerable.Range(0, p.Length).OrderByDescending(k => p[k]).ThenBy(k => k).ToList();
        var shown = order.Take(BarCount).ToList();
        if (!shown.Contains(actual)) shown.Add(actual);
        Bars = shown.Select(k => new LabeledValue(Name(model, k), p[k])).ToList();
        HighlightedBar = ActualText;
        int rank = order.IndexOf(actual) + 1;
        BarsCaption = $"The model's {BarCount} most likely next characters" + (shown.Count > BarCount ? ", and the real one below them" : "") +
                      $". The bar marked “chosen” is the character that really came next; it was the model's number {rank} choice.";
    }

    private string ActualOf(LanguageModel model, int i) => Name(model, _ids[i]);

    private static double LossOf(double p) => p > 0 ? -Math.Log(p) : double.PositiveInfinity;

    private static string Name(LanguageModel model, int id) => LabText.Visible(model.Tokenizer.TokenText(id));
}
