using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Vision;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>A filter the lab can load: a hand-made preset or one of the shipped network's first-layer filters. The nine
/// weights are row by row (top row first, left to right), as <see cref="Ops.Conv2d"/> lays out a one-channel filter.</summary>
public sealed record FilterPreset(string Name, IReadOnlyList<double> Weights, bool IsLearned)
{
    /// <summary>"Vertical edge. Top row −1, 0, 1. Middle row … Bottom row …", for tooltips and screen readers.</summary>
    public string Description => $"{Name}. {FilterCell.RowsText(Weights)}";

    /// <summary>The name, which is what a list of presets shows.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// One of the filter's nine numbers. <see cref="Value"/> is clamped to <see cref="FiltersLabViewModel.MinWeight"/> ..
/// <see cref="FiltersLabViewModel.MaxWeight"/>; not-a-number is ignored. <see cref="Text"/> is what the box shows: typing a
/// number sets the value at once (the typographic minus is accepted), typing something that is not a number leaves the
/// value as it was, and a number outside the range is replaced by the clamped one.
/// </summary>
public sealed class FilterCell : ObservableObject
{
    private static readonly string[] RowNames = ["Top", "Middle", "Bottom"];
    private readonly Action<FilterCell> _changed;
    private double _value;
    private string _text;

    internal FilterCell(int row, int column, double value, Action<FilterCell> changed)
    {
        Row = row;
        Column = column;
        _value = Clamp(value);
        _text = Format(_value);
        _changed = changed;
    }

    /// <summary>0, 1 or 2 from the top.</summary>
    public int Row { get; }

    /// <summary>0, 1 or 2 from the left.</summary>
    public int Column { get; }

    /// <summary>"Filter number in the top row, left column", for the box's accessible name.</summary>
    public string Name => $"Filter number in the {RowNames[Row].ToLowerInvariant()} row, {(Column switch { 0 => "left", 1 => "middle", _ => "right" })} column";

    public double Value
    {
        get => _value;
        set
        {
            if (double.IsNaN(value)) return;
            double v = Clamp(value);
            string text = Format(v);
            bool changed = v != _value;
            _value = v;
            if (text != _text) { _text = text; OnPropertyChanged(nameof(Text)); }
            if (!changed) return;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ExactText));
            _changed(this);
        }
    }

    /// <summary>What the box shows: the number to three decimals, or what is being typed.</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= "";
            if (!TryParse(value, out double v))
            {
                // Not a number yet ("−", "", "abc"): keep what was typed; the number stays as it was.
                if (value != _text) { _text = value; OnPropertyChanged(nameof(Text)); }
                return;
            }
            double clamped = Clamp(v);
            // Keep the typing as typed ("0.", "1.50") unless it had to be clamped.
            string text = clamped == v ? value : Format(clamped);
            if (text != _text) { _text = text; OnPropertyChanged(nameof(Text)); }
            if (clamped == _value) return;
            _value = clamped;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ExactText));
            _changed(this);
        }
    }

    /// <summary>The number in full, for the box's tooltip (a learned filter's numbers have many decimals).</summary>
    public string ExactText => NumberText.Exact(_value);

    /// <summary>Puts the number back in the box in the usual form (when the box loses the keyboard, so half-typed text
    /// such as "−" does not stay on screen).</summary>
    public void ShowValue()
    {
        string text = Format(_value);
        if (text == _text) return;
        _text = text;
        OnPropertyChanged(nameof(Text));
    }

    /// <summary>"Top row −1, 0, 1. Middle row −1, 0, 1. Bottom row −1, 0, 1." (three decimals at most).</summary>
    public static string RowsText(IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        return string.Join(" ", Enumerable.Range(0, 3).Select(r =>
            $"{RowNames[r]} row {string.Join(", ", Enumerable.Range(0, 3).Select(c => NumberText.Short(weights[r * 3 + c])))}."));
    }

    internal static double Clamp(double v) => Math.Clamp(v, FiltersLabViewModel.MinWeight, FiltersLabViewModel.MaxWeight);

    private static string Format(double v) => NumberText.Short(v);

    private static bool TryParse(string text, out double value)
    {
        string t = text.Trim().Replace('−', '-');
        return (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
               && !double.IsNaN(value);
    }
}

/// <summary>
/// Lesson 4.6's lab: one 3 by 3 filter sliding over an 8 by 8 digit (the UCI digits the shipped network learned from).
/// The input is a digit from the bundled test set (Previous, Next, or a digit 0 to 9, which is the keyboard path) or the
/// learner's drawing, made into an 8 by 8 image by <see cref="DigitImage.FromStrokes"/>. The filter is nine editable
/// numbers, or a preset: four hand-made ones and the shipped network's 16 first-layer filters ("Learned 1" to
/// "Learned 16", exactly its conv1 weights). The feature map is <see cref="Ops.Conv2d"/>, the op the network itself uses,
/// with padding 1 and bias 0, so it is 8 by 8 too. Everything is worked out again at once, on the calling thread (64
/// positions of 9 multiplications). Screens: "image" and "feature-map".
/// </summary>
public sealed partial class FiltersLabViewModel : LabViewModel
{
    public const string Id = "FiltersLab";
    public const int ImageSize = 8, Kernel = 3, Padding = 1, Positions = ImageSize * ImageSize;
    public const double MinWeight = -10, MaxWeight = 10;

    public const string VerticalEdgeName = "Vertical edge", HorizontalEdgeName = "Horizontal edge", BlurName = "Blur", SharpenName = "Sharpen";
    public const string OwnFilterName = "Your own filter";

    public const string CountText = "The same 9 numbers were used at all 64 positions.";
    public const string EmptyDrawingMessage = "The pad is empty, so the lab shows the test digit. Draw a digit on the pad to use your own.";
    public const string LoadingMessage = "Loading the digits and the network's filters…";
    public const string LoadFailedMessage = "The digits could not be loaded because of an unexpected problem. Reinstalling the app may fix it.";

    public const string DrawingCaption =
        "Your drawing, made into an 8 by 8 image the way the test digits were made: the ink counted in 4 by 4 blocks of a 32 by 32 picture.";

    public const string MapNote =
        "This map is the filter's sum alone (bias 0). In the network, each learned filter's sum also gets that filter's own bias added, " +
        "and then every number below zero is set to 0, before the next layer reads it.";

    /// <summary>The hand-made presets, in the order the lab shows them.</summary>
    public static IReadOnlyList<FilterPreset> HandMadePresets { get; } =
    [
        new(VerticalEdgeName, [-1, 0, 1, -1, 0, 1, -1, 0, 1], false),
        new(HorizontalEdgeName, [-1, -1, -1, 0, 0, 0, 1, 1, 1], false),
        new(BlurName, Enumerable.Repeat(1.0 / 9, 9).ToArray(), false),
        new(SharpenName, [0, -1, 0, -1, 5, -1, 0, -1, 0], false),
    ];

    /// <summary>Row and column names, 1 to 8.</summary>
    public static IReadOnlyList<string> PositionLabels { get; } = Enumerable.Range(1, ImageSize).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();

    /// <summary>The digits 0 to 9, for the digit picker.</summary>
    public static IReadOnlyList<int> Digits { get; } = Enumerable.Range(0, 10).ToArray();

    private readonly FamilyModels _family;
    private readonly Task _ready;
    private DigitSet? _set;
    private double[] _pixels = new double[Positions];
    private bool _quiet;

    public FiltersLabViewModel(FamilyModels family)
    {
        _family = family ?? throw new ArgumentNullException(nameof(family));
        var start = HandMadePresets[0].Weights;
        Filter = Enumerable.Range(0, 9).Select(i => new FilterCell(i / 3, i % 3, start[i], OnCellChanged)).ToArray();
        Weights = start.ToArray();
        _ready = LoadAsync();
    }

    public override string LabId => Id;

    /// <summary>Loading the test set and the network.</summary>
    public override Task WorkCompleted => _ready;

    [ObservableProperty] private bool isReady;

    /// <summary>While loading, what is happening; if loading failed, why. Null once ready.</summary>
    [ObservableProperty] private string? loadMessage = LoadingMessage;

    /// <summary>The shipped network's first-layer filters, "Learned 1" onwards; empty until loaded.</summary>
    [ObservableProperty] private IReadOnlyList<FilterPreset> learnedPresets = [];

    /// <summary>The filter's nine numbers, row by row.</summary>
    public IReadOnlyList<FilterCell> Filter { get; }

    /// <summary>The filter's nine numbers now (a new array after every change).</summary>
    [ObservableProperty] private double[] weights = [];

    /// <summary>The preset the filter matches exactly, or <see cref="OwnFilterName"/>.</summary>
    [ObservableProperty] private string filterName = VerticalEdgeName;

    /// <summary>The test digit on show (or last shown, while a drawing is in use), from 0.</summary>
    public int SampleIndex { get; private set; }

    /// <summary>How many digits the test set holds (0 until loaded).</summary>
    public int SampleCount => _set?.Count ?? 0;

    /// <summary>Which digit the test digit is.</summary>
    [ObservableProperty] private int sampleLabel;

    /// <summary>True while the image is the learner's drawing.</summary>
    [ObservableProperty] private bool usingDrawing;

    /// <summary><see cref="EmptyDrawingMessage"/> after an empty drawing; otherwise null.</summary>
    [ObservableProperty] private string? message;

    /// <summary>The input image, [row, column], values 0 to 1; null until loaded.</summary>
    [ObservableProperty] private double[,]? image;

    [ObservableProperty] private string imageCaption = "";

    /// <summary>The feature map, [row, column], the same size as the image; null until loaded.</summary>
    [ObservableProperty] private double[,]? featureMap;

    /// <summary>The colour scale runs from −MapLimit to +MapLimit: the largest size in the map (1 when it is all 0).</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MapMinimum))] private double mapLimit = 1;

    /// <summary>−<see cref="MapLimit"/>, the blue end of the scale.</summary>
    public double MapMinimum => -MapLimit;

    /// <summary>"Smallest −3, largest 3."</summary>
    [ObservableProperty] private string featureMapCaption = "";

    [RelayCommand]
    public void Next()
    {
        if (_set is null) return;
        ShowSample((SampleIndex + 1) % _set.Count);
    }

    [RelayCommand]
    public void Previous()
    {
        if (_set is null) return;
        ShowSample((SampleIndex - 1 + _set.Count) % _set.Count);
    }

    /// <summary>Shows the next test digit (after the one last shown, going round) that is a <paramref name="digit"/>.</summary>
    [RelayCommand]
    public void ShowDigit(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digit, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);
        if (_set is null) return;
        for (int step = 1; step <= _set.Count; step++)
        {
            int i = (SampleIndex + step) % _set.Count;
            if (_set.Label(i) == digit) { ShowSample(i); return; }
        }
    }

    /// <summary>Loads a preset's nine numbers (one change, so the map is worked out and lights once).</summary>
    [RelayCommand]
    public void ChoosePreset(FilterPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        SetFilterQuietly(preset.Weights);
        Recompute();
    }

    /// <summary>Uses the drawing as the image. No ink (no points) shows <see cref="EmptyDrawingMessage"/> and goes back
    /// to the test digit last shown.</summary>
    public void UseDrawing(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        if (_set is null) return;
        var pixels = DigitImage.FromStrokes(strokes, ImageSize);
        if (pixels is null)
        {
            ShowSample(SampleIndex, EmptyDrawingMessage);
            return;
        }
        UseImage(pixels, DrawingCaption);
    }

    /// <summary>For tests: puts any 8 by 8 image (row by row, 0 to 1) in as the input.</summary>
    internal void UseImage(double[] pixels, string caption)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != Positions) throw new ArgumentException($"An image needs {Positions} pixels.", nameof(pixels));
        _pixels = (double[])pixels.Clone();
        Image = ToGrid(_pixels);
        ImageCaption = caption;
        Message = null;
        UsingDrawing = true;
        Recompute();
    }

    public override void Reset()
    {
        SetFilterQuietly(HandMadePresets[0].Weights);
        if (_set is null) return;
        ShowSample(0);
    }

    private void ShowSample(int index, string? message = null)
    {
        var set = _set!;
        SampleIndex = index;
        OnPropertyChanged(nameof(SampleIndex));
        SampleLabel = set.Label(index);
        _pixels = set.Pixels(index);
        Image = ToGrid(_pixels);
        ImageCaption = $"Test digit {N0(index + 1)} of {N0(set.Count)}: a {SampleLabel}.";
        UsingDrawing = false;
        Message = message;
        Recompute();
    }

    private void SetFilterQuietly(IReadOnlyList<double> values)
    {
        _quiet = true;
        try
        {
            for (int i = 0; i < 9; i++) Filter[i].Value = values[i];
        }
        finally { _quiet = false; }
    }

    private void OnCellChanged(FilterCell cell)
    {
        if (!_quiet) Recompute();
    }

    private void Recompute()
    {
        var w = Filter.Select(c => c.Value).ToArray();
        Weights = w;
        FilterName = HandMadePresets.Concat(LearnedPresets).FirstOrDefault(p => p.Weights.SequenceEqual(w))?.Name ?? OwnFilterName;
        if (_set is null) return;

        // The network's own op: input [1, 1, 8, 8], one filter [1, 9], bias 0, kernel 3, padding 1.
        var output = Ops.Conv2d(Tensor.FromArray((double[])_pixels.Clone(), 1, 1, ImageSize, ImageSize),
            Tensor.FromArray(w, 1, Kernel * Kernel), Tensor.Zeros(1), Kernel, Padding);
        var map = ToGrid(output.Data);
        double min = output.Data.Min(), max = output.Data.Max();
        double limit = Math.Max(Math.Abs(min), Math.Abs(max));
        MapLimit = limit > 0 ? limit : 1;
        FeatureMap = map;
        FeatureMapCaption = $"Smallest {NumberText.Short(min)}, largest {NumberText.Short(max)}.";
        ReportScreens();
    }

    private async Task LoadAsync()
    {
        try
        {
            var setTask = _family.DigitTestSetAsync();
            var netTask = _family.DigitsAsync();
            await Task.WhenAll(setTask, netTask);
            var set = setTask.Result;
            var (net, _) = netTask.Result;
            if (set.ImageSize != ImageSize || net.Config.ImageSize != ImageSize || set.Count == 0)
                throw new InvalidDataException($"The digits that come with the app are not {ImageSize} by {ImageSize} images. Reinstalling the app puts them back.");
            var conv1 = net.Parameters().Single(p => p.Name == DigitNet.Conv1Name).Value;
            int k2 = Kernel * Kernel;
            LearnedPresets = Enumerable.Range(0, net.Config.Filters1)
                .Select(f => new FilterPreset($"Learned {f + 1}", conv1.Data.Skip(f * k2).Take(k2).ToArray(), true))
                .ToArray();
            _set = set;
            OnPropertyChanged(nameof(SampleCount));
            LoadMessage = null;
            IsReady = true;
            ShowSample(0);   // the baselines: nothing lights for loading
        }
        catch (InvalidDataException e)
        {
            LoadMessage = e.Message;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Anything else must not leave "Loading…" on screen for ever.
            ErrorLog.ReportBackground(e);
            LoadMessage = LoadFailedMessage;
        }
    }

    private void ReportScreens()
    {
        ReportScreen("image", new object[] { ImageCaption, Message ?? "" }.Concat(_pixels.Cast<object>()).ToArray());
        ReportScreen("feature-map", new object[] { FilterName }.Concat(Weights.Cast<object>()).Concat(FeatureMap!.Cast<double>().Cast<object>()).ToArray());
    }

    private static double[,] ToGrid(IReadOnlyList<double> values)
    {
        var grid = new double[ImageSize, ImageSize];
        for (int r = 0; r < ImageSize; r++)
            for (int c = 0; c < ImageSize; c++) grid[r, c] = values[r * ImageSize + c];
        return grid;
    }

    private static string N0(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
