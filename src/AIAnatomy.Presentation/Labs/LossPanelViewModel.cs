using AIAnatomy.Models.Toys;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One step of the descent: where it is and what the loss is there.</summary>
public sealed record PathRow(int Step, string W1, string W2, string Loss)
{
    public string Description => $"Step {Step}: w1 {W1}, w2 {W2}, loss {Loss}";
}

/// <summary>
/// Part (b) of the learning lab: gradient descent on the bowl w1² + 10·w2² from (−4, 1.5). The path comes from
/// <see cref="LossSurface.Descend"/>; the contour grid from <see cref="LossSurface.Loss"/>.
/// </summary>
public sealed partial class LossPanelViewModel : ObservableObject
{
    public const double StartW1 = -4, StartW2 = 1.5;
    public const double DefaultRate = 0.04, MinRate = 0.01, MaxRate = 0.15;
    public const int Steps = 30;
    public const double W1Min = -5, W1Max = 5, W2Min = -2, W2Max = 2;
    public const int GridColumns = 51, GridRows = 21;

    public const string TooLargeMessage = "Too large: each step overshoots further";

    public const string ThresholdNote =
        "Gradient descent moves each weight a little against its slope: new weight = weight − rate × slope. Here the slopes are 2·w1 and 20·w2. " +
        "Along w2 the curve is steep, so each step multiplies w2 by (1 − 20 × rate). Above a rate of 0.1 that factor is below −1: " +
        "each step jumps over the bottom and lands further from it than before, so w2 grows instead of shrinking.";

    public static IReadOnlyList<double> Levels { get; } = [1, 4, 10, 20, 40];

    public LossPanelViewModel()
    {
        Grid = new double[GridRows, GridColumns];
        for (int r = 0; r < GridRows; r++)
            for (int c = 0; c < GridColumns; c++)
                Grid[r, c] = LossSurface.Loss(W1Min + (W1Max - W1Min) * c / (GridColumns - 1), W2Min + (W2Max - W2Min) * r / (GridRows - 1));
        Refresh();
    }

    /// <summary>Loss at each grid point: row 0 is w2 = −2 (the bottom of the plot), column 0 is w1 = −5.</summary>
    public double[,] Grid { get; }

    [ObservableProperty] private double rate = DefaultRate;
    [ObservableProperty] private string rateText = "";
    [ObservableProperty] private IReadOnlyList<PlotPoint> path = [];
    [ObservableProperty] private IReadOnlyList<PathRow> rows = [];
    [ObservableProperty] private string factorText = "";
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool isDiverging;

    partial void OnRateChanged(double value) => Refresh();

    public void Reset() => Rate = DefaultRate;

    private void Refresh()
    {
        double used = double.IsNaN(Rate) ? DefaultRate : Math.Clamp(Rate, MinRate, MaxRate);
        string usedText = NumberText.ShortWithDecimals(used, 2);
        RateText = usedText;
        var steps = LossSurface.Descend(StartW1, StartW2, used, Steps);
        Path = steps.Select((s, i) => new PlotPoint(s.W1, s.W2, i == 0 ? "start" : i == Steps ? $"step {i}" : "", i == 0 ? "Start" : "Path")).ToList();
        var shown = steps.Select((s, i) => (s, i)).Where(t => t.i <= 5 || t.i == Steps).ToList();
        Rows = shown.Select(t => new PathRow(t.i, NumberText.Short(t.s.W1), NumberText.Short(t.s.W2), NumberText.Short(t.s.Loss))).ToList();

        // The loss grows when the last step ended higher than the one before it.
        IsDiverging = steps[^1].Loss > steps[^2].Loss;
        Message = IsDiverging ? TooLargeMessage : null;
        FactorText = $"Each step multiplies w2 by 1 − 20 × {usedText} = {NumberText.Short(1 - 20 * used)}, and w1 by 1 − 2 × {usedText} = {NumberText.Short(1 - 2 * used)}.";
        var first = steps[0];
        var last = steps[^1];
        Summary = $"Start: w1 = {NumberText.Short(first.W1)}, w2 = {NumberText.Short(first.W2)}, loss {NumberText.Short(first.Loss)}. " +
                  $"After {Steps} steps: w1 = {NumberText.Short(last.W1)}, w2 = {NumberText.Short(last.W2)}, loss {NumberText.Short(last.Loss)}.";
    }
}
