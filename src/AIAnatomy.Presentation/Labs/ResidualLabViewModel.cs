using AIAnatomy.Core;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>The length of the residual vector after one stage.</summary>
public sealed record SizeRow(string Label, double Value)
{
    public string ValueText => NumberText.Short(Value);
}

/// <summary>One of the five best guesses at one stage of the logit lens.</summary>
public sealed record LensGuess(string Token, double Probability, string PercentText)
{
    public string Text => $"{Token}  {PercentText}";
}

/// <summary>One row of the logit-lens table: a stage of the model and its five best guesses.</summary>
public sealed record LensRow(string Label, IReadOnlyList<LensGuess> Guesses)
{
    public string Description => $"{Label}: " + string.Join(", ", Guesses.Select(g => g.Text));
}

/// <summary>
/// The residual stream of one token: how long its vector is after each stage, what the model would guess if it stopped
/// at each stage (the logit lens), and what layer normalisation does to a vector.
/// </summary>
public sealed partial class ResidualLabViewModel : LabViewModel
{
    public const string Id = "ResidualLab";
    private const int TopCount = 5;

    public const string SizeNote =
        "Size means the length of the vector (square root of the sum of squares), measured at the token you chose, after each stage. \"After the embeddings\" means the token's vector plus its position vector, before block 1.";

    public const string LensNote =
        "This technique is called the “logit lens” (nostalgebraist, 2020). Each row reads the residual stream at that stage as if it were the last one: " +
        "it goes through the final layer norm and the output layer, and the result becomes five best guesses. " +
        "The guesses of the early rows are only a rough reading, because the final layer norm and the output layer were trained only on the last layer's output. " +
        "The last row is exactly the model's own prediction.";

    private readonly IActiveModelService _models;
    private string _lastDefault;
    private bool _syncing;
    private bool _moveToEnd = true;

    public ResidualLabViewModel(IActiveModelService models)
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
    /// <summary>0-based position of the token that is followed through the model.</summary>
    [ObservableProperty] private int positionIndex;
    [ObservableProperty] private IReadOnlyList<string> positionChoices = [];

    [ObservableProperty] private IReadOnlyList<SizeRow> sizes = [];
    [ObservableProperty] private double sizeMaximum = 1;
    [ObservableProperty] private IReadOnlyList<LensRow> lensRows = [];
    [ObservableProperty] private string lensCaption = "";

    /// <summary>1-based: 1 to N are the layer norms at the start of each block; N + 1 is the final layer norm.</summary>
    [ObservableProperty] private int normStage = 1;
    [ObservableProperty] private IReadOnlyList<int> normStageNumbers = [];
    [ObservableProperty] private string normStageName = "";
    [ObservableProperty] private double[]? normBefore;
    [ObservableProperty] private double[]? normAfter;
    [ObservableProperty] private double[]? normScaled;
    [ObservableProperty] private double normBeforeMean;
    [ObservableProperty] private double normBeforeSpread;
    [ObservableProperty] private double normAfterMean;
    [ObservableProperty] private double normAfterSpread;
    [ObservableProperty] private double normScaledMean;
    [ObservableProperty] private double normScaledSpread;
    [ObservableProperty] private string normBeforeText = "";
    [ObservableProperty] private string normAfterText = "";
    [ObservableProperty] private string normScaledText = "";

    public const string NormNote =
        "Layer norm moves a vector so its numbers average 0 and have a spread of 1 (the spread is the standard deviation). " +
        "Then gamma and beta, two lists of learned numbers (one pair for each place in the vector), rescale and shift it: each number is multiplied by its gamma and its beta is added.";

    partial void OnTextChanged(string value) { _moveToEnd = true; Refresh(); }
    partial void OnPositionIndexChanged(int value) => Refresh();
    partial void OnNormStageChanged(int value) => Refresh();

    public override void Reset()
    {
        _syncing = true;
        NormStage = 1;
        _syncing = false;
        _lastDefault = LabText.StartingText(_models.Current);
        _moveToEnd = true;
        if (Text == _lastDefault) Refresh(); else Text = _lastDefault;
    }

    public override void OnActiveModelChanged()
    {
        if (Text == _lastDefault) { _lastDefault = LabText.StartingText(_models.Current); Text = _lastDefault; }
        _moveToEnd = true;
        Refresh();
    }

    private void Refresh()
    {
        if (_syncing) return;
        var model = _models.Current;
        var config = model.Transformer.Config;
        if (NormStageNumbers.Count != config.Layers + 1) NormStageNumbers = Enumerable.Range(1, config.Layers + 1).ToList();
        _syncing = true;
        NormStage = Math.Clamp(NormStage, 1, config.Layers + 1);
        _syncing = false;

        if (string.IsNullOrEmpty(Text))
        {
            Message = LabText.EmptyPrompt;
            Notice = null;
            ShownText = LensCaption = NormStageName = "";
            PositionChoices = [];
            Sizes = [];
            LensRows = [];
            NormBefore = NormAfter = NormScaled = null;
            NormBeforeText = NormAfterText = NormScaledText = "";
            return;
        }
        Message = null;
        var prediction = model.Predict(Text);
        Notice = prediction.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        var ids = prediction.UsedTokenIds;
        ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(ids));
        var choices = ids.Select((id, i) => $"{i + 1}:{LabText.Visible(model.Tokenizer.TokenText(id))}").ToList();
        if (!PositionChoices.SequenceEqual(choices)) PositionChoices = choices;
        int position = _moveToEnd ? ids.Count - 1 : Math.Clamp(PositionIndex, 0, ids.Count - 1);
        _moveToEnd = false;
        if (position != PositionIndex) { _syncing = true; PositionIndex = position; _syncing = false; }
        var trace = prediction.Forward.Trace;

        // Stage 0 is the embeddings, stage i is after block i.
        var stages = new List<(string Label, double[] Vector)> { ("after the embeddings", trace.Input.Row(position)) };
        for (int i = 0; i < trace.Blocks.Count; i++) stages.Add(($"after block {i + 1}", trace.Blocks[i].ResidualOut.Row(position)));
        var sizes = stages.Select(s => new SizeRow(s.Label, Length(s.Vector))).ToList();
        Sizes = sizes;
        SizeMaximum = Math.Max(1e-12, sizes.Max(s => s.Value));

        var lens = LogitLens.PerLayerProbabilities(model.Transformer, trace, position);
        LensRows = lens.Select((p, i) => new LensRow(stages[i].Label, Top(model, p))).ToList();
        LensCaption = $"What the model would guess comes after token {PositionChoices[position]}, if it stopped at each stage.";

        ShowNorm(model, trace, position);
    }

    /// <summary>
    /// Layer norm of one stage: the vector before it, after it (mean 0, spread 1), and after the learned gamma and beta.
    /// The last version is read from the trace, so it is exactly what the model used.
    /// </summary>
    private void ShowNorm(LanguageModel model, ForwardTrace trace, int position)
    {
        int layers = trace.Blocks.Count;
        double[] before, scaled;
        if (NormStage <= layers)
        {
            var block = trace.Blocks[NormStage - 1];
            before = block.ResidualIn.Row(position);
            scaled = block.Norm1Out.Row(position);
            NormStageName = $"the layer norm at the start of block {NormStage}, before attention";
        }
        else
        {
            before = (layers == 0 ? trace.Input : trace.Blocks[^1].ResidualOut).Row(position);
            scaled = trace.FinalNorm.Row(position);
            NormStageName = "the final layer norm, before the output layer";
        }
        int d = before.Length;
        var plain = Ops.LayerNormRows(Tensor.FromArray((double[])before.Clone(), 1, d),
            Tensor.FromArray(Enumerable.Repeat(1.0, d).ToArray(), d), Tensor.Zeros(d)).Row(0);
        NormBefore = before;
        NormAfter = plain;
        NormScaled = scaled;
        (NormBeforeMean, NormBeforeSpread) = MeanAndSpread(before);
        (NormAfterMean, NormAfterSpread) = MeanAndSpread(plain);
        (NormScaledMean, NormScaledSpread) = MeanAndSpread(scaled);
        NormBeforeText = $"Before: mean {Mean(NormBeforeMean)}, spread {NumberText.Short(NormBeforeSpread)}";
        NormAfterText = $"After layer norm: mean {Mean(NormAfterMean)}, spread {NumberText.Short(NormAfterSpread)}";
        NormScaledText = $"After gamma and beta (what the model uses): mean {Mean(NormScaledMean)}, spread {NumberText.Short(NormScaledSpread)}";
    }

    /// <summary>A mean this close to zero is rounding error, not a number worth reading.</summary>
    private static string Mean(double mean) => Math.Abs(mean) < 1e-9 ? "0 (to rounding)" : NumberText.Short(mean);

    /// <summary>Mean and standard deviation (the square root of the average squared distance from the mean).</summary>
    private static (double Mean, double Spread) MeanAndSpread(double[] v)
    {
        double mean = v.Average();
        return (mean, Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / v.Length));
    }

    private static double Length(double[] v) => Math.Sqrt(v.Sum(x => x * x));

    private static IReadOnlyList<LensGuess> Top(LanguageModel model, double[] p) =>
        Enumerable.Range(0, p.Length).OrderByDescending(i => p[i]).ThenBy(i => i).Take(TopCount)
            .Select(i => new LensGuess(LabText.Visible(model.Tokenizer.TokenText(i)), p[i], NumberText.Percent(p[i]))).ToList();
}
