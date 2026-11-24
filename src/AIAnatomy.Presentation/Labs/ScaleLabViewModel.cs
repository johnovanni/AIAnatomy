using System.Globalization;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One row of the size chart. <c>Count</c> and <c>BarFraction</c> are null when the size is not published.</summary>
public sealed record ScaleRow(string Label, long? Count, string CountText, string? Source, double? BarFraction, string? Note = null)
{
    public string Description => $"{Label}{(Count == null ? "." : ":")} {CountText}" + (Note == null ? "" : $" {Note}") + (Source == null ? "" : $". Source: {Source}");
}

/// <summary>How big our model is, by part, next to published sizes of real models on a logarithmic scale.</summary>
public sealed partial class ScaleLabViewModel : LabViewModel
{
    public const string Id = "ScaleLab";
    public const long Gpt3Count = 175_000_000_000, Gpt2Count = 124_000_000;

    public static IReadOnlyList<string> PartOrder => TrainSettingsPanelViewModel.PartOrder;

    public const string ScaleNote =
        "The bars use a logarithmic scale: each step along it is 10 times bigger than the one before. " +
        "That is why a bar for a model a million times larger is only six steps longer.";

    public const string Gpt2Number = "124 million";
    public const string Gpt2Note = "(the GPT-2 paper's table lists 117 million; the released model has 124 million)";

    public const string ClaudeLabel = "Claude: size not published";

    private readonly IActiveModelService _models;

    public ScaleLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        Refresh();
    }

    public override string LabId => Id;

    [ObservableProperty] private long ourCount;
    [ObservableProperty] private string ourDescription = "";
    [ObservableProperty] private IReadOnlyList<ParameterRow> parts = [];
    [ObservableProperty] private IReadOnlyList<ScaleRow> models = [];
    [ObservableProperty] private string axisText = "";
    /// <summary>The scale runs from 10^AxisLow to 10^AxisHigh; a bar's length is (log10 n - low) / (high - low).</summary>
    [ObservableProperty] private int axisLow;
    [ObservableProperty] private int axisHigh;
    /// <summary>The value of each faint line, from the left; the right edge is 10^AxisHigh.</summary>
    [ObservableProperty] private IReadOnlyList<string> decadeLabels = [];
    /// <summary>The same lines in short form (10K, 1M, 1B), the text written at the gridlines.</summary>
    [ObservableProperty] private IReadOnlyList<string> shortDecadeLabels = [];
    [ObservableProperty] private string ratioSentence = "";

    public override void Reset() => Refresh();

    public override void OnActiveModelChanged() => Refresh();

    private void Refresh()
    {
        var model = _models.Current;
        var counts = model.Transformer.ParameterCountsByPart();
        Parts = PartOrder.Select(p => new ParameterRow(p, counts[p], NumberText.Short(counts[p]))).ToList();
        OurCount = Parts.Sum(p => p.Count);
        var config = model.Transformer.Config;
        string layers = config.Layers == 1 ? "1 layer" : $"{config.Layers} layers";
        string heads = config.Heads == 1 ? "1 head" : $"{config.Heads} heads";
        OurDescription = $"This model: {NumberText.Short(OurCount)} numbers to learn ({layers}, {heads} each, width {config.ModelWidth}).";

        // The scale runs from one step (power of ten) below our model to the first one above GPT-3, so every bar is visible.
        int low = AxisLow = (int)Math.Floor(Math.Log10(Math.Max(1, OurCount))) - 1;
        int high = AxisHigh = (int)Math.Ceiling(Math.Log10(Gpt3Count));
        double Fraction(long n) => (Math.Log10(n) - low) / (high - low);
        Models =
        [
            new("This model", OurCount, NumberText.Short(OurCount), null, Fraction(OurCount)),
            new("GPT-2 small", Gpt2Count, Gpt2Number, "radford2019 (117 million, Radford et al., 2019); openai-gpt2-release (124 million, OpenAI, 2019)", Fraction(Gpt2Count), Gpt2Note),
            new("GPT-3", Gpt3Count, "175 billion", "brown2020 (Brown et al., 2020)", Fraction(Gpt3Count)),
            new(ClaudeLabel, null, "No bar is drawn.", null, null),
        ];
        DecadeLabels = Enumerable.Range(low, high - low).Select(Power).ToList();
        ShortDecadeLabels = Enumerable.Range(low, high - low).Select(ShortPower).ToList();
        AxisText = $"Each faint line is a power of ten. From the left they are {string.Join(", ", DecadeLabels)}; the right edge is {Power(high)}. (K = thousand, M = million, B = billion, T = trillion.)";
        RatioSentence = $"GPT-3 has about {Times(Gpt3Count / (double)OurCount)} times as many parameters as this model.";
        // The first report of each screen is its baseline; after that only a switch of model (or Reset, which finds the same numbers) can change them.
        ReportScreen("parts", new object[] { OurDescription, OurCount, Parts.ToArray() });
        ReportScreen("compare", new object[] { Models.ToArray(), AxisText, RatioSentence, ShortDecadeLabels.ToArray() });
    }

    /// <summary>1, 10, 100, 1K, 10K, 100K, 1M ... 1B ... 1T.</summary>
    public static string ShortPower(int exponent)
    {
        (string unit, int shift) = exponent switch { < 3 => ("", 0), < 6 => ("K", 3), < 9 => ("M", 6), < 12 => ("B", 9), _ => ("T", 12) };
        return $"{(long)Math.Pow(10, exponent - shift)}{unit}";
    }

    private static string Power(int exponent) =>
        exponent < 3 ? ((long)Math.Pow(10, exponent)).ToString(CultureInfo.InvariantCulture) : Word(exponent);

    private static string Word(int exponent)
    {
        (string unit, int shift) = exponent switch { < 6 => ("thousand", 3), < 9 => ("million", 6), < 12 => ("billion", 9), _ => ("trillion", 12) };
        return $"{(long)Math.Pow(10, exponent - shift)} {unit}";
    }

    /// <summary>A ratio in words with one decimal, such as "1.5 million".</summary>
    private static string Times(double ratio)
    {
        if (ratio >= 1e6) return $"{(ratio / 1e6).ToString("0.#", CultureInfo.InvariantCulture)} million";
        if (ratio >= 1e3) return $"{(ratio / 1e3).ToString("0.#", CultureInfo.InvariantCulture)} thousand";
        return ratio.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
