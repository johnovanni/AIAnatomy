using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A small square plot of two-dimensional vectors, each drawn as an arrow from the centre (0, 0) to a labelled handle.
/// Drag a handle with the mouse, or select it (click, or Page Up / Page Down) and move it with the arrow keys
/// (Shift = bigger steps). Handles marked fixed are drawn as diamonds and cannot be moved. The numbers are always
/// shown in the readout under the plot; typing them into boxes is the other way to move a point.
/// </summary>
public sealed class DragPlot : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double BaseMinPlotHeight = 320, BaseSide = 420, Gap = 8, HitRadius = 18, Snap = 0.05, SmallStep = 0.05, BigStep = 0.5;

    // The plot grows with the text (final review I6): at the default 20 pt the side is about 420 px; at 32 pt about 680 px,
    // so the labels and axis numbers, which grow with the text, keep their room.
    private double MaxSide => Math.Max(BaseSide, LabelSize * 20);
    private double MinPlotHeight => Math.Max(BaseMinPlotHeight, LabelSize * 15);

    public static readonly DependencyProperty HandlesProperty = DependencyProperty.Register(nameof(Handles),
        typeof(IReadOnlyList<DragHandle>), typeof(DragPlot), new FrameworkPropertyMetadata(null, Relayout, OnHandlesChanged));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int),
        typeof(DragPlot), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | Relayout));

    public static readonly DependencyProperty XAxisTitleProperty = DependencyProperty.Register(nameof(XAxisTitle), typeof(string), typeof(DragPlot),
        new FrameworkPropertyMetadata(null, Relayout));
    public static readonly DependencyProperty YAxisTitleProperty = DependencyProperty.Register(nameof(YAxisTitle), typeof(string), typeof(DragPlot),
        new FrameworkPropertyMetadata(null, Relayout));

    private int _dragging = -1;

    public DragPlot()
    {
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 200);
    }

    public IReadOnlyList<DragHandle>? Handles { get => (IReadOnlyList<DragHandle>?)GetValue(HandlesProperty); set => SetValue(HandlesProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public string? XAxisTitle { get => (string?)GetValue(XAxisTitleProperty); set => SetValue(XAxisTitleProperty, value); }
    public string? YAxisTitle { get => (string?)GetValue(YAxisTitleProperty); set => SetValue(YAxisTitleProperty, value); }

    private bool HasData => Handles is { Count: > 0 };
    private double Range => Handles!.Max(h => h.Limit);

    private static void OnHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var plot = (DragPlot)d;
        if (e.OldValue is IReadOnlyList<DragHandle> old) foreach (var h in old) h.PropertyChanged -= plot.OnHandleChanged;
        if (e.NewValue is IReadOnlyList<DragHandle> now) foreach (var h in now) h.PropertyChanged += plot.OnHandleChanged;
        if (plot.SelectedIndex >= (plot.Handles?.Count ?? 0)) plot.SelectedIndex = -1;
    }

    private void OnHandleChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        double side = Math.Min(FiniteOr(available.Width, MaxSide), MaxSide);
        return new Size(side, Math.Max(side, MinPlotHeight) + Gap + ReadoutHeight(side));
    }

    private (AxisFrame Frame, Rect Bounds, double ReadoutTop) ComputeLayout()
    {
        double readout = ReadoutHeight(ActualWidth);
        var bounds = new Rect(0, 0, ActualWidth, Math.Max(40, ActualHeight - readout - Gap));
        double r = Range;
        return (LayoutAxes(bounds, -r, r, -r, r, XAxisTitle, YAxisTitle), bounds, ActualHeight - readout);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        var handles = Handles!;
        var (frame, bounds, readoutTop) = ComputeLayout();
        DrawAxes(dc, frame, bounds);

        var centre = frame.Map(0, 0);
        var zeroPen = new Pen(AxisBrush, 1) { DashStyle = DashStyles.Dash };
        dc.DrawLine(zeroPen, new Point(frame.Plot.Left, centre.Y), new Point(frame.Plot.Right, centre.Y));
        dc.DrawLine(zeroPen, new Point(centre.X, frame.Plot.Top), new Point(centre.X, frame.Plot.Bottom));

        double marker = Math.Max(8, LabelSize * 0.5);
        var labelSpots = PlaceLabels(handles, frame, marker, readoutTop);
        for (int i = 0; i < handles.Count; i++)
        {
            var h = handles[i];
            var brush = SeriesBrush(i);
            var at = frame.Map(h.X, h.Y);
            var line = new Pen(brush, 2);
            if (h.IsFixed) line.DashStyle = DashStyles.Dot;
            dc.DrawLine(line, centre, at);

            var outline = new Pen(SurfaceBrush, 1.5);
            if (h.IsFixed)
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(at.X, at.Y - marker * 1.2), true, true);
                    c.PolyLineTo([new Point(at.X + marker * 1.2, at.Y), new Point(at.X, at.Y + marker * 1.2), new Point(at.X - marker * 1.2, at.Y)], true, true);
                }
                g.Freeze();
                dc.DrawGeometry(brush, outline, g);
            }
            else dc.DrawEllipse(brush, outline, at, marker, marker);

            if (i == SelectedIndex)
            {
                dc.DrawEllipse(null, new Pen(HighlightBrush, 3), at, marker + 6, marker + 6);
                if (IsKeyboardFocused) dc.DrawEllipse(null, new Pen(FocusBrush, 1.5) { DashStyle = DashStyles.Dash }, at, marker + 10, marker + 10);
            }

            dc.DrawText(LabelText(i), labelSpots[i]);
        }

        dc.DrawText(ReadoutText(ActualWidth), new Point(0, readoutTop + Gap));
    }

    private FormattedText LabelText(int i) => Text(Handles![i].Label, LabelSize, Foreground, i == SelectedIndex ? BoldFace : TextFace);

    /// <summary>
    /// Where each label goes: right of its point, else left, above or below, then the corners, taking the first place that
    /// stays inside the plot and covers no earlier label, no point and no arrow from the origin, its own included (final
    /// review I6 and the residual batch). If no place is free, the one that covers least is used (<see cref="LabelPlacement"/>).
    /// </summary>
    private Point[] PlaceLabels(IReadOnlyList<DragHandle> handles, AxisFrame frame, double marker, double readoutTop)
    {
        var points = handles.Select(h => frame.Map(h.X, h.Y)).ToArray();
        var centre = frame.Map(0, 0);
        var obstacles = points.Select(p => new LabelBox(p.X - marker * 1.2, p.Y - marker * 1.2, marker * 2.4, marker * 2.4)).ToList();
        var lines = points.Select(p => new LabelLine(centre.X, centre.Y, p.X, p.Y)).ToList();
        var result = new Point[handles.Count];
        double bottom = Math.Max(0, readoutTop);
        for (int i = 0; i < handles.Count; i++)
        {
            var text = LabelText(i);
            var at = points[i];
            double w = text.Width, h = text.Height, d = marker + 6;
            (double X, double Y)[] candidates =
            [
                (at.X + d, at.Y - h / 2),              // right
                (at.X - d - w, at.Y - h / 2),          // left
                (at.X - w / 2, at.Y - d - h),          // above
                (at.X - w / 2, at.Y + d),              // below
                (at.X + d, at.Y - d - h),              // above right
                (at.X + d, at.Y + d / 2),              // below right
                (at.X - d - w, at.Y - d - h),          // above left
                (at.X - d - w, at.Y + d / 2),          // below left
            ];
            var (x, y) = LabelPlacement.Choose(candidates, w, h, ActualWidth, bottom, obstacles, lines);
            obstacles.Add(new LabelBox(x, y, w, h));
            result[i] = new Point(x, y);
        }
        return result;
    }

    private string Describe(int i)
    {
        var h = Handles![i];
        return $"{h.Label}: x = {NumberText.Exact(h.X)}, y = {NumberText.Exact(h.Y)}" + (h.IsFixed ? " (worked out, not movable)" : "");
    }

    private const string Hint = "Drag a point, or click one (Page Up and Page Down pick another) and use the arrow keys. The boxes hold the same numbers.";

    private FormattedText ReadoutText(double width)
    {
        string text = HasData && SelectedIndex >= 0 && SelectedIndex < Handles!.Count ? Describe(SelectedIndex) : Hint;
        return Text(text, FontSize * 0.9, Foreground, maxWidth: Math.Max(50, FiniteOr(width, 300)));
    }

    /// <summary>Room kept for the readout: always the (longer) hint, so the plot does not jump when a point is selected.</summary>
    private double ReadoutHeight(double width) =>
        Math.Max(ReadoutText(width).Height, Text(Hint, FontSize * 0.9, Foreground, maxWidth: Math.Max(50, FiniteOr(width, 300))).Height);

    /// <summary>The nearest multiple of 0.05, written without floating-point dust (0.15, not 0.15000000000000002).</summary>
    private static double Snapped(double v) => Math.Round(Math.Round(v / Snap) * Snap, 2);

    private int HandleAt(Point at)
    {
        if (!HasData || ActualWidth <= 0) return -1;
        var frame = ComputeLayout().Frame;
        int best = -1;
        double bestDistance = HitRadius;
        for (int i = 0; i < Handles!.Count; i++)
        {
            double d = (frame.Map(Handles[i].X, Handles[i].Y) - at).Length;
            if (d <= bestDistance) { best = i; bestDistance = d; }
        }
        return best;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        int i = HandleAt(e.GetPosition(this));
        if (i >= 0)
        {
            SelectedIndex = i;
            if (!Handles![i].IsFixed)
            {
                _dragging = i;
                CaptureMouse();
            }
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging >= 0 && HasData && _dragging < Handles!.Count)
        {
            var frame = ComputeLayout().Frame;
            var p = e.GetPosition(this);
            double x = frame.XMin + (p.X - frame.Plot.Left) / frame.Plot.Width * (frame.XMax - frame.XMin);
            double y = frame.YMin + (frame.Plot.Bottom - p.Y) / frame.Plot.Height * (frame.YMax - frame.YMin);
            Handles[_dragging].X = Snapped(x);
            Handles[_dragging].Y = Snapped(y);
            return;
        }
        int i = HandleAt(e.GetPosition(this));
        ToolTip = i >= 0 ? Describe(i) : null;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging >= 0) { _dragging = -1; ReleaseMouseCapture(); }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragging = -1;   // the capture was taken away (window deactivated, popup, ...): the drag is over
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HasData) return;
        int count = Handles!.Count;
        switch (e.Key)
        {
            case Key.PageDown:
                SelectedIndex = SelectedIndex < 0 ? 0 : (SelectedIndex + 1) % count;
                e.Handled = true;
                return;
            case Key.PageUp:
                SelectedIndex = SelectedIndex < 0 ? count - 1 : (SelectedIndex - 1 + count) % count;
                e.Handled = true;
                return;
        }
        if (SelectedIndex < 0 || SelectedIndex >= count || Handles[SelectedIndex].IsFixed) return;
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? BigStep : SmallStep;
        var h = Handles[SelectedIndex];
        switch (e.Key)
        {
            case Key.Left: h.X = Snapped(h.X - step); break;
            case Key.Right: h.X = Snapped(h.X + step); break;
            case Key.Up: h.Y = Snapped(h.Y + step); break;
            case Key.Down: h.Y = Snapped(h.Y - step); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (SelectedIndex < 0 && HasData) SelectedIndex = 0;
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }
}
