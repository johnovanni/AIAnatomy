using System.Text;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One of a neuron's strongest moments: the text just before, and the character it had just read (shown in bold, in a box).</summary>
public sealed record ContextRow(int Rank, double Value, string ValueText, string Before, string Own)
{
    public string Description => $"Number {Rank}, activation {ValueText}: {Before}{Own}, the last character is {Own}";
}

/// <summary>
/// One feed-forward neuron: how strongly it fires along your text, and the ten places in the first 20,000 characters of the
/// fables where it fired most. The search runs in the background and is remembered per model, layer and neuron.
/// </summary>
public sealed partial class FeedForwardLabViewModel : LabViewModel
{
    public const string Id = "FeedForwardLab";
    public const int ProbeCharacters = 20000;
    public const int ContextCount = 10;

    public const string NoFeedForwardNote = "This model has no feed-forward layers (it is attention-only).";

    public const string ResearchNote =
        "Research suggests that feed-forward layers act partly like a store of patterns the model has learned (Geva et al., 2021). " +
        "This is an active research area, not settled. A single neuron often does not have one tidy meaning.";

    private readonly IActiveModelService _models;
    private readonly Func<string> _fablesText;
    private readonly Dictionary<(LanguageModel Model, int Layer, int Neuron), IReadOnlyList<ContextRow>> _cache = [];
    private CancellationTokenSource? _search;
    private int _generation;
    private Task _background = Task.CompletedTask;
    private string _lastDefault;
    private bool _syncing;

    public FeedForwardLabViewModel(IActiveModelService models, Func<string> fablesText)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _fablesText = fablesText ?? throw new ArgumentNullException(nameof(fablesText));
        _lastDefault = text = LabText.StartingText(models.Current);
        Refresh();
        StartSearch();
    }

    public override string LabId => Id;
    public override Task WorkCompleted => _background;

    [ObservableProperty] private string text;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    [ObservableProperty] private bool hasFeedForward = true;
    /// <summary>1-based.</summary>
    [ObservableProperty] private int layer = 1;
    /// <summary>1-based.</summary>
    [ObservableProperty] private int neuron = 1;
    [ObservableProperty] private IReadOnlyList<int> layerNumbers = [];
    [ObservableProperty] private IReadOnlyList<int> neuronNumbers = [];
    /// <summary>The neuron's activation at each token of the typed text.</summary>
    [ObservableProperty] private double[]? activations;
    [ObservableProperty] private string tokenKey = "";
    [ObservableProperty] private string activationCaption = "";
    [ObservableProperty] private IReadOnlyList<ContextRow> contexts = [];
    [ObservableProperty] private string contextsStatus = "";

    partial void OnTextChanged(string value) => Refresh();

    partial void OnLayerChanged(int value) { if (_syncing) return; Refresh(); StartSearch(); }

    partial void OnNeuronChanged(int value) { if (_syncing) return; Refresh(); StartSearch(); }

    public override void Reset()
    {
        _syncing = true;
        Layer = 1;
        Neuron = 1;
        _syncing = false;
        _lastDefault = LabText.StartingText(_models.Current);
        if (Text == _lastDefault) Refresh(); else Text = _lastDefault;
        StartSearch();
    }

    public override void OnActiveModelChanged()
    {
        if (Text == _lastDefault) { _lastDefault = LabText.StartingText(_models.Current); Text = _lastDefault; }
        Refresh();
        StartSearch();
    }

    public override void Deactivate()
    {
        _search?.Cancel();
        _generation++;
    }

    private void Refresh()
    {
        var model = _models.Current;
        var config = model.Transformer.Config;
        HasFeedForward = config.UseFeedForward && config.Layers > 0;
        if (!HasFeedForward)
        {
            Message = NoFeedForwardNote;
            Notice = null;
            ShownText = TokenKey = ActivationCaption = "";
            Activations = null;
            LayerNumbers = NeuronNumbers = [];
            return;
        }
        if (LayerNumbers.Count != config.Layers) LayerNumbers = Enumerable.Range(1, config.Layers).ToList();
        if (NeuronNumbers.Count != config.FeedForwardWidth) NeuronNumbers = Enumerable.Range(1, config.FeedForwardWidth).ToList();
        _syncing = true;
        Layer = Math.Clamp(Layer, 1, config.Layers);
        Neuron = Math.Clamp(Neuron, 1, config.FeedForwardWidth);
        _syncing = false;

        if (string.IsNullOrEmpty(Text))
        {
            Message = LabText.EmptyPrompt;
            Notice = null;
            ShownText = TokenKey = ActivationCaption = "";
            Activations = null;
            return;
        }
        Message = null;
        var prediction = model.Predict(Text);
        Notice = prediction.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        var ids = prediction.UsedTokenIds;
        ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(ids));
        TokenKey = string.Join("   ", ids.Select((id, i) => $"{i + 1}:{LabText.Visible(model.Tokenizer.TokenText(id))}"));
        var hidden = prediction.Forward.Trace.Blocks[Layer - 1].FeedForwardHidden!;
        int width = hidden.Shape[1];
        Activations = Enumerable.Range(0, ids.Count).Select(t => hidden.Data[t * width + Neuron - 1]).ToArray();
        ActivationCaption = $"Layer {Layer}, neuron {Neuron} of {config.FeedForwardWidth}: its activation at each character of your text.";
    }

    private void StartSearch()
    {
        _search?.Cancel();
        _search = null;
        int generation = ++_generation;
        var model = _models.Current;
        if (!HasFeedForward)
        {
            Contexts = [];
            ContextsStatus = "";
            _background = Task.CompletedTask;
            return;
        }
        var key = (model, Layer - 1, Neuron - 1);
        if (_cache.TryGetValue(key, out var cached))
        {
            Contexts = cached;
            ContextsStatus = Done(cached);
            _background = Task.CompletedTask;
            return;
        }
        Contexts = [];
        ContextsStatus = $"Searching the first {ProbeCharacters:N0} characters of the fables…";
        var cts = _search = new CancellationTokenSource();
        _background = SearchAsync(key, generation, cts);
    }

    private async Task SearchAsync((LanguageModel Model, int Layer, int Neuron) key, int generation, CancellationTokenSource cts)
    {
        try
        {
            var found = await Task.Run(() =>
            {
                string corpus = _fablesText();
                if (corpus.Length > ProbeCharacters) corpus = corpus[..ProbeCharacters];
                return NeuronProbe.TopContexts(key.Model, corpus, key.Layer, key.Neuron, ContextCount, cts.Token);
            }, cts.Token);
            var rows = found.Select((a, i) => ToRow(i + 1, a)).ToList();
            _cache[key] = rows;
            if (generation != _generation) return;
            Contexts = rows;
            ContextsStatus = Done(rows);
        }
        catch (OperationCanceledException) { /* a newer search or leaving the lab */ }
        catch (Exception e)
        {
            if (generation == _generation) ContextsStatus = "The search could not be done: " + e.Message;
        }
        finally
        {
            if (ReferenceEquals(_search, cts)) _search = null;
            cts.Dispose();
        }
    }

    private static string Done(IReadOnlyList<ContextRow> rows) =>
        rows.Count == 0 ? "No text to search." : $"The {rows.Count} places in the first {ProbeCharacters:N0} characters of the fables where this neuron fired most, strongest first.";

    private static ContextRow ToRow(int rank, NeuronActivation a)
    {
        var runes = a.Context.EnumerateRunes().ToList();
        string own = runes.Count == 0 ? "" : LabText.Visible(runes[^1].ToString());
        string before = LabText.Visible(string.Concat(runes.Take(Math.Max(0, runes.Count - 1)).Select(r => r.ToString())));
        return new ContextRow(rank, a.Value, NumberText.Short(a.Value), before, own);
    }
}
