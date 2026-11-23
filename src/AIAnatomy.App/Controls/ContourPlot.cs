using System.Windows;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A surface z = f(x, y) seen from above: the values are shaded on the single-hue ramp (light = low, dark = high), lines
/// join points of equal value (contours, each named with its value), and a path (such as the steps of gradient descent)
/// is drawn over it. The path's first point is a square and its last a diamond, joined by a dashed line with a dot at
/// each step, so nothing depends on colour. Points of the path outside the axes are clipped.
/// </summary>
public sealed class ContourPlot : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double UnboundedHeight = 400, Gap = 8;

    public static readonly DependencyProperty ValuesProperty = Register<double[,]?>(nameof(Values), null);
    public static readonly DependencyProperty XMinProperty = Register(nameof(XMin), -1.0);
    public static readonly DependencyProperty XMaxProperty = Register(nameof(XMax), 1.0);
    public static readonly DependencyProperty YMinProperty = Register(nameof(YMin), -1.0);
    public static readonly DependencyProperty YMaxProperty = Register(nameof(YMax), 1.0);
    public static readonly DependencyProperty LevelsProperty = Register<IReadOnlyList<double>?>(nameof(Levels), null);
    public static readonly DependencyProperty PathProperty = Register<IReadOnlyList<PlotPoint>?>(nameof(Path), null);
    public static readonly DependencyProperty XAxisTitleProperty = Register<string?>(nameof(XAxisTitle), null);
    public static readonly DependencyProperty YAxisTitleProperty = Register<string?>(nameof(YAxisTitle), null);
    public static readonly DependencyProperty RampLowColorProperty = RegisterColor(nameof(RampLowColor), Colors.White);
    public static readonly DependencyProperty RampMidColorProperty = RegisterColor(nameof(RampMidColor), Colors.LightBlue);
    public static readonly DependencyProperty RampHighColorProperty = RegisterColor(nameof(RampHighColor), Colors.Navy);

    public ContourPlot()
    {
        SetResourceReference(RampLowColorProperty, "RampLowColor");
        SetResourceReference(RampMidColorProperty, "RampMidColor");
        SetResourceReference(RampHighColorProperty, "RampHighColor");
    }

    /// <summary>The surface: row 0 is at <see cref="YMin"/>, column 0 at <see cref="XMin"/>.</summary>
    public double[,]? Values { get => (double[,]?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public double XMin { get => (double)GetValue(XMinProperty); set => SetValue(XMinProperty, value); }
    public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }
    public double YMin { get => (double)GetValue(YMinProperty); set => SetValue(YMinProperty, value); }
    public double YMax { get => (double)GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }
    /// <summary>The values to draw contour lines at.</summary>
    public IReadOnlyList<double>? Levels { get => (IReadOnlyList<double>?)GetValue(LevelsProperty); set => SetValue(LevelsProperty, value); }
    /// <summary>Points joined in order. Labels are written beside the points that have one.</summary>
    public IReadOnlyList<PlotPoint>? Path { get => (IReadOnlyList<PlotPoint>?)GetValue(PathProperty); set => SetValue(PathProperty, value); }
    public string? XAxisTitle { get => (string?)GetValue(XAxisTitleProperty); set => SetValue(XAxisTitleProperty, value); }
    public string? YAxisTitle { get => (string?)GetValue(YAxisTitleProperty); set => SetValue(YAxisTitleProperty, value); }
    public Color RampLowColor { get => (Color)GetValue(RampLowColorProperty); set => SetValue(RampLowColorProperty, value); }
    public Color RampMidColor { get => (Color)GetValue(RampMidColorProperty); set => SetValue(RampMidColorProperty, value); }
    public Color RampHighColor { get => (Color)GetValue(RampHighColorProperty); set => SetValue(RampHighColorProperty, value); }

    private bool HasData => Values is { } v && v.GetLength(0) >= 2 && v.GetLength(1) >= 2 && XMax > XMin && YMax > YMin;

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        return new Size(FiniteOr(available.Width, 600), double.IsFinite(available.Height) && available.Height > 0 ? available.Height : Math.Max(UnboundedHeight, FontSize * 14));
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        var values = Values!;
        int rows = values.GetLength(0), cols = values.GetLength(1);
        var bounds = new Rect(0, 0, ActualWidth, Math.Max(40, ActualHeight));
        var frame = LayoutAxes(bounds, XMin, XMax, YMin, YMax, XAxisTitle, YAxisTitle);
        DrawAxes(dc, frame, bounds);

        double max = 0;
        foreach (double v in values) if (double.IsFinite(v)) max = Math.Max(max, v);
        if (max <= 0) max = 1;

        dc.PushClip(new RectangleGeometry(frame.Plot));
        // Shading: one rectangle per grid cell, coloured by its centre-left corner value (the grid is fine enough to look smooth).
        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < cols - 1; c++)
        {
            double v = (values[r, c] + values[r + 1, c] + values[r, c + 1] + values[r + 1, c + 1]) / 4;
            var a = frame.Map(XAt(c), YAt(r));
            var b = frame.Map(XAt(c + 1), YAt(r + 1));
            var rect = new Rect(Math.Min(a.X, b.X) - 0.5, Math.Min(a.Y, b.Y) - 0.5, Math.Abs(b.X - a.X) + 1, Math.Abs(b.Y - a.Y) + 1);
            dc.DrawRectangle(new SolidColorBrush(RampColor(Math.Sqrt(Math.Clamp(v / max, 0, 1)))), null, rect);
        }

        var pen = new Pen(Foreground, 1.5);
        var contourHalo = new Pen(SurfaceBrush, 4);
        var labels = new List<(Point At, string Text)>();
        foreach (double level in Levels ?? [])
        {
            var segments = Contour(values, level);
            foreach (var (p, q) in segments) dc.DrawLine(contourHalo, frame.Map(p.X, p.Y), frame.Map(q.X, q.Y));
            foreach (var (p, q) in segments) dc.DrawLine(pen, frame.Map(p.X, p.Y), frame.Map(q.X, q.Y));
            if (segments.Count > 0)
            {
                var (p, q) = segments[segments.Count / 4];   // a point on the line, away from the very end
                labels.Add((frame.Map((p.X + q.X) / 2, (p.Y + q.Y) / 2), NumberText.Short(level)));
            }
        }
        foreach (var (at, text) in labels)
        {
            var t = Number(text, LabelSize * 0.85, Foreground);
            var plate = new Rect(at.X - t.Width / 2 - 3, at.Y - t.Height / 2, t.Width + 6, t.Height);
            dc.DrawRoundedRectangle(SurfaceBrush, null, plate, 3, 3);
            dc.DrawText(t, new Point(plate.X + 3, plate.Y));
        }

        DrawPath(dc, frame);
        dc.Pop();
        dc.DrawRectangle(null, new Pen(AxisBrush, 1.5), frame.Plot);
    }

    private void DrawPath(DrawingContext dc, AxisFrame frame)
    {
        var path = (Path ?? []).Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).ToList();
        if (path.Count == 0) return;
        var halo = new Pen(SurfaceBrush, 6) { LineJoin = PenLineJoin.Round };
        var line = new Pen(Foreground, 2.5) { DashStyle = new DashStyle(new DoubleCollection { 4, 2 }, 0), LineJoin = PenLineJoin.Round };

        // Each step is cut to the axes (plus a small margin) in data coordinates first: a diverging path can be millions of
        // pixels long, and only the visible part is drawn. Joined steps form one line, so the dashes run on along the whole
        // path instead of starting again at every step (final review M19); a new piece starts only where the path was cut.
        double mx = (XMax - XMin) * 0.02, my = (YMax - YMin) * 0.02;
        double x0 = XMin - mx, x1 = XMax + mx, y0 = YMin - my, y1 = YMax + my;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            Point? end = null;
            for (int i = 1; i < path.Count; i++)
            {
                if (SegmentClip.Clip(path[i - 1].X, path[i - 1].Y, path[i].X, path[i].Y, x0, x1, y0, y1) is not { } cut) { end = null; continue; }
                var a = frame.Map(cut.From.X, cut.From.Y);
                var b = frame.Map(cut.To.X, cut.To.Y);
                if (end is not { } previous || (previous - a).Length > 0.01) g.BeginFigure(a, false, false);
                g.LineTo(b, true, true);
                end = b;
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, halo, geometry);
        dc.DrawGeometry(null, line, geometry);

        double r = Math.Max(4, LabelSize * 0.25), big = Math.Max(8, LabelSize * 0.5);
        var ring = new Pen(SurfaceBrush, 1.5);
        bool Visible(int i) => SegmentClip.Inside(path[i].X, path[i].Y, x0, x1, y0, y1);
        for (int i = 1; i < path.Count - 1; i++)
            if (Visible(i)) dc.DrawEllipse(Foreground, ring, frame.Map(path[i].X, path[i].Y), r, r);
        if (path.Count > 1 && Visible(path.Count - 1))
            dc.DrawGeometry(Foreground, ring, Diamond(frame.Map(path[^1].X, path[^1].Y), big * 1.15));
        if (Visible(0))
        {
            var s = frame.Map(path[0].X, path[0].Y);
            dc.DrawRectangle(Foreground, ring, new Rect(s.X - big * 0.85, s.Y - big * 0.85, big * 1.7, big * 1.7));
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (string.IsNullOrEmpty(path[i].Label) || !Visible(i)) continue;
            var at = frame.Map(path[i].X, path[i].Y);
            var text = Text(path[i].Label, LabelSize * 0.9, Foreground, BoldFace);
            var plate = new Rect(at.X + big + 4, at.Y - text.Height / 2, text.Width + 6, text.Height);
            if (plate.Right > frame.Plot.Right) plate.X = at.X - big - 4 - plate.Width;
            dc.DrawRoundedRectangle(SurfaceBrush, null, plate, 3, 3);
            dc.DrawText(text, new Point(plate.X + 3, plate.Y));
        }
    }

    private static Geometry Diamond(Point c, double r)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(c.X, c.Y - r), true, true);
            g.PolyLineTo([new Point(c.X + r, c.Y), new Point(c.X, c.Y + r), new Point(c.X - r, c.Y)], true, true);
        }
        geometry.Freeze();
        return geometry;
    }

    private double XAt(int column) => XMin + (XMax - XMin) * column / (Values!.GetLength(1) - 1);
    private double YAt(int row) => YMin + (YMax - YMin) * row / (Values!.GetLength(0) - 1);

    private Color RampColor(double t) =>
        t < 0.5 ? ChartMath.Mix(RampLowColor, RampMidColor, t * 2) : ChartMath.Mix(RampMidColor, RampHighColor, (t - 0.5) * 2);

    /// <summary>Marching squares: the line segments, in data coordinates, where the surface crosses <paramref name="level"/>.</summary>
    private List<(Point, Point)> Contour(double[,] v, double level)
    {
        var result = new List<(Point, Point)>();
        int rows = v.GetLength(0), cols = v.GetLength(1);
        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < cols - 1; c++)
        {
            double v00 = v[r, c], v10 = v[r, c + 1], v01 = v[r + 1, c], v11 = v[r + 1, c + 1];
            if (!double.IsFinite(v00 + v10 + v01 + v11)) continue;
            double x0 = XAt(c), x1 = XAt(c + 1), y0 = YAt(r), y1 = YAt(r + 1);
            Point? bottom = Cross(v00, v10, level) is { } tb ? new Point(x0 + (x1 - x0) * tb, y0) : null;
            Point? top = Cross(v01, v11, level) is { } tt ? new Point(x0 + (x1 - x0) * tt, y1) : null;
            Point? left = Cross(v00, v01, level) is { } tl ? new Point(x0, y0 + (y1 - y0) * tl) : null;
            Point? right = Cross(v10, v11, level) is { } tr ? new Point(x1, y0 + (y1 - y0) * tr) : null;
            var found = new[] { bottom, right, top, left }.Where(p => p != null).Select(p => p!.Value).ToList();
            if (found.Count == 2) result.Add((found[0], found[1]));
            else if (found.Count == 4)
            {
                result.Add((bottom!.Value, left!.Value));
                result.Add((top!.Value, right!.Value));
            }
        }
        return result;
    }

    /// <summary>Where, from 0 to 1, the level lies between two values, or null if it does not lie between them.</summary>
    private static double? Cross(double a, double b, double level)
    {
        if ((a < level) == (b < level)) return null;
        return (level - a) / (b - a);
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(ContourPlot), new FrameworkPropertyMetadata(fallback, Relayout));

    private static DependencyProperty RegisterColor(string name, Color fallback) =>
        DependencyProperty.Register(name, typeof(Color), typeof(ContourPlot), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));
}
