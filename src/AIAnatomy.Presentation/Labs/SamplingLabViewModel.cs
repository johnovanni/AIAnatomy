using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One line of the score list: a character and the raw score (logit) the model gave it.</summary>
public sealed record ScoreRow(int Rank, string Token, double Score, string ScoreText, string ProbabilityText)
{
    public string Description => $"Number {Rank}, {Token}: score {ScoreText}, probability {ProbabilityText}";
}

/// <summary>How many of the 100 samples were this character.</summary>
public sealed record SampleCount(string Token, int Count)
{
    public string Description => $"{Token}: {Count}";
}

/// <summary>
/// Scores, probabilities and sampling for the text typed: the model's raw scores (logits) for the next character,
/// what temperature, top-k and top-p do to them, 100 draws with the lab's seed, and 200 characters written one draw at a time.
/// </summary>
public sealed partial class SamplingLabViewModel : LabViewModel
{
    public const string Id = "SamplingLab";
    public const int SampleSize = 100;
    public const int GenerateLength = 200;
    public const int ShownCount = 15;
    public const double DefaultTemperature = 1.0;
    public const double MaxTemperature = 2.0;
    public const int MaxTopK = 50;
    public const double MinTopP = 0.1;
    public const string OthersLabel = "others";

    public const string TemperatureNote =
        "Temperature divides the scores before softmax turns them into probabilities. Below 1 it makes the top choice even more likely; above 1 it spreads the probability out. " +
        "At temperature 0 the model always takes its top choice.";

    public const string SamplingNote =
        "Sampling is why the same question can get different answers: each answer is built from random draws, one character at a time. " +
        "The same seed gives the same draws every time; change the seed to get different ones.";

    public const string ConfidenceNote =
        "“Confident” means a high probability for the model's choice, not that the choice is correct. A confident-sounding choice can still be wrong, " +
        "because the probability measures how the model's training text continued, not what is true.";

    private readonly IActiveModelService _models;
    private CancellationTokenSource? _generating;
    private int _generation;
    private Task _background = Task.CompletedTask;
    private string _lastDefault;
    private bool _syncing;

    public SamplingLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _lastDefault = text = LabText.StartingText(models.Current);
        Refresh();
    }

    public override string LabId => Id;
    public override Task WorkCompleted => _background;

    [ObservableProperty] private string text;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    [ObservableProperty] private double temperature = DefaultTemperature;
    [ObservableProperty] private int topK;
    [ObservableProperty] private double topP = 1.0;
    [ObservableProperty] private string temperatureText = "1.00";
    [ObservableProperty] private string topKText = "off";
    [ObservableProperty] private string topPText = "1.00 (off)";
    /// <summary>The probability of every character in the vocabulary, after temperature, top-k and top-p (empty when there is no text).</summary>
    [ObservableProperty] private double[] probabilities = [];
    [ObservableProperty] private IReadOnlyList<ScoreRow> scores = [];
    /// <summary>The most likely characters, as bars.</summary>
    [ObservableProperty] private IReadOnlyList<LabeledValue> bars = [];
    [ObservableProperty] private string barsCaption = "";
    /// <summary>The value a full bar stands for, the same for the probability chart and the frequency chart so they can be compared.</summary>
    [ObservableProperty] private double barScaleMaximum = 1;
    [ObservableProperty] private string barScaleCaption = "";
    [ObservableProperty] private string topChoiceText = "";

    [ObservableProperty] private IReadOnlyList<SampleCount> sampleCounts = [];
    [ObservableProperty] private IReadOnlyList<LabeledValue> frequencies = [];
    [ObservableProperty] private int sampleTotal;
    [ObservableProperty] private string sampleCaption = "";

    [ObservableProperty] private string generated = "";
    [ObservableProperty] private string generateStatus = "";

    partial void OnTextChanged(string value) { if (_syncing) return; Refresh(); }
    partial void OnTemperatureChanged(double value) { if (_syncing) return; Refresh(); }
    partial void OnTopKChanged(int value) { if (_syncing) return; Refresh(); }
    partial void OnTopPChanged(double value) { if (_syncing) return; Refresh(); }

    protected override void OnSeedEdited() { if (!_syncing) ClearResults(); }

    public SamplingSettings Settings => new(Math.Clamp(double.IsNaN(Temperature) ? DefaultTemperature : Temperature, 0, MaxTemperature),
        Math.Clamp(TopK, 0, MaxTopK), Math.Clamp(double.IsNaN(TopP) ? 1 : TopP, MinTopP, 1));

    public override void Reset()
    {
        _syncing = true;
        Temperature = DefaultTemperature;
        TopK = 0;
        TopP = 1.0;
        Seed = 1;
        Text = _lastDefault = LabText.StartingText(_models.Current);
        _syncing = false;
        Refresh();
    }

    public override void OnActiveModelChanged()
    {
        if (Text == _lastDefault)
        {
            _lastDefault = LabText.StartingText(_models.Current);
            _syncing = true;
            Text = _lastDefault;
            _syncing = false;
        }
        Refresh();
    }

    public override void Deactivate()
    {
        _generating?.Cancel();
        _generation++;
    }

    /// <summary>Draws 100 characters from the current probabilities, using the seed, and counts them.</summary>
    [RelayCommand]
    private void SampleHundred()
    {
        if (Probabilities.Length == 0) return;
        var model = _models.Current;
        var rng = new SeededRandom(Seed);
        var counts = new int[Probabilities.Length];
        for (int i = 0; i < SampleSize; i++) counts[Sampling.Sample(Probabilities, rng)]++;
        var seen = Enumerable.Range(0, counts.Length).Where(i => counts[i] > 0).OrderByDescending(i => counts[i]).ThenBy(i => i).ToList();
        SampleCounts = seen.Select(i => new SampleCount(Name(model, i), counts[i])).ToList();
        SampleTotal = counts.Sum();
        // The same characters in the same order as the probability bars, plus "others", so the rows line up.
        var shown = _shownIds;
        var rows = shown.Select(i => new LabeledValue(Name(model, i), (double)counts[i] / SampleSize)).ToList();
        int other = counts.Sum() - shown.Sum(i => counts[i]);
        if (shown.Count < counts.Length) rows.Add(new LabeledValue(OthersLabel, (double)other / SampleSize));
        Frequencies = rows;
        UpdateScale();
        SampleCaption = $"{SampleTotal} draws with seed {Seed}. Each bar is the share of the draws that gave that character, so 31% means 31 of the 100. " +
            $"The rows are the same characters as in the probability chart; “{OthersLabel}” adds up all the rest.";
    }

    /// <summary>Writes 200 characters after the typed text, one random draw at a time, using the seed.</summary>
    [RelayCommand]
    private Task GenerateText()
    {
        if (Probabilities.Length == 0) return Task.CompletedTask;
        _generating?.Cancel();
        int generation = ++_generation;
        var cts = _generating = new CancellationTokenSource();
        var model = _models.Current;
        string prompt = Text;
        var settings = Settings;
        int seed = Seed;
        Generated = "";
        GenerateStatus = $"Writing {GenerateLength} characters…";
        return _background = RunGenerate(model, prompt, settings, seed, generation, cts);
    }

    private async Task RunGenerate(LanguageModel model, string prompt, SamplingSettings settings, int seed, int generation, CancellationTokenSource cts)
    {
        try
        {
            string result = await Task.Run(() => Sampling.Generate(model, prompt, GenerateLength, settings, new SeededRandom(seed)), cts.Token);
            if (generation != _generation) return;
            Generated = result;
            GenerateStatus = $"{GenerateLength} new characters, written with seed {seed}. The same text, seed and settings always give the same writing.";
        }
        catch (OperationCanceledException) { /* a newer request, or leaving the lab */ }
        catch (Exception)
        {
            if (generation == _generation) GenerateStatus = "The writing could not be done. Please try again.";
        }
        finally
        {
            if (ReferenceEquals(_generating, cts)) _generating = null;
            cts.Dispose();
        }
    }

    private void ClearResults()
    {
        _generating?.Cancel();
        _generation++;
        SampleCounts = [];
        Frequencies = [];
        UpdateScale();
        SampleTotal = 0;
        SampleCaption = "";
        Generated = "";
        GenerateStatus = "";
    }

    private void Refresh()
    {
        ClearResults();
        TemperatureText = NumberText.ShortWithDecimals(Temperature, 2);
        TopKText = TopK == 0 ? "off" : TopK.ToString();
        TopPText = TopP >= 1 ? "1.00 (off)" : NumberText.ShortWithDecimals(TopP, 2);

        var model = _models.Current;
        var config = model.Transformer.Config;
        if (string.IsNullOrEmpty(Text))
        {
            Message = LabText.EmptyPrompt;
            Notice = null;
            ShownText = BarsCaption = TopChoiceText = "";
            Probabilities = [];
            Scores = [];
            Bars = [];
            _shownIds = [];
            UpdateScale();
            return;
        }
        Message = null;
        var prediction = model.Predict(Text);
        Notice = prediction.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        ShownText = LabText.Visible(model.Tokenizer.Decode(prediction.UsedTokenIds));
        var logits = prediction.Forward.Logits;
        var last = logits.Row(logits.Shape[0] - 1);
        var p = Probabilities = Sampling.Probabilities(last, Settings);

        // Highest score first (ties keep the lower index), the same order Sampling uses, so scores and bars line up.
        var order = Enumerable.Range(0, last.Length).OrderByDescending(i => last[i]).ThenBy(i => i).Take(ShownCount).ToList();
        Scores = order.Select((i, rank) => new ScoreRow(rank + 1, Name(model, i), last[i], NumberText.Short(last[i]), NumberText.Percent(p[i]))).ToList();
        _shownIds = order;
        var bars = order.Select(i => new LabeledValue(Name(model, i), p[i])).ToList();
        if (order.Count < p.Length) bars.Add(new LabeledValue(OthersLabel, Math.Max(0, 1 - order.Sum(i => p[i]))));
        Bars = bars;
        UpdateScale();
        int nonZero = p.Count(v => v > 0);
        BarsCaption = $"The {order.Count} highest-scoring characters of {last.Length}, and “{OthersLabel}” for all the rest. {nonZero} {(nonZero == 1 ? "character has" : "characters have")} a probability above 0 after your settings.";
        int top = Array.IndexOf(p, p.Max());
        TopChoiceText = $"Most likely next character: {Name(model, top)} at {NumberText.Percent(p[top])}.";
    }

    private IReadOnlyList<int> _shownIds = [];

    /// <summary>Both charts get one scale: the largest bar in either, rounded up to a nice number.</summary>
    private void UpdateScale()
    {
        double max = Bars.Concat(Frequencies).Select(b => b.Value).DefaultIfEmpty(0).Max();
        BarScaleMaximum = AxisTicks.NiceCeiling(max);
        BarScaleCaption = Bars.Count == 0 ? "" : $"Both charts use the same scale: a full bar is {NumberText.Percent(BarScaleMaximum)}.";
    }

    private static string Name(LanguageModel model, int id) => LabText.Visible(model.Tokenizer.TokenText(id));
}
