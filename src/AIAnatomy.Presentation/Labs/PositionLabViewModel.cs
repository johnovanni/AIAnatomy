using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>The model's position vectors beside the sine/cosine scheme, and its predictions with and without position information.</summary>
public sealed partial class PositionLabViewModel : LabViewModel
{
    public const string Id = "PositionLab";
    public const string DefaultText = "The fox and the ";
    private const int TopCount = 5;

    /// <summary>On-screen wording, accurate for models of any depth (a causal model with 2 or more layers is not fully order-blind).</summary>
    public const string OrderNote =
        "Removing position information changes the predictions and takes away the model's direct signal of order. " +
        "Some order information can still leak through the causal mask in deeper models, so a model with two or more layers " +
        "is not completely blind to order even without positions.";

    private readonly IActiveModelService _models;

    public PositionLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        Refresh();
    }

    public override string LabId => Id;

    [ObservableProperty] private string text = DefaultText;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    [ObservableProperty] private double[,]? modelPositions;
    [ObservableProperty] private double[,]? sinusoidalPositions;
    [ObservableProperty] private string modelPositionsTitle = "";
    [ObservableProperty] private IReadOnlyList<string> positionLabels = [];
    [ObservableProperty] private IReadOnlyList<string> vectorLabels = [];
    [ObservableProperty] private IReadOnlyList<LabeledValue> withPositions = [];
    [ObservableProperty] private IReadOnlyList<LabeledValue> withoutPositions = [];
    [ObservableProperty] private string comparison = "";

    partial void OnTextChanged(string value) => Refresh();

    public override void Reset() => Text = DefaultText;

    public override void OnActiveModelChanged() => Refresh();

    private void Refresh()
    {
        var model = _models.Current;
        var config = model.Transformer.Config;

        // The position vectors come from the trace of a forward pass over a full-length dummy text.
        var learned = model.Transformer.Forward(new int[config.ContextLength]).Trace.PositionEmbeddings;
        ModelPositions = Grid(learned);
        ModelPositionsTitle = config.Positions == PositionKind.Learned
            ? "This model's position vectors (learned)"
            : "This model's position vectors (the fixed wave scheme)";
        SinusoidalPositions = Grid(new SinusoidalPositions(config.ModelWidth).Forward(config.ContextLength));
        PositionLabels = Enumerable.Range(1, config.ContextLength).Select(i => i.ToString()).ToList();
        VectorLabels = Enumerable.Range(1, config.ModelWidth).Select(i => i.ToString()).ToList();

        if (string.IsNullOrEmpty(Text))
        {
            Message = LabText.EmptyPrompt;
            Notice = null;
            ShownText = "";
            WithPositions = WithoutPositions = [];
            Comparison = "";
            return;
        }
        Message = null;
        ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(model.Tokenizer.Encode(Text)));
        var with = model.Predict(Text);
        var without = model.Predict(Text, new ForwardOptions { RemovePositions = true });
        Notice = with.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        var p = Probabilities(with);
        var q = Probabilities(without);
        WithPositions = Top(model, p);
        WithoutPositions = Top(model, q);

        double moved = 0;
        for (int i = 0; i < p.Length; i++) moved += Math.Abs(p[i] - q[i]) / 2;
        Comparison = $"Top choice with positions: {WithPositions[0].Label} ({NumberText.Percent(WithPositions[0].Value)}). " +
                     $"Without: {WithoutPositions[0].Label} ({NumberText.Percent(WithoutPositions[0].Value)}). " +
                     $"{NumberText.Percent(moved)} of the probability moved to other characters.";
    }

    private static double[] Probabilities(PredictionResult result)
    {
        var logits = result.Forward.Logits;
        return Sampling.Probabilities(logits.Row(logits.Shape[0] - 1), new SamplingSettings(1.0));
    }

    private static IReadOnlyList<LabeledValue> Top(LanguageModel model, double[] p) =>
        Enumerable.Range(0, p.Length).OrderByDescending(i => p[i]).ThenBy(i => i).Take(TopCount)
            .Select(i => new LabeledValue(LabText.Visible(model.Tokenizer.TokenText(i)), p[i])).ToList();

    private static double[,] Grid(Tensor t)
    {
        int rows = t.Shape[0], columns = t.Shape[1];
        var grid = new double[rows, columns];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++) grid[r, c] = t.Data[r * columns + c];
        return grid;
    }
}
