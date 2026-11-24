using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// One or two lines (such as training and validation loss). The first line is solid, the second dashed, the third
/// dotted, so they differ without colour. The legend gives each line's latest value to 3 decimals; hovering shows the
/// exact values nearest the pointer.
/// </summary>
public sealed class LineChart : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double UnboundedHeight = 380, Gap = 8;

    public static readonly DependencyProperty SeriesProperty = Register<IReadOnlyList<LineSeries>?>(nameof(Series), null);
    public static readonly DependencyProperty XAxisTitleProperty = Register<string?>(nameof(XAxisTitle), null);
    public static readonly DependencyProperty YAxisTitleProperty = Register<string?>(nameof(YAxisTitle), null);

    /// <summary>The x axis covers at least this much from its first value (0: just the data). With one point at step 0
    /// and a span of 20, the axis runs 0 to 20 rather than −1 to 1.</summary>
    public static readonly DependencyProperty MinXSpanProperty = Register(nameof(MinXSpan), 0.0);

    public LineChart() => ToolTipService.SetInitialShowDelay(this, 200);

    public IReadOnlyList<LineSeries>? Series { get => (IReadOnlyList<LineSeries>?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public string? XAxisTitle { get => (string?)GetValue(XAxisTitleProperty); set => SetValue(XAxisTitleProperty, value); }
    public string? YAxisTitle { get => (string?)GetValue(YAxisTitleProperty); set => SetValue(YAxisTitleProperty, value); }
    public double MinXSpan { get => (double)GetValue(MinXSpanProperty); set => SetValue(MinXSpanProperty, value); }

    private IEnumerable<(double X, double Y)> AllPoints => (Series ?? []).SelectMany(s => s.Points).Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y));

    private bool HasData => AllPoints.Any();

    private static readonly DoubleCollection Dashed = [4, 2.5];
    private static readonly DoubleCollection Dotted = [0.5, 2];

    private Pen SeriesPen(int i, double thickness)
    {
        var pen = new Pen(SeriesBrush(i), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (i % 3 == 1) { pen.DashStyle = new DashStyle(Dashed, 0); pen.DashCap = PenLineCap.Flat; }
        else if (i % 3 == 2) { pen.DashStyle = new DashStyle(Dotted, 0); pen.DashCap = PenLineCap.Round; }
        return pen;
    }

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        return new Size(FiniteOr(available.Width, 600), double.IsFinite(available.Height) && available.Height > 0 ? available.Height : Math.Max(UnboundedHeight, FontSize * 14));
    }

    private AxisFrame Frame(double legendHeight)
    {
        var points = AllPoints.ToList();
        // The x axis starts and ends at the data (widened only when all x are nearly equal, or to MinXSpan); y gets 5% room.
        var (xMin, xMax) = AxisTicks.WidenTo(points.Min(p => p.X), points.Max(p => p.X), MinXSpan);
        var (yMin, yMax) = AxisTicks.Pad(points.Min(p => p.Y), points.Max(p => p.Y));
        var bounds = new Rect(0, legendHeight + Gap, ActualWidth, Math.Max(40, ActualHeight - legendHeight - Gap));
        return LayoutAxes(bounds, xMin, xMax, yMin, yMax, XAxisTitle, YAxisTitle);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        double legend = DrawLegend(null);
        var frame = Frame(legend);
        DrawLegend(dc);
        DrawAxes(dc, frame, new Rect(0, legend + Gap, ActualWidth, Math.Max(40, ActualHeight - legend - Gap)));

        var series = Series!;
        dc.PushClip(new RectangleGeometry(Inflate(frame.Plot, 6)));
        for (int i = 0; i < series.Count; i++)
        {
            var points = series[i].Points.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).ToList();
            if (points.Count == 0) continue;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(frame.Map(points[0].X, points[0].Y), false, false);
                g.PolyLineTo(points.Skip(1).Select(p => frame.Map(p.X, p.Y)).ToList(), true, true);
            }
            dc.DrawGeometry(null, SeriesPen(i, 3), geometry);
            if (points.Count <= 30 || points.Count == 1)
                foreach (var p in points) dc.DrawEllipse(SeriesBrush(i), null, frame.Map(p.X, p.Y), 4, 4);
        }
        dc.Pop();
    }

    /// <summary>Draws the legend (when <paramref name="dc"/> is not null) and returns its height.</summary>
    private double DrawLegend(DrawingContext? dc)
    {
        double x = 0, y = 0;
        var series = Series!;
        double line = Text("Ag", LabelSize, Foreground).Height;
        for (int i = 0; i < series.Count; i++)
        {
            string? latest = series[i].LatestText;
            string name = series[i].Name + (latest != null ? ": latest " : ": no points yet");
            var nameText = Text(name, LabelSize, Foreground);
            var valueText = latest != null ? Number(latest, LabelSize, Foreground, bold: true) : null;
            double itemWidth = 48 + nameText.WidthIncludingTrailingWhitespace + (valueText?.Width ?? 0) + 28;
            if (x > 0 && x + itemWidth > ActualWidth) { x = 0; y += line + 4; }
            if (dc != null)
            {
                dc.DrawLine(SeriesPen(i, 4), new Point(x + 2, y + line / 2), new Point(x + 40, y + line / 2));
                dc.DrawText(nameText, new Point(x + 48, y));
                if (valueText != null) dc.DrawText(valueText, new Point(x + 48 + nameText.WidthIncludingTrailingWhitespace, y + (line - valueText.Height) / 2));
            }
            x += itemWidth;
        }
        return y + line;
    }

    private static Rect Inflate(Rect r, double by)
    {
        r.Inflate(by, by);
        return r;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!HasData || ActualWidth <= 0) { ToolTip = null; return; }
        var frame = Frame(DrawLegend(null));
        var at = e.GetPosition(this);
        if (!Inflate(frame.Plot, 8).Contains(at)) { ToolTip = null; return; }
        double x = frame.XMin + (at.X - frame.Plot.Left) / frame.Plot.Width * (frame.XMax - frame.XMin);
        var lines = Series!.Where(s => s.Points.Count > 0).Select(s =>
        {
            var nearest = s.Points.MinBy(p => Math.Abs(p.X - x));
            return $"{s.Name} at {NumberText.Exact(nearest.X)}: {NumberText.Exact(nearest.Y)}";
        });
        ToolTip = string.Join("\n", lines);
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(LineChart), new FrameworkPropertyMetadata(fallback, Relayout));
}
