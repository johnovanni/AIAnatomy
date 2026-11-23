using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Visuals;

/// <summary>
/// A point on a <c>DragPlot</c> that can be moved by dragging, by the arrow keys or by typing its two numbers.
/// X and Y are kept inside −<see cref="Limit"/>..+<see cref="Limit"/> (a number that is not a number becomes 0).
/// A fixed handle (such as a computed result) is drawn but not moved by the user.
/// </summary>
public sealed class DragHandle : ObservableObject
{
    public const double DefaultLimit = 3;

    private double _x, _y;

    public DragHandle(string label, double x, double y, bool isFixed = false, double limit = DefaultLimit)
    {
        Label = label;
        Limit = limit;
        IsFixed = isFixed;
        _x = Clamp(x);
        _y = Clamp(y);
    }

    public string Label { get; }
    public double Limit { get; }
    public bool IsFixed { get; }

    public double X { get => _x; set => Set(ref _x, value, nameof(X)); }
    public double Y { get => _y; set => Set(ref _y, value, nameof(Y)); }

    /// <summary>Stores the clamped number. When clamping changed what was asked for, the change is announced anyway, so a box showing the typed number is corrected.</summary>
    private void Set(ref double field, double asked, string name)
    {
        double value = Clamp(asked);
        if (!SetProperty(ref field, value, name) && value != asked) OnPropertyChanged(name);
    }

    private double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, -Limit, Limit);
}
