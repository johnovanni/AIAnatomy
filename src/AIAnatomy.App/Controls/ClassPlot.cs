using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Models.ClassicMl;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>A place on the plot, in plot units, that the learner clicked or chose with the keyboard cross.</summary>
public sealed class PlotPlaceEventArgs(double x, double y) : EventArgs
{
    public double X { get; } = x;
    public double Y { get; } = y;
}

/// <summary>
/// The classic machine learning lab's plot (lesson 4.4): a 0 to 10 square with points of three classes, the regions a
/// method gives each class shaded faintly in the class colour, and a decision tree's splits as lines. Classes differ in
/// shape as well as colour: A is a circle, B a square, C a triangle, named in the legend. A click asks for a point at that
/// place (a click on an existing point only moves the cross onto it) and a right-click asks to remove the nearest (the view
/// passes both to the lab). From the keyboard the arrow keys
/// move a cross by 0.5 (0.1 with Shift), Enter or Space adds a point there and Delete removes the nearest.
/// </summary>
public sealed class ClassPlot : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double Min = ClassicMlLabViewModel.PlotMin, Max = ClassicMlLabViewModel.PlotMax;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(IEnumerable),
        typeof(ClassPlot), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));
    public static readonly DependencyProperty RegionsProperty = Register<IReadOnlyList<Region>?>(nameof(Regions), null);
    public static readonly DependencyProperty SplitLinesProperty =
        Register<IReadOnlyList<(Axis Axis, double At, double From, double To)>?>(nameof(SplitLines), null);
    public static readonly DependencyProperty ShadeOpacityProperty = Register(nameof(ShadeOpacity), 0.25);
    public static readonly DependencyProperty ClassABrushProperty = Register<Brush>(nameof(ClassABrush), Brushes.SteelBlue);
    public static readonly DependencyProperty ClassBBrushProperty = Register<Brush>(nameof(ClassBBrush), Brushes.DarkOrange);
    public static readonly DependencyProperty ClassCBrushProperty = Register<Brush>(nameof(ClassCBrush), Brushes.MediumVioletRed);

    private double _crossX = 5, _crossY = 5;

    public ClassPlot()
    {
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 200);
        SetResourceReference(ClassABrushProperty, "ClassABrush");
        SetResourceReference(ClassBBrushProperty, "ClassBBrush");
        SetResourceReference(ClassCBrushProperty, "ClassCBrush");
        AutomationProperties.SetHelpText(this, CrossText);
    }

    /// <summary>The points (a collection of <see cref="LabeledPoint"/>; an observable one redraws when it changes).</summary>
    public IEnumerable? Points { get => (IEnumerable?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public IReadOnlyList<Region>? Regions { get => (IReadOnlyList<Region>?)GetValue(RegionsProperty); set => SetValue(RegionsProperty, value); }
    public IReadOnlyList<(Axis Axis, double At, double From, double To)>? SplitLines
    {
        get => (IReadOnlyList<(Axis, double, double, double)>?)GetValue(SplitLinesProperty);
        set => SetValue(SplitLinesProperty, value);
    }
    /// <summary>How strongly the regions are shaded in their class colour (faint, so points and lines stay clear).</summary>
    public double ShadeOpacity { get => (double)GetValue(ShadeOpacityProperty); set => SetValue(ShadeOpacityProperty, value); }
    public Brush ClassABrush { get => (Brush)GetValue(ClassABrushProperty); set => SetValue(ClassABrushProperty, value); }
    public Brush ClassBBrush { get => (Brush)GetValue(ClassBBrushProperty); set => SetValue(ClassBBrushProperty, value); }
    public Brush ClassCBrush { get => (Brush)GetValue(ClassCBrushProperty); set => SetValue(ClassCBrushProperty, value); }

    /// <summary>A click (or Enter on the cross) asks for a point here.</summary>
    public event EventHandler<PlotPlaceEventArgs>? AddRequested;

    /// <summary>A right-click (or Delete on the cross) asks for the nearest point to go.</summary>
    public event EventHandler<PlotPlaceEventArgs>? RemoveRequested;

    /// <summary>Where the keyboard cross is, in words (the control's help text for screen readers, and shown under the plot while focused).</summary>
    public string CrossText => CrossTextFor(ClassicMlLabViewModel.DeleteText(PointList, _crossX, _crossY));

    // The cross line with the given Delete part: "Cross at (5, 5). Arrow keys … Delete removes the class B point at (6.2, 7)."
    private string CrossTextFor(string deleteText) =>
        $"Cross at ({NumberText.Tick(_crossX)}, {NumberText.Tick(_crossY)}). Arrow keys move it (with Shift, in small steps); Enter adds a point there. {deleteText}";

    // The longest the cross line can be, so its room is kept whatever it says.
    private string LongestCrossText => CrossTextFor("Delete removes the class C point at (10.0, 10.0).");

    private List<LabeledPoint> PointList => Points?.OfType<LabeledPoint>().ToList() ?? [];

    private Brush ClassBrush(int label) => label switch { 0 => ClassABrush, 1 => ClassBBrush, _ => ClassCBrush };

    private double Marker => Math.Max(6, LabelSize * 0.45);

    private double LineHeight => Text("Ag", LabelSize, Foreground).Height;

    protected override Size MeasureOverride(Size available)
    {
        double width = FiniteOr(available.Width, 560);
        double side = Math.Min(width, Math.Max(380, FontSize * 30));
        return new Size(width, DrawLegend(null, width) + 8 + side + ReadoutHeight(width));
    }

    /// <summary>The line under the plot when it does not have the keyboard.</summary>
    public const string HintText = "Point at a point to see its class and place. Tab to the plot to use the keyboard.";

    private string ReadoutText => IsKeyboardFocused ? CrossText : HintText;

    // Room for the longer of the two lines, so the plot does not move when the keyboard arrives.
    private double ReadoutHeight(double width) =>
        Math.Max(Text(LongestCrossText, LabelSize, MutedBrush, maxWidth: Math.Max(50, width)).Height,
                 Text(HintText, LabelSize, MutedBrush, maxWidth: Math.Max(50, width)).Height) + 6;

    private AxisFrame Frame() => Layout().Frame;

    private (AxisFrame Frame, Rect Bounds) Layout()
    {
        double legend = DrawLegend(null, ActualWidth) + 8;
        double height = Math.Max(60, ActualHeight - legend - ReadoutHeight(ActualWidth));
        // About square: the y labels and the axis titles take a little width beside the plot.
        var bounds = new Rect(0, legend, Math.Min(ActualWidth, height + LabelSize * 4), height);
        return (LayoutAxes(bounds, Min, Max, Min, Max, "x", "y"), bounds);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        var (frame, bounds) = Layout();
        DrawLegend(dc, ActualWidth);
        DrawAxes(dc, frame, bounds);

        dc.PushClip(new RectangleGeometry(frame.Plot));
        DrawRegions(dc, frame);
        var linePen = new Pen(Foreground, 2.5);
        foreach (var (axis, at, from, to) in SplitLines ?? [])
        {
            if (axis == Axis.X) dc.DrawLine(linePen, frame.Map(at, from), frame.Map(at, to));
            else dc.DrawLine(linePen, frame.Map(from, at), frame.Map(to, at));
        }
        dc.Pop();   // points on the edge of the plot are drawn whole
        var outline = new Pen(Foreground, 1.5);
        foreach (var p in PointList) DrawMarker(dc, p.Label, frame.Map(p.X, p.Y), Marker, ClassBrush(p.Label), outline);

        if (IsKeyboardFocused)
        {
            var at = frame.Map(_crossX, _crossY);
            var pen = new Pen(FocusBrush, 2.5);
            double arm = Marker * 1.6;
            dc.DrawLine(pen, new Point(at.X - arm, at.Y), new Point(at.X + arm, at.Y));
            dc.DrawLine(pen, new Point(at.X, at.Y - arm), new Point(at.X, at.Y + arm));
            // The reach of Delete: points inside this ring can be removed from here.
            double reach = ClassicMlLabViewModel.RemoveRadius / (Max - Min) * frame.Plot.Width;
            dc.DrawEllipse(null, new Pen(FocusBrush, 1.5) { DashStyle = DashStyles.Dash }, at, reach, reach);
        }
        var readout = Text(ReadoutText, LabelSize, MutedBrush, maxWidth: Math.Max(50, ActualWidth));
        dc.DrawText(readout, new Point(0, ActualHeight - ReadoutHeight(ActualWidth) + 6));
    }

    /// <summary>Each class's regions as one shape, filled once, so neighbouring squares of a class show no seams.</summary>
    private void DrawRegions(DrawingContext dc, AxisFrame frame)
    {
        var regions = Regions;
        if (regions is not { Count: > 0 }) return;
        foreach (var group in regions.GroupBy(r => r.Label))
        {
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var g = geometry.Open())
                foreach (var r in group)
                {
                    var a = frame.Map(r.XMin, r.YMax);
                    var b = frame.Map(r.XMax, r.YMin);
                    g.BeginFigure(a, true, true);
                    g.PolyLineTo([new Point(b.X, a.Y), b, new Point(a.X, b.Y)], false, false);
                }
            geometry.Freeze();
            var colour = (ClassBrush(group.Key) as SolidColorBrush)?.Color ?? Colors.Gray;
            var fill = new SolidColorBrush(colour) { Opacity = ShadeOpacity };
            fill.Freeze();
            dc.DrawGeometry(fill, null, geometry);
        }
    }

    /// <summary>Draws the legend (when <paramref name="dc"/> is not null), wrapping to the width, and returns its height.</summary>
    private double DrawLegend(DrawingContext? dc, double width)
    {
        double x = 0, y = 0, line = LineHeight, r = Marker;
        var outline = new Pen(Foreground, 1.5);
        for (int label = 0; label < ClassicMlLabViewModel.ClassCount; label++)
        {
            var text = Text($"Class {ClassicMlLabViewModel.ClassName(label)} ({ClassicMlLabViewModel.ShapeName(label)})", LabelSize, Foreground);
            double itemWidth = 2 * r + 10 + text.Width;
            if (x > 0 && x + itemWidth > width) { x = 0; y += line + 4; }
            if (dc != null)
            {
                DrawMarker(dc, label, new Point(x + r + 2, y + line / 2), r, ClassBrush(label), outline);
                dc.DrawText(text, new Point(x + 2 * r + 10, y));
            }
            x += itemWidth + 24;
        }
        return y + line;
    }

    /// <summary>Circle (A), square (B), triangle (C): never colour alone.</summary>
    private static void DrawMarker(DrawingContext dc, int label, Point c, double r, Brush fill, Pen outline)
    {
        switch (label)
        {
            case 0: dc.DrawEllipse(fill, outline, c, r, r); break;
            case 1: dc.DrawRectangle(fill, outline, new Rect(c.X - r * 0.88, c.Y - r * 0.88, r * 1.76, r * 1.76)); break;
            default:
                var triangle = new StreamGeometry();
                using (var g = triangle.Open())
                {
                    g.BeginFigure(new Point(c.X, c.Y - r * 1.2), true, true);
                    g.PolyLineTo([new Point(c.X + r * 1.1, c.Y + r * 0.85), new Point(c.X - r * 1.1, c.Y + r * 0.85)], true, true);
                }
                triangle.Freeze();
                dc.DrawGeometry(fill, outline, triangle);
                break;
        }
    }

    private bool TryPlace(Point at, out double x, out double y)
    {
        var f = Frame();
        x = f.XMin + (at.X - f.Plot.Left) / f.Plot.Width * (f.XMax - f.XMin);
        y = f.YMin + (f.Plot.Bottom - at.Y) / f.Plot.Height * (f.YMax - f.YMin);
        return f.Plot.Contains(at);
    }

    private LabeledPoint? PointAt(Point at)
    {
        var f = Frame();
        LabeledPoint? best = null;
        double bestDistance = Marker + 4;
        foreach (var p in PointList)
        {
            double d = (f.Map(p.X, p.Y) - at).Length;
            if (d <= bestDistance) { best = p; bestDistance = d; }
        }
        return best;
    }

    private void MoveCross(double x, double y)
    {
        _crossX = Math.Round(Math.Clamp(x, Min, Max), 1);
        _crossY = Math.Round(Math.Clamp(y, Min, Max), 1);
        AutomationProperties.SetHelpText(this, CrossText);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var at = e.GetPosition(this);
        if (PointAt(at) is { } hit) MoveCross(hit.X, hit.Y);   // a click on a point only puts the cross on it: no second point there
        else if (TryPlace(at, out double x, out double y))
        {
            MoveCross(x, y);
            AddRequested?.Invoke(this, new PlotPlaceEventArgs(x, y));
        }
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        Focus();
        var at = e.GetPosition(this);
        if (TryPlace(at, out double x, out double y))
        {
            // The point under the pointer if there is one (its marker may reach past 0.3 plot units), else the place itself.
            if (PointAt(at) is { } hit) (x, y) = (hit.X, hit.Y);
            MoveCross(x, y);
            RemoveRequested?.Invoke(this, new PlotPlaceEventArgs(x, y));
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        ToolTip = PointAt(e.GetPosition(this)) is { } p ? ClassicMlLabViewModel.PointText(p) : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1 : 0.5;
        switch (e.Key)
        {
            case Key.Left: MoveCross(_crossX - step, _crossY); break;
            case Key.Right: MoveCross(_crossX + step, _crossY); break;
            case Key.Up: MoveCross(_crossX, _crossY + step); break;
            case Key.Down: MoveCross(_crossX, _crossY - step); break;
            case Key.Enter or Key.Space: AddRequested?.Invoke(this, new PlotPlaceEventArgs(_crossX, _crossY)); break;
            case Key.Delete or Key.Back: RemoveRequested?.Invoke(this, new PlotPlaceEventArgs(_crossX, _crossY)); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusedChanged(e);
        InvalidateVisual();   // the cross shows only while the plot has the keyboard
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var plot = (ClassPlot)d;
        if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= plot.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged now) now.CollectionChanged += plot.OnCollectionChanged;
        AutomationProperties.SetHelpText(plot, plot.CrossText);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AutomationProperties.SetHelpText(this, CrossText);   // what Delete would remove may have changed
        InvalidateVisual();
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(ClassPlot), new FrameworkPropertyMetadata(fallback, Relayout));
}
