using AIAnatomy.Core;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One key's line of the toy's working: its dot product with the query, the scaled score, the softmax weight and its share of the answer.</summary>
public sealed record ToyRow(
    string Name, string DotWorking, double Dot, double Scaled, double Exponential, double Weight,
    string WeightText, string ContributionText)
{
    /// <summary>The four lines of working for this key, one per step.</summary>
    public string DotLine => $"Q · {Name} = {DotWorking} = {NumberText.Short(Dot)}";
    public string ScaledLine => $"{Name}: {NumberText.Short(Dot)} × {NumberText.Short(AttentionToyViewModel.ScaleFactor)} = {NumberText.Short(Scaled)}";
    public string WeightLine => $"{Name}: e^{NumberText.Short(Scaled)} = {NumberText.Short(Exponential)}, weight = {WeightText} = {NumberText.Short(Weight)}";
    public string ShareLine => $"{Name}: {ContributionText}";
}

/// <summary>
/// Attention in two dimensions with one query and two to four key/value pairs, small enough to check by hand.
/// Each number on screen comes from the Core operations: dot products (MatMul), the 1/√2 scaling (Scale), softmax
/// (SoftmaxRows) and the weighted sum of the values (MatMul). Every vector can be dragged on a plot or typed in.
/// </summary>
public sealed partial class AttentionToyViewModel : ObservableObject
{
    public const int Width = 2;
    public const int MinKeys = 2;
    public const int MaxKeys = 4;
    public const int DefaultKeys = 3;
    public static readonly double ScaleFactor = 1.0 / Math.Sqrt(Width);

    private static readonly (double X, double Y) DefaultQuery = (1.0, 0.5);
    private static readonly (double X, double Y)[] DefaultKeyVectors = [(1.0, 0.0), (0.0, 1.0), (-1.0, 0.5), (-0.5, -1.0)];
    private static readonly (double X, double Y)[] DefaultValueVectors = [(2.0, 0.0), (0.0, 2.0), (-1.0, -1.0), (1.0, 1.0)];

    private readonly DragHandle[] _keys = new DragHandle[MaxKeys];
    private readonly DragHandle[] _values = new DragHandle[MaxKeys];
    private bool _updating;

    public AttentionToyViewModel()
    {
        Query = new DragHandle("Q", DefaultQuery.X, DefaultQuery.Y);
        Output = new DragHandle("Answer", 0, 0, isFixed: true);
        for (int i = 0; i < MaxKeys; i++)
        {
            _keys[i] = new DragHandle($"K{i + 1}", DefaultKeyVectors[i].X, DefaultKeyVectors[i].Y);
            _values[i] = new DragHandle($"V{i + 1}", DefaultValueVectors[i].X, DefaultValueVectors[i].Y);
        }
        foreach (var h in new[] { Query }.Concat(_keys).Concat(_values)) h.PropertyChanged += (_, _) => Recompute();
        Recompute();
    }

    public DragHandle Query { get; }
    public DragHandle Output { get; }
    public IReadOnlyList<DragHandle> AllKeys => _keys;
    public IReadOnlyList<DragHandle> AllValues => _values;

    [ObservableProperty] private int keyCount = DefaultKeys;

    /// <summary>The keys in use, then (for the first plot) the query: what can be dragged on the "query and keys" plot.</summary>
    [ObservableProperty] private IReadOnlyList<DragHandle> keyPlotHandles = [];
    /// <summary>The values in use and the answer: the "values and answer" plot.</summary>
    [ObservableProperty] private IReadOnlyList<DragHandle> valuePlotHandles = [];
    [ObservableProperty] private IReadOnlyList<DragHandle> activeKeys = [];
    [ObservableProperty] private IReadOnlyList<DragHandle> activeValues = [];

    [ObservableProperty] private IReadOnlyList<ToyRow> rows = [];
    [ObservableProperty] private IReadOnlyList<LabeledValue> weightBars = [];
    [ObservableProperty] private double[] weights = [];
    [ObservableProperty] private string scaleText = "";
    [ObservableProperty] private string softmaxText = "";
    [ObservableProperty] private string answerText = "";
    [ObservableProperty] private double answerX;
    [ObservableProperty] private double answerY;

    partial void OnKeyCountChanged(int value)
    {
        if (value < MinKeys || value > MaxKeys) { KeyCount = Math.Clamp(value, MinKeys, MaxKeys); return; }
        Recompute();
    }

    [RelayCommand(CanExecute = nameof(CanAddKey))]
    private void AddKey() => KeyCount++;
    private bool CanAddKey() => KeyCount < MaxKeys;

    [RelayCommand(CanExecute = nameof(CanRemoveKey))]
    private void RemoveKey() => KeyCount--;
    private bool CanRemoveKey() => KeyCount > MinKeys;

    /// <summary>Back to the starting vectors and three keys.</summary>
    public void Reset()
    {
        _updating = true;
        try
        {
            Query.X = DefaultQuery.X; Query.Y = DefaultQuery.Y;
            for (int i = 0; i < MaxKeys; i++)
            {
                _keys[i].X = DefaultKeyVectors[i].X; _keys[i].Y = DefaultKeyVectors[i].Y;
                _values[i].X = DefaultValueVectors[i].X; _values[i].Y = DefaultValueVectors[i].Y;
            }
            KeyCount = DefaultKeys;
        }
        finally { _updating = false; }
        Recompute();
    }

    private void Recompute()
    {
        if (_updating) return;
        int n = KeyCount;
        var q = Tensor.FromArray([Query.X, Query.Y], 1, Width);
        var k = Tensor.FromArray(_keys.Take(n).SelectMany(h => new[] { h.X, h.Y }).ToArray(), n, Width);
        var v = Tensor.FromArray(_values.Take(n).SelectMany(h => new[] { h.X, h.Y }).ToArray(), n, Width);

        var dots = Ops.MatMul(q, Ops.Transpose(k));          // [1, n]: one dot product per key
        var scaled = Ops.Scale(dots, ScaleFactor);           // divide by √d, d = 2
        var softmax = Ops.SoftmaxRows(scaled);               // weights that add up to 1
        var answer = Ops.MatMul(softmax, v);                 // [1, 2]: the weighted sum of the values

        double sumOfExponentials = 0;
        var exponentials = new double[n];
        for (int i = 0; i < n; i++) sumOfExponentials += exponentials[i] = Math.Exp(scaled.Data[i]);

        var list = new List<ToyRow>(n);
        for (int i = 0; i < n; i++)
        {
            double w = softmax.Data[i];
            var share = Ops.Scale(Tensor.FromArray([v.Data[i * Width], v.Data[i * Width + 1]], 1, Width), w);
            list.Add(new ToyRow(
                _keys[i].Label,
                $"{NumberText.Short(Query.X)} × {NumberText.Short(_keys[i].X)} + {NumberText.Short(Query.Y)} × {NumberText.Short(_keys[i].Y)}",
                dots.Data[i], scaled.Data[i], exponentials[i], w,
                $"{NumberText.Short(exponentials[i])} ÷ {NumberText.Short(sumOfExponentials)}",
                $"{NumberText.Short(w)} × {_values[i].Label} ({NumberText.Short(_values[i].X)}, {NumberText.Short(_values[i].Y)}) = ({NumberText.Short(share.Data[0])}, {NumberText.Short(share.Data[1])})"));
        }

        Rows = list;
        Weights = softmax.Data.ToArray();
        WeightBars = list.Select(r => new LabeledValue(r.Name, r.Weight)).ToList();
        ScaleText = $"Divide each dot product by √{Width} (the size of each vector is d = {Width}): multiply by 1/√{Width} = {NumberText.Short(ScaleFactor)}.";
        SoftmaxText = $"Softmax: raise e to each score, then divide by their total, {NumberText.Short(sumOfExponentials)}. The weights add up to {NumberText.Short(softmax.Data.Sum())}.";
        AnswerX = answer.Data[0];
        AnswerY = answer.Data[1];
        AnswerText = $"Answer = the shares added up = ({NumberText.Short(AnswerX)}, {NumberText.Short(AnswerY)})";
        Output.X = AnswerX;
        Output.Y = AnswerY;

        ActiveKeys = _keys.Take(n).ToList();
        ActiveValues = _values.Take(n).ToList();
        KeyPlotHandles = new[] { Query }.Concat(ActiveKeys).ToList();
        ValuePlotHandles = ActiveValues.Concat(new[] { Output }).ToList();
        AddKeyCommand.NotifyCanExecuteChanged();
        RemoveKeyCommand.NotifyCanExecuteChanged();
    }
}
