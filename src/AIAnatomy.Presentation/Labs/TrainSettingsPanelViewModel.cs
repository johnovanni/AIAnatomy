using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One part of the model and how many learnable numbers it has.</summary>
public sealed record ParameterRow(string Part, long Count, string CountText)
{
    public string Description => $"{Part}: {CountText}";
}

/// <summary>Everything a press of Start needs, read from the form. <c>Continue</c> is the active model when carrying on from a copy of it.</summary>
public sealed record TrainingChoice(
    LanguageModel? Continue, bool OwnText, string Text, CharTokenizer Tokenizer, TransformerConfig Config,
    double LearningRate, int Steps, int Batch);

/// <summary>
/// The Train lab's form: the text, the model's shape (or carrying on from the active model) and the training settings, all
/// typed text read after every key, with plain messages for anything wrong and a live parameter count by part.
/// Large pasted text is read (tokenized) off the UI thread.
/// </summary>
public sealed partial class TrainSettingsPanelViewModel : ObservableObject
{
    public const int MaxLayers = 4, MaxHeads = 8, MinContext = 16, MaxContext = 128, MaxSteps = 100_000, MaxBatch = 64;
    public const double MaxLearningRate = 100;
    public const int DefaultWidth = 32;
    public const string DefaultLayers = "2", DefaultHeads = "4", DefaultContext = "64", DefaultLearningRate = "0.003",
        DefaultSteps = "600", DefaultBatch = "8";

    /// <summary>Pasted text longer than this is tokenized on a background thread.</summary>
    public const int ReadInBackgroundFrom = 50_000;
    public const string ReadingMessage = "Reading your text…";

    /// <summary>The order the parts are listed in (ruling R12), the same as the lessons describe them.</summary>
    public static IReadOnlyList<string> PartOrder { get; } =
        ["Token embeddings", "Positions", "Attention", "Feed-forward", "Layer norms", "Output layer"];

    public static IReadOnlyList<int> Widths { get; } = [16, 32, 48, 64, 96];

    private readonly IActiveModelService _models;
    private readonly Func<string> _fables;
    private readonly Lazy<TextAnalysis> _fablesAnalysis;
    private TextAnalysis? _own;
    private string? _readingText;
    private TransformerConfig? _config;
    private (TransformerConfig Config, IReadOnlyDictionary<string, long> Counts)? _countsCache;
    private bool _syncing;

    public TrainSettingsPanelViewModel(IActiveModelService models, Func<string> fables)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _fables = fables ?? throw new ArgumentNullException(nameof(fables));
        _fablesAnalysis = new Lazy<TextAnalysis>(() => Analyse(_fables()));
        Refresh();
    }

    // ---- the text ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseFables))]
    private bool useOwnText;
    [ObservableProperty] private string ownText = "";
    [ObservableProperty] private string? textProblem;
    [ObservableProperty] private string textSummary = "";
    /// <summary>Why a paste was refused (the text would have been too long), or null.</summary>
    [ObservableProperty] private string? pasteNote;

    // ---- the model ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FreshModel), nameof(CanEditShape), nameof(SeedHint))]
    private bool continueActive;
    [ObservableProperty] private bool canContinueActive;
    [ObservableProperty] private string continueNote = "";
    [ObservableProperty] private string feedForwardNote = "";
    [ObservableProperty] private int width = DefaultWidth;
    [ObservableProperty] private string layersText = DefaultLayers;
    [ObservableProperty] private string headsText = DefaultHeads;
    [ObservableProperty] private string contextText = DefaultContext;

    // ---- training ----
    [ObservableProperty] private string learningRateText = DefaultLearningRate;
    [ObservableProperty] private string stepsText = DefaultSteps;
    [ObservableProperty] private string batchText = DefaultBatch;
    [ObservableProperty] private string? settingsProblem;
    /// <summary>Why Start can't be pressed (shown in its place), or null.</summary>
    [ObservableProperty] private string? startProblem;
    [ObservableProperty] private IReadOnlyList<ParameterRow> parameterRows = [];
    [ObservableProperty] private string parameterTotalText = "";

    /// <summary>False while a run is going on or paused: the form is then locked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditShape))]
    private bool isEditable = true;

    /// <summary>The shape boxes are used only for a new model.</summary>
    public bool CanEditShape => IsEditable && !ContinueActive;

    /// <summary>The other choice of <see cref="UseOwnText"/> (for a pair of radio buttons).</summary>
    public bool UseFables
    {
        get => !UseOwnText;
        set => UseOwnText = !value;
    }

    /// <summary>The other choice of <see cref="ContinueActive"/>: a new model built from the boxes (the default).</summary>
    public bool FreshModel
    {
        get => !ContinueActive;
        set => ContinueActive = !value;
    }

    /// <summary>What the lab's seed is used for, which depends on where the weights start.</summary>
    public string SeedHint => ContinueActive
        ? "Picks the pieces of text used in each step and the samples (the weights start from the active model)"
        : "Picks the starting weights, the pieces of text used in each step and the samples";

    /// <summary>A background read of a large pasted text, or a completed task.</summary>
    public Task Reading { get; private set; } = Task.CompletedTask;

    partial void OnUseOwnTextChanged(bool value) => Refresh();
    partial void OnOwnTextChanged(string value)
    {
        PasteNote = null;
        Refresh();
    }
    partial void OnContinueActiveChanged(bool value) => Refresh();
    partial void OnWidthChanged(int value) => Refresh();
    partial void OnLayersTextChanged(string value) => Refresh();
    partial void OnHeadsTextChanged(string value) => Refresh();
    partial void OnContextTextChanged(string value) => Refresh();
    partial void OnLearningRateTextChanged(string value) => Refresh();
    partial void OnStepsTextChanged(string value) => Refresh();
    partial void OnBatchTextChanged(string value) => Refresh();

    /// <summary>The shape a press of Start would train (the active model's own when carrying on), or null when a box is wrong.</summary>
    public TransformerConfig? CurrentConfig() => _config;

    /// <summary>What Start would use, or null when there is a problem (see <see cref="StartProblem"/>).</summary>
    public TrainingChoice? Read()
    {
        if (StartProblem != null || _config is not { } config || Analysis() is not { } text) return null;
        var active = _models.Current;
        return new TrainingChoice(ContinueActive ? active : null, UseOwnText, text.Text,
            ContinueActive ? active.Tokenizer : text.Tokenizer, config,
            ReadDouble(LearningRateText)!.Value, ReadInt(StepsText, 1, MaxSteps)!.Value, ReadInt(BatchText, 1, MaxBatch)!.Value);
    }

    /// <summary>Called by the view when a paste would make the text longer than <see cref="CorpusValidator.MaxCharacters"/>: the paste is refused.</summary>
    public void RefusePaste(int resultingLength) =>
        PasteNote = "That text was not pasted. " + CorpusValidator.ProblemForLength(resultingLength, MinContext);

    public void Reset()
    {
        _syncing = true;
        UseOwnText = false;
        OwnText = "";
        ContinueActive = false;
        Width = DefaultWidth;
        LayersText = DefaultLayers;
        HeadsText = DefaultHeads;
        ContextText = DefaultContext;
        LearningRateText = DefaultLearningRate;
        StepsText = DefaultSteps;
        BatchText = DefaultBatch;
        PasteNote = null;
        _syncing = false;
        Refresh();
    }

    /// <summary>Re-reads every box (also called when the active model changes).</summary>
    public void Refresh()
    {
        if (_syncing) return;
        var active = _models.Current;
        var activeConfig = active.Transformer.Config;
        var text = Analysis();   // null while a large pasted text is being read

        if (text != null)
        {
            var known = active.Tokenizer.Vocabulary.ToHashSet();
            CanContinueActive = text.Tokenizer.Vocabulary.All(known.Contains);
            if (ContinueActive && !CanContinueActive)
            {
                _syncing = true;
                ContinueActive = false;
                _syncing = false;
            }
        }
        ContinueNote = text == null ? ""
            : CanContinueActive
                ? $"Carries on from a copy of “{active.Info.Name}” (width {activeConfig.ModelWidth}, {activeConfig.Layers} layers, " +
                  $"{activeConfig.Heads} heads, context {activeConfig.ContextLength}). The active model itself is not changed."
                : $"Not possible with this text: it has characters that “{active.Info.Name}” doesn't know.";
        FeedForwardNote = !ContinueActive
            ? $"The feed-forward layers are 4 times as wide as the model ({NumberText.Short(4 * Width)} numbers), as in GPT-2."
            : activeConfig.UseFeedForward
                ? $"The active model's feed-forward layers are {NumberText.Short(activeConfig.FeedForwardWidth)} numbers wide."
                : "The active model has no feed-forward layers.";

        var problems = new List<string>();
        int? layers = ReadInt(LayersText, 1, MaxLayers);
        int? heads = ReadInt(HeadsText, 1, MaxHeads);
        int? context = ReadInt(ContextText, MinContext, MaxContext);
        if (!ContinueActive)
        {
            if (layers == null) problems.Add($"The number of layers must be a whole number from 1 to {MaxLayers}.");
            if (heads == null) problems.Add($"The number of heads must be a whole number from 1 to {MaxHeads}.");
            else if (Width % heads != 0) problems.Add($"The number of heads must divide the width evenly: {Width} can't be split into {heads} heads.");
            if (context == null) problems.Add($"The context length must be a whole number from {MinContext} to {MaxContext}.");
        }
        double? rate = ReadDouble(LearningRateText);
        if (!(rate > 0 && rate <= MaxLearningRate))
            problems.Add($"The learning rate must be a number above 0 and no more than {MaxLearningRate:0}, such as 0.003.");
        if (ReadInt(StepsText, 1, MaxSteps) == null) problems.Add($"The number of steps must be a whole number from 1 to {MaxSteps:#,0}.");
        if (ReadInt(BatchText, 1, MaxBatch) == null) problems.Add($"The batch size must be a whole number from 1 to {MaxBatch}.");
        SettingsProblem = problems.Count == 0 ? null : string.Join("\n", problems);

        _config = ContinueActive ? activeConfig
            : text != null && layers is int l && heads is int h && context is int c && Width % h == 0
                ? new TransformerConfig(text.Tokenizer.VocabularySize, c, Width, l, h, 4 * Width)
                : null;

        // The text is checked by its length in tokens, as the model will read it ("\r\n" is one new line, an emoji one unknown token).
        TextProblem = UseOwnText && OwnText.Length == 0 ? LabText.EmptyPrompt
            : text == null ? ReadingMessage
            : _config is { } config ? CorpusValidator.ProblemForLength(text.TokenCount, config.ContextLength)
            : null;
        TextSummary = text == null || (UseOwnText && OwnText.Length == 0) ? ""
            : $"{NumberText.Short(text.TokenCount)} characters as the model reads them, {NumberText.Short(text.Tokenizer.VocabularySize)} different tokens (counting one for “unknown”).";
        var all = new[] { TextProblem, SettingsProblem }.Where(p => p != null).ToList();
        StartProblem = all.Count == 0 ? null : string.Join("\n", all);

        var counts = _config == null ? null : ContinueActive ? active.Transformer.ParameterCountsByPart() : CountsFor(_config);
        ParameterRows = counts == null ? [] : PartOrder.Select(p => new ParameterRow(p, counts[p], NumberText.Short(counts[p]))).ToList();
        ParameterTotalText = counts != null ? $"Total: {NumberText.Short(counts.Values.Sum())} learnable numbers (parameters)."
            : text == null ? "The number of parameters appears when your text has been read."
            : "The number of parameters appears when the shape is valid.";
    }

    // ---- the text, tokenized ----

    private sealed record TextAnalysis(string Text, CharTokenizer Tokenizer, int TokenCount);

    private static TextAnalysis Analyse(string text)
    {
        var tokenizer = CharTokenizer.FromText(text);
        return new TextAnalysis(text, tokenizer, tokenizer.Encode(text).Count);
    }

    /// <summary>The chosen text with its tokenizer and length in tokens; null while a large pasted text is still being read.</summary>
    private TextAnalysis? Analysis()
    {
        if (!UseOwnText) return _fablesAnalysis.Value;
        string text = OwnText;
        if (_own is { } own && ReferenceEquals(own.Text, text)) return own;
        if (text.Length < ReadInBackgroundFrom) return _own = Analyse(text);
        if (!ReferenceEquals(_readingText, text))
        {
            _readingText = text;
            Reading = ReadAsync(text);
        }
        return null;
    }

    private async Task ReadAsync(string text)
    {
        var result = await Task.Run(() => Analyse(text));
        if (!ReferenceEquals(text, OwnText)) return;   // the text changed meanwhile; its own read is on the way
        _own = result;
        Refresh();
    }

    /// <summary>The counts of a model of this shape, read from a freshly built one (the lab does not redo the arithmetic).</summary>
    private IReadOnlyDictionary<string, long> CountsFor(TransformerConfig config)
    {
        if (_countsCache is { } cache && cache.Config == config) return cache.Counts;
        var counts = new Transformer(config, new SeededRandom(0)).ParameterCountsByPart();
        _countsCache = (config, counts);
        return counts;
    }

    private static int? ReadInt(string? text, int min, int max) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= min && v <= max ? v : null;

    private static double? ReadDouble(string? text) =>
        double.TryParse(text?.Trim().Replace('−', '-'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v) ? v : null;
}
