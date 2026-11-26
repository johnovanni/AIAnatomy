using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A cloud of 2-D points (as in the diffusion lab) on square axes. Everything shown is told apart by shape and line style
/// as well as colour: the target shape, faint, as small hollow rings; the current points as solid squares; highlighted
/// points (<see cref="HighlightIndices"/>) as large outlined circles around their squares; arrows (such as the true
/// noise) as solid lines with an open head; and a second set of arrows (such as the predicted noise) as dashed lines with
/// a filled head. The legend names each one with its style in words.
/// The axes run from −<see cref="AxisLimit"/> to +<see cref="AxisLimit"/> (3 by default), or fit the data when
/// <see cref="FitToData"/> is set. The line under the plot gives the number of points, where their middle is and how
/// spread out they are, in words, at the full text size. It changes only with the data, never from a key, so it is not
/// a live region (during sampling it would be read out many times a second); it is the plot's help text instead, read
/// when the keyboard reaches the plot, whose name is "Point cloud" unless a lab sets another.
/// </summary>
public sealed class PointCloudPlot : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double Gap = 8, UnboundedSide = 420;

    public static readonly DependencyProperty PointsProperty = Register<IReadOnlyList<Point>?>(nameof(Points), null);
    public static readonly DependencyProperty TargetPointsProperty = Register<IReadOnlyList<Point>?>(nameof(TargetPoints), null);
    public static readonly DependencyProperty ArrowsProperty = Register<IReadOnlyList<(Point From, Point To)>?>(nameof(Arrows), null);
    public static readonly DependencyProperty PointsLabelProperty = Register(nameof(PointsLabel), "points");
    public static readonly DependencyProperty TargetLabelProperty = Register(nameof(TargetLabel), "target shape");
    public static readonly DependencyProperty ArrowsLabelProperty = Register(nameof(ArrowsLabel), "noise");
    public static readonly DependencyProperty SecondaryArrowsProperty = Register<IReadOnlyList<(Point From, Point To)>?>(nameof(SecondaryArrows), null);
    public static readonly DependencyProperty SecondaryArrowsLabelProperty = Register(nameof(SecondaryArrowsLabel), "predicted noise");
    public static readonly DependencyProperty HighlightIndicesProperty = Register<IReadOnlyList<int>?>(nameof(HighlightIndices), null);
    public static readonly DependencyProperty HighlightLabelProperty = Register(nameof(HighlightLabel), "highlighted points");
    public static readonly DependencyProperty AxisLimitProperty = Register(nameof(AxisLimit), 3.0);
    public static readonly DependencyProperty FitToDataProperty = Register(nameof(FitToData), false);
    public static readonly DependencyProperty PreferredPlotSideProperty = Register(nameof(PreferredPlotSide), UnboundedSide);
    public static readonly DependencyProperty TargetRingBrushProperty = DependencyProperty.Register(nameof(TargetRingBrush), typeof(Brush),
        typeof(PointCloudPlot), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public PointCloudPlot()
    {
        SetResourceReference(TargetRingBrushProperty, "TargetRingBrush");
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 200);
        AutomationProperties.SetName(this, "Point cloud");   // a lab may set its own name
        AutomationProperties.SetHelpText(this, SummaryText());
    }

    /// <summary>The colour of the target shape's rings: a solid theme colour, lighter than the points but at least 3:1 on every
    /// background (AA-043; it used to be the muted text colour at 55% opacity, which fell to about 2.6:1 in Light).</summary>
    public Brush TargetRingBrush { get => (Brush)GetValue(TargetRingBrushProperty); set => SetValue(TargetRingBrushProperty, value); }

    /// <summary>The current points, drawn as solid squares.</summary>
    public IReadOnlyList<Point>? Points { get => (IReadOnlyList<Point>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    /// <summary>The shape being learned, drawn faintly as hollow rings behind everything else.</summary>
    public IReadOnlyList<Point>? TargetPoints { get => (IReadOnlyList<Point>?)GetValue(TargetPointsProperty); set => SetValue(TargetPointsProperty, value); }
    /// <summary>Arrows from one place to another (such as the true noise vectors): solid lines with an open head.</summary>
    public IReadOnlyList<(Point From, Point To)>? Arrows { get => (IReadOnlyList<(Point From, Point To)>?)GetValue(ArrowsProperty); set => SetValue(ArrowsProperty, value); }
    /// <summary>The legend's name for the current points.</summary>
    public string PointsLabel { get => (string)GetValue(PointsLabelProperty); set => SetValue(PointsLabelProperty, value); }
    /// <summary>The legend's name for the target shape.</summary>
    public string TargetLabel { get => (string)GetValue(TargetLabelProperty); set => SetValue(TargetLabelProperty, value); }
    /// <summary>The legend's name for the arrows.</summary>
    public string ArrowsLabel { get => (string)GetValue(ArrowsLabelProperty); set => SetValue(ArrowsLabelProperty, value); }
    /// <summary>A second set of arrows (such as the predicted noise): dashed lines with a filled head.</summary>
    public IReadOnlyList<(Point From, Point To)>? SecondaryArrows { get => (IReadOnlyList<(Point From, Point To)>?)GetValue(SecondaryArrowsProperty); set => SetValue(SecondaryArrowsProperty, value); }
    /// <summary>The legend's name for the second set of arrows.</summary>
    public string SecondaryArrowsLabel { get => (string)GetValue(SecondaryArrowsLabelProperty); set => SetValue(SecondaryArrowsLabelProperty, value); }
    /// <summary>Indices into <see cref="Points"/> to draw with a large outlined circle, so a few points stand out from the
    /// cloud (indices outside the list are ignored).</summary>
    public IReadOnlyList<int>? HighlightIndices { get => (IReadOnlyList<int>?)GetValue(HighlightIndicesProperty); set => SetValue(HighlightIndicesProperty, value); }
    /// <summary>The legend's name for the highlighted points.</summary>
    public string HighlightLabel { get => (string)GetValue(HighlightLabelProperty); set => SetValue(HighlightLabelProperty, value); }
    /// <summary>The axes run from −AxisLimit to +AxisLimit on both sides (3 by default) unless <see cref="FitToData"/>.</summary>
    public double AxisLimit { get => (double)GetValue(AxisLimitProperty); set => SetValue(AxisLimitProperty, value); }
    /// <summary>The side the square plot (with its axis labels) asks for, at most the width it is given: 420 by default, or
    /// 18 font sizes when that is more. A lab whose finished shape is small on axes from −3 to 3 asks for more.</summary>
    public double PreferredPlotSide { get => (double)GetValue(PreferredPlotSideProperty); set => SetValue(PreferredPlotSideProperty, value); }
    /// <summary>Fit the axes to everything shown instead (the same range on both, so the shape is not stretched).</summary>
    public bool FitToData { get => (bool)GetValue(FitToDataProperty); set => SetValue(FitToDataProperty, value); }

    private bool HasData => Points is { Count: > 0 } || TargetPoints is { Count: > 0 } || Arrows is { Count: > 0 } || SecondaryArrows is { Count: > 0 };

    /// <summary>The highlighted points that exist (each once, in order).</summary>
    private List<Point> Highlighted => Points is { } points && HighlightIndices is { } indices
        ? indices.Where(i => i >= 0 && i < points.Count).Distinct().Select(i => points[i]).ToList()
        : [];

    /// <summary>The line under the plot: how many points, their middle and their spread (standard deviation along each axis).</summary>
    public string SummaryText()
    {
        var points = (Points ?? []).Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).ToList();
        if (points.Count == 0) return TargetPoints is { Count: > 0 } ? $"No {PointsLabel} yet; the faint rings show the {TargetLabel}." : "No points yet.";
        double mx = points.Average(p => p.X), my = points.Average(p => p.Y);
        double sx = Math.Sqrt(points.Average(p => (p.X - mx) * (p.X - mx))), sy = Math.Sqrt(points.Average(p => (p.Y - my) * (p.Y - my)));
        string count = points.Count == 1 ? $"1 of the {PointsLabel}" : $"{points.Count:#,0} {PointsLabel}";
        string text = $"{char.ToUpperInvariant(count[0])}{count[1..]}, centred at ({NumberText.Short(mx)}, {NumberText.Short(my)}); "
                      + $"spread (standard deviation) {NumberText.Short(sx)} across and {NumberText.Short(sy)} up and down.";
        int highlighted = Highlighted.Count;
        if (highlighted > 0) text += highlighted == 1 ? $" 1 of them is marked ({HighlightLabel})." : $" {highlighted:#,0} of them are marked ({HighlightLabel}).";
        if (!FitToData)
        {
            double limit = Limit;
            int outside = points.Count(p => Math.Abs(p.X) > limit || Math.Abs(p.Y) > limit);
            if (outside > 0) text += outside == 1 ? " 1 point is outside the plot." : $" {outside:#,0} points are outside the plot.";
        }
        return text;
    }

    private double Limit => double.IsFinite(AxisLimit) && AxisLimit > 0 ? AxisLimit : 3;

    private (double Min, double Max) Range()
    {
        if (!FitToData) return (-Limit, Limit);
        var all = (Points ?? []).Concat(TargetPoints ?? []).Concat((Arrows ?? []).Concat(SecondaryArrows ?? []).SelectMany(a => new[] { a.From, a.To }))
            .Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).ToList();
        if (all.Count == 0) return (-Limit, Limit);
        var (min, max) = AxisTicks.Pad(Math.Min(all.Min(p => p.X), all.Min(p => p.Y)), Math.Max(all.Max(p => p.X), all.Max(p => p.Y)));
        return (min, max);
    }

    /// <summary>
    /// The height asked for the axes and the square plot inside them, at <paramref name="width"/>. The square's side is the
    /// preferred side (at most the width) less the room the y labels take across, and the height adds the room the titles
    /// and x labels take down, so the square really is that wide; at a large text size the labels' room no longer shrinks
    /// the square to a fraction of the width.
    /// </summary>
    private double PlotHeightFor(double width)
    {
        double side = Math.Min(width, Math.Max(FiniteOr(PreferredPlotSide, UnboundedSide), FontSize * 18));
        var (min, max) = Range();
        var frame = LayoutAxes(new Rect(0, 0, width, side), min, max, min, max, "x", "y");
        double across = width - frame.Plot.Width, down = side - frame.Plot.Height;
        return Math.Max(60, side - across + down);
    }

    /// <summary>The square plot area at the control's current size (for checks of the layout).</summary>
    public Rect PlotArea => ComputeLayout(DrawLegend(null, ActualWidth), Readout(ActualWidth).Height).Frame.Plot;

    // The main way the numbers reach the learner, so at the full text size.
    private FormattedText Readout(double width) => Text(SummaryText(), FontSize, Foreground, maxWidth: Math.Max(50, FiniteOr(width, 600)));

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        double width = FiniteOr(available.Width, UnboundedSide + 80);
        double natural = DrawLegend(null, width) + Gap + PlotHeightFor(width) + Gap + Readout(width).Height;
        return new Size(width, double.IsFinite(available.Height) && available.Height > 0 ? Math.Min(available.Height, natural) : natural);
    }

    /// <summary>The axes, made square: both axes have the same range, so the plot area is square and a circle stays round.</summary>
    private (AxisFrame Frame, Rect Bounds) ComputeLayout(double legend, double readout)
    {
        var (min, max) = Range();
        var bounds = new Rect(0, legend + Gap, ActualWidth, Math.Max(60, ActualHeight - legend - Gap - Gap - readout));
        var frame = LayoutAxes(bounds, min, max, min, max, "x", "y");
        double excess = frame.Plot.Width - frame.Plot.Height;
        if (Math.Abs(excess) > 0.5)
        {
            // Narrow the bounds (centred) when the plot is wider than tall, or shorten them when it is taller than wide.
            bounds = excess > 0
                ? new Rect(bounds.X + excess / 2, bounds.Y, bounds.Width - excess, bounds.Height)
                : new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height + excess);
            frame = LayoutAxes(bounds, min, max, min, max, "x", "y");
        }
        return (frame, bounds);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        double legend = DrawLegend(dc, ActualWidth);
        var readout = Readout(ActualWidth);
        var (frame, bounds) = ComputeLayout(legend, readout.Height);
        DrawAxes(dc, frame, bounds);
        double r = MarkerRadius;

        dc.PushClip(new RectangleGeometry(frame.Plot));
        if (TargetPoints is { Count: > 0 } target)
        {
            dc.DrawGeometry(null, new Pen(TargetRingBrush, 1), Markers(target.Select(p => frame.Map(p.X, p.Y)), r, ring: true));
        }
        if (Points is { Count: > 0 } points)
            dc.DrawGeometry(Series1Brush, null, Markers(points.Select(p => frame.Map(p.X, p.Y)), r, ring: false));
        var highlighted = Highlighted;
        if (highlighted.Count > 0) DrawHighlights(dc, highlighted.Select(p => frame.Map(p.X, p.Y)), r);
        if (Arrows is { Count: > 0 } arrows)
            DrawArrows(dc, arrows.Select(a => (frame.Map(a.From.X, a.From.Y), frame.Map(a.To.X, a.To.Y))), r, secondary: false);
        if (SecondaryArrows is { Count: > 0 } second)
            DrawArrows(dc, second.Select(a => (frame.Map(a.From.X, a.From.Y), frame.Map(a.To.X, a.To.Y))), r, secondary: true);
        dc.Pop();

        dc.DrawText(readout, new Point(0, ActualHeight - readout.Height));
    }

    private double MarkerRadius => Math.Max(2.5, LabelSize * 0.17);

    /// <summary>Hollow rings (the target) or solid squares (the points), all in one geometry so thousands draw quickly.</summary>
    private static StreamGeometry Markers(IEnumerable<Point> centres, double r, bool ring)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            foreach (var c in centres)
            {
                if (!double.IsFinite(c.X) || !double.IsFinite(c.Y)) continue;
                if (ring)
                {
                    g.BeginFigure(new Point(c.X - r, c.Y), false, true);
                    g.ArcTo(new Point(c.X + r, c.Y), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                    g.ArcTo(new Point(c.X - r, c.Y), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                }
                else
                {
                    double s = r * 0.9;
                    g.BeginFigure(new Point(c.X - s, c.Y - s), true, true);
                    g.PolyLineTo([new Point(c.X + s, c.Y - s), new Point(c.X + s, c.Y + s), new Point(c.X - s, c.Y + s)], false, false);
                }
            }
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>A large circle around each highlighted point: a thick line in the text colour over a wider surface-coloured
    /// one, so it shows on the cloud and on the background alike.</summary>
    private void DrawHighlights(DrawingContext dc, IEnumerable<Point> centres, double r)
    {
        double radius = r * 2.6;
        var geometry = new GeometryGroup();
        foreach (var c in centres)
            if (double.IsFinite(c.X) && double.IsFinite(c.Y)) geometry.Children.Add(new EllipseGeometry(c, radius, radius));
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(SurfaceBrush, 4.5), geometry);
        dc.DrawGeometry(null, new Pen(Foreground, 2), geometry);
    }

    /// <summary>The first set: solid shafts with open (outlined) heads in Series 2. The second: dashed shafts with filled
    /// heads in Series 3. A head is never longer than half its arrow.</summary>
    private void DrawArrows(DrawingContext dc, IEnumerable<(Point From, Point To)> arrows, double r, bool secondary)
    {
        var brush = secondary ? Series3Brush : Series2Brush;
        var shaftPen = secondary
            ? new Pen(brush, 1.8) { DashStyle = new DashStyle(new DoubleCollection { 2.5, 1.8 }, 0) }
            : new Pen(brush, 1.8);
        var shafts = new StreamGeometry();
        var heads = new StreamGeometry();
        double headLength = Math.Max(9, r * 3.5);   // big enough to tell an open head from a filled one
        using (var s = shafts.Open())
        using (var h = heads.Open())
        {
            foreach (var (from, to) in arrows)
            {
                if (!double.IsFinite(from.X) || !double.IsFinite(from.Y) || !double.IsFinite(to.X) || !double.IsFinite(to.Y)) continue;
                var along = to - from;
                double length = along.Length;
                if (length < 0.5) continue;
                along /= length;
                double head = Math.Min(headLength, length / 2);
                var back = to - along * head;
                var side = new Vector(-along.Y, along.X) * head * 0.45;
                s.BeginFigure(from, false, false);
                s.LineTo(back, true, false);
                h.BeginFigure(to, secondary, true);
                h.PolyLineTo([back + side, back - side], true, true);
            }
        }
        shafts.Freeze();
        heads.Freeze();
        dc.DrawGeometry(null, shaftPen, shafts);
        if (secondary) dc.DrawGeometry(brush, null, heads);
        else dc.DrawGeometry(SurfaceBrush, new Pen(brush, 1.8) { LineJoin = PenLineJoin.Miter }, heads);   // open head: outlined, hollow
    }

    /// <summary>The legend's size when laid out in <paramref name="width"/>: the right edge of its widest row and its height.</summary>
    public Size LegendSize(double width)
    {
        double height = DrawLegend(null, width, out double right);
        return new Size(right, height);
    }

    private double DrawLegend(DrawingContext? dc, double width) => DrawLegend(dc, width, out _);

    /// <summary>Draws the legend (when <paramref name="dc"/> is not null) and returns its height. Only what is shown is named.
    /// Entries go side by side while they fit; an entry's words wrap when the width is too narrow for them on one line, so
    /// at a large text size in a narrow pane nothing runs past the plot's edge.</summary>
    private double DrawLegend(DrawingContext? dc, double width, out double right)
    {
        right = 0;
        double x = 0, y = 0, line = Text("Ag", LabelSize, Foreground).Height, r = Math.Max(MarkerRadius, line * 0.2);
        var items = new List<(string Text, Action<DrawingContext, Point> Key)>();
        if (TargetPoints is { Count: > 0 })
            items.Add(($"{TargetLabel} (faint rings)", (d, at) =>
                d.DrawGeometry(null, new Pen(TargetRingBrush, 1), Markers([at with { X = at.X - r * 2.5 }, at, at with { X = at.X + r * 2.5 }], r, ring: true))));
        if (Points is { Count: > 0 })
            items.Add(($"{PointsLabel} (solid squares)", (d, at) =>
                d.DrawGeometry(Series1Brush, null, Markers([at with { X = at.X - r * 2.5 }, at, at with { X = at.X + r * 2.5 }], r, ring: false))));
        if (Highlighted.Count > 0)
            items.Add(($"{HighlightLabel} (large circles)", (d, at) =>
            {
                d.DrawGeometry(Series1Brush, null, Markers([at], r, ring: false));
                DrawHighlights(d, [at], r);
            }));
        if (Arrows is { Count: > 0 })
            items.Add(($"{ArrowsLabel} (solid arrows, open heads)", (d, at) =>
                DrawArrows(d, [(at with { X = at.X - 18 }, at with { X = at.X + 18 })], r, secondary: false)));
        if (SecondaryArrows is { Count: > 0 })
            items.Add(($"{SecondaryArrowsLabel} (dashed arrows, filled heads)", (d, at) =>
                DrawArrows(d, [(at with { X = at.X - 18 }, at with { X = at.X + 18 })], r, secondary: true)));
        double row = line;   // the height of the row being laid out (an entry that wraps makes it taller)
        foreach (var (label, key) in items)
        {
            var text = Text(label, LabelSize, Foreground, maxWidth: Math.Max(50, FiniteOr(width, 600) - 48 - 1));
            double itemWidth = 40 + 8 + text.Width + 24;
            if (x > 0 && x + itemWidth > width) { x = 0; y += row + 4; row = line; }
            if (dc != null)
            {
                key(dc, new Point(x + 20, y + line / 2));
                dc.DrawText(text, new Point(x + 48, y));
            }
            right = Math.Max(right, x + 48 + text.Width);
            row = Math.Max(row, text.Height);
            x += itemWidth;
        }
        return items.Count == 0 ? 0 : y + row;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == PointsProperty || e.Property == TargetPointsProperty || e.Property == PointsLabelProperty
            || e.Property == TargetLabelProperty || e.Property == AxisLimitProperty || e.Property == FitToDataProperty
            || e.Property == HighlightIndicesProperty || e.Property == HighlightLabelProperty
            || e.Property == ArrowsProperty || e.Property == SecondaryArrowsProperty)
        {
            string summary = SummaryText();
            AutomationProperties.SetHelpText(this, summary);
            ToolTip = HasData ? summary : null;
        }
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(PointCloudPlot), new FrameworkPropertyMetadata(fallback, Relayout));
}
