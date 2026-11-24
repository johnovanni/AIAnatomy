using AIAnatomy.Core;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Transformers;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// Every token's embedding vector squashed onto a flat picture (PCA), for the active model and for an untrained model
/// with the same shape and starting seed. The same token is selected in both pictures.
/// </summary>
public sealed partial class EmbeddingsLabViewModel : LabViewModel
{
    public const string Id = "EmbeddingsLab";
    public const string LettersGroup = "letters";
    public const string DigitsGroup = "digits";
    public const string PunctuationGroup = "punctuation";
    public const string SpaceGroup = "space or new line";
    public const string UnknownGroup = "unknown";

    private readonly IActiveModelService _models;
    private double[][] _trainedRows = [];
    private double[][] _untrainedRows = [];
    private bool _refreshing;

    public EmbeddingsLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        Refresh(resetSeed: true);
    }

    public override string LabId => Id;

    /// <summary>One point per token id, in id order (the labels are the characters).</summary>
    [ObservableProperty] private IReadOnlyList<PlotPoint> trainedPoints = [];
    [ObservableProperty] private IReadOnlyList<PlotPoint> untrainedPoints = [];
    [ObservableProperty] private string trainedTitle = "";
    [ObservableProperty] private string trainedSpreadText = "";
    [ObservableProperty] private string untrainedSpreadText = "";
    /// <summary>The selected token id, or −1.</summary>
    [ObservableProperty] private int selectedIndex = -1;
    [ObservableProperty] private string selectedLabel = "";
    [ObservableProperty] private double[]? trainedVector;
    [ObservableProperty] private double[]? untrainedVector;
    [ObservableProperty] private string selectedSummary = "Click a point, or use the arrow keys, to see that token's vector.";

    public override void Reset()
    {
        SelectedIndex = -1;
        Refresh(resetSeed: true);
    }

    public override void OnActiveModelChanged()
    {
        SelectedIndex = -1;
        Refresh(resetSeed: true);
    }

    protected override void OnSeedEdited()
    {
        if (!_refreshing) Refresh(resetSeed: false);
    }

    partial void OnSelectedIndexChanged(int value) => ShowSelection();

    private void Refresh(bool resetSeed)
    {
        _refreshing = true;
        try
        {
            var model = _models.Current;
            if (resetSeed) Seed = model.Info.Seed;
            var config = model.Transformer.Config;
            TrainedTitle = $"The model you are using ({model.Info.Name})";

            _trainedRows = TableRows(model.Transformer);
            _untrainedRows = TableRows(new Transformer(config, new SeededRandom(Seed)));
            (TrainedPoints, TrainedSpreadText) = Plot(model, _trainedRows);
            (UntrainedPoints, UntrainedSpreadText) = Plot(model, _untrainedRows);
        }
        finally { _refreshing = false; }
        ReportScreen("trained", new object[] { TrainedTitle, TrainedPoints.ToArray(), TrainedSpreadText });
        ReportScreen("untrained", new object[] { UntrainedPoints.ToArray(), UntrainedSpreadText });
        ShowSelection();
    }

    private void ShowSelection()
    {
        int i = SelectedIndex;
        if (i < 0 || i >= _trainedRows.Length)
        {
            SelectedLabel = "";
            TrainedVector = UntrainedVector = null;
            SelectedSummary = "Click a point, or use the arrow keys, to see that token's vector.";
            ReportVector();
            return;
        }
        SelectedLabel = TrainedPoints[i].Label;
        TrainedVector = _trainedRows[i];
        UntrainedVector = _untrainedRows[i];
        SelectedSummary = $"Token “{SelectedLabel}” ({TrainedPoints[i].Group}) is a list of {_trainedRows[i].Length} numbers.";
        ReportVector();
    }

    private void ReportVector() =>
        ReportScreen("vector", new object[] { SelectedSummary, TrainedVector ?? [], UntrainedVector ?? [] });

    private static double[][] TableRows(Transformer transformer)
    {
        var table = transformer.Parameters().First(p => p.Name == "token_embedding").Value;
        return Enumerable.Range(0, table.Shape[0]).Select(table.Row).ToArray();
    }

    private static (IReadOnlyList<PlotPoint> Points, string Spread) Plot(LanguageModel model, double[][] rows)
    {
        var flat = Pca.Project2D(rows, out double explained);
        var points = new List<PlotPoint>(flat.Length);
        for (int id = 0; id < flat.Length; id++)
        {
            string text = model.Tokenizer.TokenText(id);
            points.Add(new PlotPoint(flat[id].X, flat[id].Y, LabText.Visible(text), GroupOf(id, text)));
        }
        string spread = $"This flat picture keeps {NumberText.ShortWithDecimals(explained * 100, 0)}% of the spread.";
        return (points, spread);
    }

    /// <summary>The group a token belongs to: id 0 is unknown; otherwise by what kind of character it is.</summary>
    public static string GroupOf(int id, string text)
    {
        if (id == 0 || text.Length == 0) return UnknownGroup;
        char c = text[0];
        if (char.IsLetter(c)) return LettersGroup;
        if (char.IsDigit(c)) return DigitsGroup;
        if (char.IsWhiteSpace(c) || char.IsControl(c)) return SpaceGroup;
        return PunctuationGroup;
    }
}
