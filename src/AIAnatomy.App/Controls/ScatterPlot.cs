using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Labelled points on two axes. Each group has its own marker shape as well as its own colour, named in the legend.
/// Click a point (or use the arrow keys) to see its exact position in the readout; hovering shows it too.
/// </summary>
public sealed class ScatterPlot : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double UnboundedHeight = 420, Gap = 8, HitRadius = 16;

    public static readonly DependencyProperty PointsProperty = Register<IReadOnlyList<PlotPoint>?>(nameof(Points), null);
    public static readonly DependencyProperty XAxisTitleProperty = Register<string?>(nameof(XAxisTitle), null);
    public static readonly DependencyProperty YAxisTitleProperty = Register<string?>(nameof(YAxisTitle), null);
    public static readonly DependencyProperty LinesProperty = Register<IReadOnlyList<LineSeries>?>(nameof(Lines), null);
    public static readonly DependencyProperty ShowLabelsProperty = Register(nameof(ShowLabels), true);
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int),
        typeof(ScatterPlot), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | Relayout));

    public ScatterPlot()
    {
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 200);
    }

    public IReadOnlyList<PlotPoint>? Points { get => (IReadOnlyList<PlotPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public string? XAxisTitle { get => (string?)GetValue(XAxisTitleProperty); set => SetValue(XAxisTitleProperty, value); }
    public string? YAxisTitle { get => (string?)GetValue(YAxisTitleProperty); set => SetValue(YAxisTitleProperty, value); }
    /// <summary>Straight lines drawn behind the points (such as a decision line), each named in the legend. The axes also fit their end points.</summary>
    public IReadOnlyList<LineSeries>? Lines { get => (IReadOnlyList<LineSeries>?)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    /// <summary>Write each point's label beside it.</summary>
    public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    /// <summary>The selected point (index into <see cref="Points"/>), or −1.</summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    private bool HasData => Points is { Count: > 0 };

    private IEnumerable<(double X, double Y)> LineEnds =>
        (Lines ?? []).SelectMany(l => l.Points).Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y));

    private List<string> Groups => Points!.Select(p => p.Group).Distinct().ToList();

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        double width = FiniteOr(available.Width, 600);
        return new Size(width, double.IsFinite(available.Height) && available.Height > 0 ? available.Height : Math.Max(UnboundedHeight, FontSize * 14));
    }

    private (double LegendHeight, double ReadoutHeight, AxisFrame Frame, Rect Bounds) ComputeLayout()
    {
        double legend = DrawLegend(null);
        double readout = ReadoutText().Height;
        var points = Points!;
        var xs = points.Select(p => p.X).Concat(LineEnds.Select(p => p.X)).ToList();
        var ys = points.Select(p => p.Y).Concat(LineEnds.Select(p => p.Y)).ToList();
        var (xMin, xMax) = AxisTicks.Pad(xs.Min(), xs.Max());
        var (yMin, yMax) = AxisTicks.Pad(ys.Min(), ys.Max());
        var bounds = new Rect(0, legend + Gap, ActualWidth, Math.Max(40, ActualHeight - legend - Gap - readout - Gap));
        return (legend, readout, LayoutAxes(bounds, xMin, xMax, yMin, yMax, XAxisTitle, YAxisTitle), bounds);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        var points = Points!;
        var (_, _, frame, bounds) = ComputeLayout();
        DrawLegend(dc);
        DrawAxes(dc, frame, bounds);

        var groups = Groups;
        double marker = Math.Max(10, LabelSize * 0.55);
        foreach (var line in Lines ?? [])
        {
            var ends = line.Points.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).Select(p => frame.Map(p.X, p.Y)).ToList();
            for (int k = 1; k < ends.Count; k++) dc.DrawLine(LinePen, ends[k - 1], ends[k]);
        }
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            int g = groups.IndexOf(p.Group);
            var at = frame.Map(p.X, p.Y);
            DrawMarker(dc, g, at, marker, SeriesBrush(g), new Pen(SurfaceBrush, 1.5));
            if (i == SelectedIndex) dc.DrawEllipse(null, new Pen(HighlightBrush, 3), at, marker + 5, marker + 5);
            if (ShowLabels && !string.IsNullOrEmpty(p.Label))
            {
                var text = Text(p.Label, LabelSize * 0.85, Foreground, i == SelectedIndex ? BoldFace : TextFace);
                double gap = marker + (i == SelectedIndex ? 10 : 4);   // clear of the selection ring
                double x = at.X + gap;
                if (x + text.Width > ActualWidth) x = at.X - gap - text.Width;
                dc.DrawText(text, new Point(x, at.Y - text.Height / 2));
            }
        }

        dc.DrawText(ReadoutText(), new Point(0, ActualHeight - ReadoutText().Height));
    }

    /// <summary>Draws the legend (when <paramref name="dc"/> is not null) and returns its height.</summary>
    private double DrawLegend(DrawingContext? dc)
    {
        double x = 0, y = 0, line = Text("Ag", LabelSize, Foreground).Height;
        double marker = Math.Max(10, LabelSize * 0.55);
        var groups = Groups;
        for (int g = 0; g < groups.Count; g++)
        {
            var text = Text(groups[g], LabelSize, Foreground);
            double itemWidth = marker * 2 + 8 + text.Width + 24;
            if (x > 0 && x + itemWidth > ActualWidth) { x = 0; y += line + 4; }
            if (dc != null)
            {
                DrawMarker(dc, g, new Point(x + marker, y + line / 2), marker, SeriesBrush(g), null);
                dc.DrawText(text, new Point(x + marker * 2 + 8, y));
            }
            x += itemWidth;
        }
        foreach (var named in Lines ?? [])
        {
            var text = Text(named.Name, LabelSize, Foreground);
            double itemWidth = 48 + text.Width + 24;
            if (x > 0 && x + itemWidth > ActualWidth) { x = 0; y += line + 4; }
            if (dc != null)
            {
                dc.DrawLine(LinePen, new Point(x + 2, y + line / 2), new Point(x + 40, y + line / 2));
                dc.DrawText(text, new Point(x + 48, y));
            }
            x += itemWidth;
        }
        return y + line;
    }

    private Pen LinePen => new(Foreground, 3) { DashStyle = new DashStyle(new DoubleCollection { 6, 3 }, 0), StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };

    /// <summary>Circle, square, triangle, diamond, then the same shapes with a cross: never colour alone.</summary>
    private static void DrawMarker(DrawingContext dc, int group, Point c, double r, Brush fill, Pen? outline)
    {
        switch (group % 4)
        {
            case 0: dc.DrawEllipse(fill, outline, c, r, r); break;
            case 1: dc.DrawRectangle(fill, outline, new Rect(c.X - r * 0.85, c.Y - r * 0.85, r * 1.7, r * 1.7)); break;
            case 2: dc.DrawGeometry(fill, outline, Polygon(new Point(c.X, c.Y - r * 1.1), new Point(c.X + r, c.Y + r * 0.8), new Point(c.X - r, c.Y + r * 0.8))); break;
            default: dc.DrawGeometry(fill, outline, Polygon(new Point(c.X, c.Y - r * 1.15), new Point(c.X + r * 1.15, c.Y), new Point(c.X, c.Y + r * 1.15), new Point(c.X - r * 1.15, c.Y))); break;
        }
        if (group >= 4)
        {
            var pen = new Pen(Brushes.White, 2);
            dc.DrawLine(pen, new Point(c.X - r * 0.5, c.Y), new Point(c.X + r * 0.5, c.Y));
            dc.DrawLine(pen, new Point(c.X, c.Y - r * 0.5), new Point(c.X, c.Y + r * 0.5));
        }
    }

    private static Geometry Polygon(params Point[] corners)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(corners[0], true, true);
            g.PolyLineTo(corners.Skip(1).ToList(), true, true);
        }
        geometry.Freeze();
        return geometry;
    }

    private string Describe(int i)
    {
        var p = Points![i];
        return $"“{p.Label}” ({p.Group}): x = {NumberText.Exact(p.X)}, y = {NumberText.Exact(p.Y)}";
    }

    private FormattedText ReadoutText()
    {
        string text = SelectedIndex >= 0 && SelectedIndex < (Points?.Count ?? 0)
            ? Describe(SelectedIndex)
            : "Click a point, or use the arrow keys, to see exactly where it is.";
        return Text(text, FontSize * 0.9, Foreground, maxWidth: Math.Max(50, ActualWidth));
    }

    private int PointAt(Point at)
    {
        if (!HasData || ActualWidth <= 0) return -1;
        var frame = ComputeLayout().Frame;
        int best = -1;
        double bestDistance = HitRadius;
        for (int i = 0; i < Points!.Count; i++)
        {
            double d = (frame.Map(Points[i].X, Points[i].Y) - at).Length;
            if (d <= bestDistance) { best = i; bestDistance = d; }
        }
        return best;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (PointAt(e.GetPosition(this)) is var i and >= 0) SelectedIndex = i;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = PointAt(e.GetPosition(this));
        ToolTip = i >= 0 ? Describe(i) : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HasData) return;
        int step = e.Key switch { Key.Left or Key.Up => -1, Key.Right or Key.Down => 1, _ => 0 };
        if (step == 0) return;
        SelectedIndex = SelectedIndex < 0 ? 0 : (SelectedIndex + step + Points!.Count) % Points.Count;
        e.Handled = true;
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(ScatterPlot), new FrameworkPropertyMetadata(fallback, Relayout));
}
