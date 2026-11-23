using AIAnatomy.Models.Toys;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One line of the epoch table: the weights after one pass over the four points and how many points were wrong in it.</summary>
public sealed record EpochRow(int Epoch, string W1, string W2, string Bias, int Errors)
{
    public string Description => $"Epoch {Epoch}: W1 {W1}, W2 {W2}, bias {Bias}, {Errors} {(Errors == 1 ? "point" : "points")} wrong";
}

/// <summary>
/// Part (a) of the learning lab: one neuron that learns AND, OR or XOR with the perceptron rule. The weights come from
/// <see cref="Perceptron"/>; stepping one epoch trains again from zero for one more epoch (the same result as carrying on).
/// </summary>
public sealed partial class PerceptronPanelViewModel : ObservableObject
{
    public const string AndName = "AND", OrName = "OR", XorName = "XOR";
    public const double DefaultRate = 0.1;
    public const int DefaultEpochLimit = 20, XorEpochLimit = 100, MaxEpochLimit = 100, TableEpochs = 20;
    public const double MinRate = 0.01, MaxRate = 1.0;

    /// <summary>The decision line is drawn only inside this square, which the plot then fits.</summary>
    public const double ViewLow = -0.25, ViewHigh = 1.25;

    public const string XorNote =
        "XOR is the case a single neuron cannot learn. No straight line can separate the points that should be 1 from the points that should be 0, " +
        "so some point is always on the wrong side, however many epochs you run. This is why networks need hidden layers.";

    public const string RuleNote =
        "The neuron answers 1 when W1·x1 + W2·x2 + Bias is above 0, and 0 otherwise. The decision line is where that sum is exactly 0. " +
        "After each wrong answer, every weight is nudged by rate × (right answer − the neuron's answer) × its input.";

    public static IReadOnlyList<string> DatasetNames { get; } = [AndName, OrName, XorName];

    private readonly Perceptron _perceptron = new();
    private int _steps;
    private bool _syncing;

    public PerceptronPanelViewModel() => Refresh();

    [ObservableProperty] private string dataset = AndName;
    [ObservableProperty] private double rate = DefaultRate;
    [ObservableProperty] private int epochLimit = DefaultEpochLimit;
    [ObservableProperty] private IReadOnlyList<PerceptronEpoch> epochs = [];
    [ObservableProperty] private IReadOnlyList<EpochRow> tableRows = [];
    [ObservableProperty] private string tableNote = "";
    [ObservableProperty] private IReadOnlyList<PlotPoint> points = [];
    [ObservableProperty] private IReadOnlyList<LineSeries> lines = [];
    [ObservableProperty] private string lineText = "";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string wrongPoints = "";
    [ObservableProperty] private string rateText = "";
    [ObservableProperty] private bool isXor;

    public IReadOnlyList<LabeledPoint> Data => Dataset switch { OrName => Perceptron.Or, XorName => Perceptron.Xor, _ => Perceptron.And };

    /// <summary>True when the last epoch had no wrong points, or the epoch limit has been reached.</summary>
    public bool IsFinished => Epochs.Count > 0 && (Epochs[^1].Errors == 0 || Epochs.Count >= EpochLimit);

    partial void OnDatasetChanged(string value)
    {
        if (_syncing) return;
        _syncing = true;
        EpochLimit = value == XorName ? XorEpochLimit : DefaultEpochLimit;
        _syncing = false;
        Restart();
    }

    partial void OnRateChanged(double value) { if (!_syncing) Restart(); }

    partial void OnEpochLimitChanged(int value) { if (!_syncing) Refresh(); }

    public void Reset()
    {
        _syncing = true;
        Dataset = AndName;
        Rate = DefaultRate;
        EpochLimit = DefaultEpochLimit;
        _syncing = false;
        Restart();
    }

    /// <summary>Does one more epoch (one pass over the four points), unless the run has finished.</summary>
    [RelayCommand]
    private void StepOneEpoch()
    {
        if (IsFinished) return;
        _steps++;
        Epochs = _perceptron.Train(Data, Rate, _steps);
        Refresh();
    }

    /// <summary>Trains from zero until no point is wrong or the epoch limit is reached.</summary>
    [RelayCommand]
    private void Run()
    {
        Epochs = _perceptron.Train(Data, Rate, EpochLimit);
        _steps = Epochs.Count;
        Refresh();
    }

    /// <summary>The Reset button of this part: AND, rate 0.1, epoch limit 20, no epochs run.</summary>
    [RelayCommand]
    private void ResetPerceptron() => Reset();

    private void Restart()
    {
        _steps = 0;
        Epochs = [];
        _perceptron.Train(Data, Rate, 0);   // zero epochs: the weights go back to 0
        Refresh();
    }

    private void Refresh()
    {
        IsXor = Dataset == XorName;
        RateText = NumberText.ShortWithDecimals(Rate, 2);
        var data = Data;
        Points = data.Select(p => new PlotPoint(p.X1, p.X2, $"({p.X1:0}, {p.X2:0})", p.Label == 1 ? "Should be 1" : "Should be 0")).ToList();

        var shown = Epochs.Count > TableEpochs ? Epochs.Skip(Epochs.Count - TableEpochs) : Epochs;
        TableRows = shown.Select(e => new EpochRow(e.Epoch, NumberText.Short(e.W1), NumberText.Short(e.W2), NumberText.Short(e.Bias), e.Errors)).ToList();
        TableNote = Epochs.Count > TableEpochs ? $"Showing the last {TableEpochs} of {Epochs.Count} epochs." : "";

        double w1 = _perceptron.W1, w2 = _perceptron.W2, bias = _perceptron.Bias;
        var segment = DecisionLine(w1, w2, bias);
        Lines = segment == null ? [] : [new LineSeries("Decision line", segment)];
        LineText = w1 == 0 && w2 == 0
            ? "No decision line yet: both weights are 0."
            : $"Decision line: {NumberText.Short(w1)}·x1 + {NumberText.Short(w2)}·x2 + {NumberText.Short(bias)} = 0" + (segment == null ? " (outside the plot)" : "");

        var wrong = data.Where(p => _perceptron.Predict(p.X1, p.X2) != p.Label).Select(p => $"({p.X1:0}, {p.X2:0})").ToList();
        WrongPoints = wrong.Count == 0 ? "No point is on the wrong side." : $"On the wrong side now: {string.Join(", ", wrong)}.";

        if (Epochs.Count == 0)
            Status = "No epochs yet. Step one epoch, or run until no point is wrong or the limit is reached.";
        else
        {
            var last = Epochs[^1];
            Status = last.Errors == 0
                ? $"Done after {last.Epoch} {(last.Epoch == 1 ? "epoch" : "epochs")}: no point was wrong in the last pass."
                : Epochs.Count >= EpochLimit
                    ? $"Stopped at the limit of {EpochLimit} epochs, with {last.Errors} {(last.Errors == 1 ? "point" : "points")} still wrong."
                    : $"Epoch {last.Epoch}: {last.Errors} {(last.Errors == 1 ? "point was" : "points were")} wrong in this pass.";
        }
    }

    /// <summary>
    /// The part of the line W1·x1 + W2·x2 + Bias = 0 inside the view square, as two end points, or null when the line
    /// is undefined (both weights 0) or misses the square.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)>? DecisionLine(double w1, double w2, double bias)
    {
        const double Tiny = 1e-12;
        if (Math.Abs(w1) < Tiny && Math.Abs(w2) < Tiny) return null;
        var found = new List<(double X, double Y)>();
        void Add(double x, double y)
        {
            if (x < ViewLow - 1e-9 || x > ViewHigh + 1e-9 || y < ViewLow - 1e-9 || y > ViewHigh + 1e-9) return;
            if (!found.Any(p => Math.Abs(p.X - x) < 1e-9 && Math.Abs(p.Y - y) < 1e-9)) found.Add((x, y));
        }
        foreach (double x in new[] { ViewLow, ViewHigh })
            if (Math.Abs(w2) >= Tiny) Add(x, -(w1 * x + bias) / w2);
        foreach (double y in new[] { ViewLow, ViewHigh })
            if (Math.Abs(w1) >= Tiny) Add(-(w2 * y + bias) / w1, y);
        if (found.Count < 2) return null;
        return found.OrderBy(p => p.X).ThenBy(p => p.Y).Take(2).ToList();
    }
}
