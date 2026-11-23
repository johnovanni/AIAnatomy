using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A grid of numbers drawn as squares on a single-hue ramp (light = small, dark = large), with row and column labels,
/// a scale, and a readout giving the selected square's exact value. Squares that are −∞ or not a number are
/// "blocked" (a grey cross-hatch). Click a square or use the arrow keys to select it; hovering shows its value too.
/// </summary>
public sealed class HeatMap : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double MaxCell = 96, UnboundedCell = 56, Gap = 10;
    public const string BlockedText = "blocked (future position)";

    public static readonly DependencyProperty ValuesProperty = Register<double[,]?>(nameof(Values), null);
    public static readonly DependencyProperty RowLabelsProperty = Register<IReadOnlyList<string>?>(nameof(RowLabels), null);
    public static readonly DependencyProperty ColumnLabelsProperty = Register<IReadOnlyList<string>?>(nameof(ColumnLabels), null);
    public static readonly DependencyProperty RowAxisTitleProperty = Register<string?>(nameof(RowAxisTitle), null);
    public static readonly DependencyProperty ColumnAxisTitleProperty = Register<string?>(nameof(ColumnAxisTitle), null);
    public static readonly DependencyProperty MinimumProperty = Register(nameof(Minimum), double.NaN);
    public static readonly DependencyProperty MaximumProperty = Register(nameof(Maximum), double.NaN);
    public static readonly DependencyProperty ShowCellValuesProperty = Register(nameof(ShowCellValues), true);

    public static readonly DependencyProperty OutlinedCellsProperty = Register<IReadOnlyList<GridCell>?>(nameof(OutlinedCells), null);

    public static readonly DependencyProperty SelectedCellProperty = DependencyProperty.Register(nameof(SelectedCell), typeof(GridCell?),
        typeof(HeatMap), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | Relayout));

    public static readonly DependencyProperty BlockedBrushProperty = DependencyProperty.Register(nameof(BlockedBrush), typeof(Brush),
        typeof(HeatMap), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BlockedLineBrushProperty = DependencyProperty.Register(nameof(BlockedLineBrush), typeof(Brush),
        typeof(HeatMap), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RampLowColorProperty = RegisterColor(nameof(RampLowColor), Colors.White);
    public static readonly DependencyProperty RampMidColorProperty = RegisterColor(nameof(RampMidColor), Colors.LightBlue);
    public static readonly DependencyProperty RampHighColorProperty = RegisterColor(nameof(RampHighColor), Colors.Navy);

    public HeatMap()
    {
        Focusable = true;
        SetResourceReference(BlockedBrushProperty, "BlockedBrush");
        SetResourceReference(BlockedLineBrushProperty, "BlockedLineBrush");
        SetResourceReference(RampLowColorProperty, "RampLowColor");
        SetResourceReference(RampMidColorProperty, "RampMidColor");
        SetResourceReference(RampHighColorProperty, "RampHighColor");
        ToolTipService.SetInitialShowDelay(this, 200);
    }

    public double[,]? Values { get => (double[,]?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IReadOnlyList<string>? RowLabels { get => (IReadOnlyList<string>?)GetValue(RowLabelsProperty); set => SetValue(RowLabelsProperty, value); }
    public IReadOnlyList<string>? ColumnLabels { get => (IReadOnlyList<string>?)GetValue(ColumnLabelsProperty); set => SetValue(ColumnLabelsProperty, value); }
    /// <summary>What the rows are, such as "Looking from".</summary>
    public string? RowAxisTitle { get => (string?)GetValue(RowAxisTitleProperty); set => SetValue(RowAxisTitleProperty, value); }
    /// <summary>What the columns are, such as "Looking at".</summary>
    public string? ColumnAxisTitle { get => (string?)GetValue(ColumnAxisTitleProperty); set => SetValue(ColumnAxisTitleProperty, value); }
    /// <summary>Value drawn lightest; NaN (the default) means the smallest value shown.</summary>
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>Value drawn darkest; NaN (the default) means the largest value shown.</summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>Write a short value inside each square when there is room.</summary>
    public bool ShowCellValues { get => (bool)GetValue(ShowCellValuesProperty); set => SetValue(ShowCellValuesProperty, value); }
    /// <summary>Squares to draw with a heavy outline (a shape, not a colour), such as the cells a lesson is pointing at. Cells outside the grid are ignored.</summary>
    public IReadOnlyList<GridCell>? OutlinedCells { get => (IReadOnlyList<GridCell>?)GetValue(OutlinedCellsProperty); set => SetValue(OutlinedCellsProperty, value); }
    public GridCell? SelectedCell { get => (GridCell?)GetValue(SelectedCellProperty); set => SetValue(SelectedCellProperty, value); }
    public Brush BlockedBrush { get => (Brush)GetValue(BlockedBrushProperty); set => SetValue(BlockedBrushProperty, value); }
    public Brush BlockedLineBrush { get => (Brush)GetValue(BlockedLineBrushProperty); set => SetValue(BlockedLineBrushProperty, value); }
    public Color RampLowColor { get => (Color)GetValue(RampLowColorProperty); set => SetValue(RampLowColorProperty, value); }
    public Color RampMidColor { get => (Color)GetValue(RampMidColorProperty); set => SetValue(RampMidColorProperty, value); }
    public Color RampHighColor { get => (Color)GetValue(RampHighColorProperty); set => SetValue(RampHighColorProperty, value); }

    public static bool IsBlocked(double v) => double.IsNaN(v) || double.IsNegativeInfinity(v);

    /// <summary>The readout for one square: its row and column names and its exact value.</summary>
    public string Describe(GridCell cell)
    {
        var values = Values!;
        double v = values[cell.Row, cell.Column];
        return $"Row “{RowName(cell.Row)}”, column “{ColumnName(cell.Column)}”: "
               + (IsBlocked(v) ? BlockedText : NumberText.Exact(v));
    }

    private string RowName(int r) => RowLabels is { } l && r < l.Count ? l[r] : r.ToString();
    private string ColumnName(int c) => ColumnLabels is { } l && c < l.Count ? l[c] : c.ToString();

    private bool HasData => Values is { } v && v.GetLength(0) > 0 && v.GetLength(1) > 0;

    private sealed record Layout(double Caption, double RowLabelWidth, double ColumnLabelHeight, bool RotateColumnLabels,
        double Cell, double LegendHeight, double ReadoutTop, double ReadoutHeight)
    {
        public double GridLeft => RowLabelWidth;
        public double GridTop => Caption + ColumnLabelHeight;
    }

    private Layout ComputeLayout(double width)
    {
        var values = Values!;
        int rows = values.GetLength(0), cols = values.GetLength(1);
        double label = LabelSize;
        var caption = CaptionText(width);
        double captionHeight = caption == null ? 0 : caption.Height + Gap;
        double rowLabelWidth = Enumerable.Range(0, rows).Max(r => Text(RowName(r), label, Foreground, BoldFace).Width) + 12;
        // The largest square grows with the text, so the numbers inside still fit at large text sizes.
        double maxCell = Math.Max(MaxCell, ValueWidth(3) + 8);
        double cell = double.IsFinite(width) ? Math.Clamp((width - rowLabelWidth - 2) / cols, 4, maxCell) : Math.Max(UnboundedCell, ValueWidth(3) + 8);
        double columnLabelWidth = Enumerable.Range(0, cols).Max(c => Text(ColumnName(c), label, Foreground, BoldFace).Width);
        bool rotate = columnLabelWidth > cell - 4;
        double columnLabelHeight = (rotate ? columnLabelWidth : Text("Ag", label, Foreground).Height) + 8;
        double legend = (Text("Ag", label, Foreground).Height + 8) * (LegendWraps(width, values) ? 2 : 1);
        double readoutTop = captionHeight + columnLabelHeight + rows * cell + Gap + legend + Gap;
        double readoutWidth = double.IsFinite(width) ? width : rowLabelWidth + cols * cell;
        double readout = ReadoutText(Math.Max(50, readoutWidth)).Height;
        return new Layout(captionHeight, rowLabelWidth, columnLabelHeight, rotate, cell, legend, readoutTop, readout);
    }

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        var l = ComputeLayout(available.Width);
        double width = double.IsFinite(available.Width) ? available.Width : l.RowLabelWidth + Values!.GetLength(1) * l.Cell + 2;
        return new Size(width, l.ReadoutTop + l.ReadoutHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        var values = Values!;
        int rows = values.GetLength(0), cols = values.GetLength(1);
        var l = ComputeLayout(ActualWidth);
        var (min, max) = Range(values);
        double label = LabelSize;
        var selected = SelectedCell is { } s && s.Row < rows && s.Column < cols && s.Row >= 0 && s.Column >= 0 ? s : (GridCell?)null;
        var hatch = HatchBrush();

        if (CaptionText(ActualWidth) is { } caption) dc.DrawText(caption, new Point(0, 0));

        // When the squares are too small for every label, only every few-th one is written (the selected one always is).
        double labelHeight = Text("Ag", label, Foreground).Height;
        int rowStep = Math.Max(1, (int)Math.Ceiling(labelHeight / l.Cell));
        double widestColumnLabel = Enumerable.Range(0, cols).Max(c => Text(ColumnName(c), label, Foreground, BoldFace).Width);
        int columnStep = Math.Max(1, (int)Math.Ceiling((l.RotateColumnLabels ? labelHeight : widestColumnLabel + 4) / l.Cell));

        // Column labels, horizontal when they fit, otherwise turned to read upwards.
        for (int c = 0; c < cols; c++)
        {
            if (c % columnStep != 0 && selected?.Column != c) continue;
            var text = Text(ColumnName(c), label, Foreground, selected?.Column == c ? BoldFace : TextFace);
            double centre = l.GridLeft + (c + 0.5) * l.Cell;
            if (!l.RotateColumnLabels)
                dc.DrawText(text, new Point(centre - text.Width / 2, l.Caption + l.ColumnLabelHeight - text.Height - 4));
            else
            {
                var at = new Point(centre - text.Height / 2, l.Caption + l.ColumnLabelHeight - 4);
                dc.PushTransform(new RotateTransform(-90, at.X, at.Y));
                dc.DrawText(text, at);
                dc.Pop();
            }
        }

        for (int r = 0; r < rows; r++)
        {
            if (r % rowStep != 0 && selected?.Row != r) continue;
            var text = Text(RowName(r), label, Foreground, selected?.Row == r ? BoldFace : TextFace);
            dc.DrawText(text, new Point(l.RowLabelWidth - 8 - text.Width, l.GridTop + (r + 0.5) * l.Cell - text.Height / 2));
        }

        // The squares.
        double valueSize = ValueSize;
        // Three decimals if they fit, else two; otherwise no numbers in the squares (the readout always has the exact value).
        int decimals = ValueWidth(3) + 6 <= l.Cell ? 3 : ValueWidth(2) + 6 <= l.Cell ? 2 : -1;
        bool writeValues = ShowCellValues && decimals >= 0;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var rect = new Rect(l.GridLeft + c * l.Cell, l.GridTop + r * l.Cell, l.Cell, l.Cell);
            double v = values[r, c];
            if (IsBlocked(v)) { dc.DrawRectangle(hatch, null, rect); continue; }
            var colour = RampColor(v, min, max);
            dc.DrawRectangle(new SolidColorBrush(colour), null, rect);
            if (writeValues)
            {
                var text = Number(NumberText.ShortWithDecimals(v, decimals), valueSize, ChartMath.TextOn(colour));
                dc.DrawText(text, new Point(rect.X + (rect.Width - text.Width) / 2, rect.Y + (rect.Height - text.Height) / 2));
            }
        }
        var gridRect = new Rect(l.GridLeft, l.GridTop, cols * l.Cell, rows * l.Cell);
        dc.DrawRectangle(null, new Pen(AxisBrush, 1), gridRect);

        if (OutlinedCells is { } outlined)
        {
            var heavy = new Pen(Foreground, 3);
            var light = new Pen(SurfaceBrush, 1.5);
            foreach (var o in outlined)
            {
                if (o.Row < 0 || o.Row >= rows || o.Column < 0 || o.Column >= cols) continue;
                var outer = new Rect(l.GridLeft + o.Column * l.Cell, l.GridTop + o.Row * l.Cell, l.Cell, l.Cell);
                dc.DrawRectangle(null, heavy, Inflate(outer, -1.5));
                dc.DrawRectangle(null, light, Inflate(outer, -4));
            }
        }

        if (selected is { } cell)
        {
            var rect = new Rect(l.GridLeft + cell.Column * l.Cell, l.GridTop + cell.Row * l.Cell, l.Cell, l.Cell);
            dc.DrawRectangle(null, new Pen(SurfaceBrush, 2), Inflate(rect, -3));
            dc.DrawRectangle(null, new Pen(HighlightBrush, 4), Inflate(rect, -1));
        }

        DrawLegend(dc, l, min, max, hatch, values);

        dc.DrawText(ReadoutText(ActualWidth), new Point(0, l.ReadoutTop));
    }

    private void DrawLegend(DrawingContext dc, Layout l, double min, double max, Brush hatch, double[,] values)
    {
        double label = LabelSize;
        double top = l.GridTop + values.GetLength(0) * l.Cell + Gap;
        bool wraps = LegendWraps(ActualWidth, values);
        double barHeight = (wraps ? l.LegendHeight / 2 : l.LegendHeight) - 8;
        var minText = Number(NumberText.Short(min), label, Foreground);
        var maxText = Number(NumberText.Short(max), label, Foreground);
        double x = 0;
        dc.DrawText(minText, new Point(x, top + (barHeight - minText.Height) / 2));
        x += minText.Width + 8;
        double barWidth = RampBarWidth(ActualWidth);
        var ramp = new LinearGradientBrush(new GradientStopCollection
        {
            new(RampLowColor, 0), new(RampMidColor, 0.5), new(RampHighColor, 1),
        }, 0);
        var bar = new Rect(x, top, barWidth, barHeight);
        dc.DrawRectangle(ramp, new Pen(AxisBrush, 1), bar);
        x += barWidth + 8;
        dc.DrawText(maxText, new Point(x, top + (barHeight - maxText.Height) / 2));
        x += maxText.Width + 24;

        if (AnyBlocked(values))
        {
            if (wraps) (x, top) = (0, top + barHeight + 8);
            var swatch = new Rect(x, top, barHeight, barHeight);
            dc.DrawRectangle(hatch, new Pen(AxisBrush, 1), swatch);
            var text = Text(BlockedText, label, Foreground);
            dc.DrawText(text, new Point(swatch.Right + 8, top + (barHeight - text.Height) / 2));
        }
    }

    private static bool AnyBlocked(double[,] values)
    {
        foreach (double v in values) if (IsBlocked(v)) return true;
        return false;
    }

    private double RampBarWidth(double width) => Math.Clamp(FiniteOr(width, 600) * 0.3, 80, 320);

    /// <summary>True when the "blocked" key does not fit beside the colour scale and goes on a second line.</summary>
    private bool LegendWraps(double width, double[,] values)
    {
        if (!AnyBlocked(values) || !double.IsFinite(width)) return false;
        var (min, max) = Range(values);
        double label = LabelSize;
        double needed = Number(NumberText.Short(min), label, Foreground).Width + 8 + RampBarWidth(width) + 8
                        + Number(NumberText.Short(max), label, Foreground).Width + 24 + (Text("Ag", label, Foreground).Height + 8)
                        + Text(BlockedText, label, Foreground).Width;
        return needed > width;
    }

    private FormattedText? CaptionText(double width)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(RowAxisTitle)) parts.Add($"Rows ↓ {RowAxisTitle}");
        if (!string.IsNullOrWhiteSpace(ColumnAxisTitle)) parts.Add($"Columns → {ColumnAxisTitle}");
        if (parts.Count == 0) return null;
        return Text(string.Join("     ", parts), LabelSize, MutedBrush, maxWidth: double.IsFinite(width) ? Math.Max(50, width) : 0);
    }

    private FormattedText ReadoutText(double width)
    {
        string text = SelectedCell is { } cell && HasData && cell.Row >= 0 && cell.Column >= 0
                      && cell.Row < Values!.GetLength(0) && cell.Column < Values.GetLength(1)
            ? Describe(cell)
            : "Click a square, or use the arrow keys, to see its exact value.";
        var formatted = Text(text, FontSize * 0.9, Foreground, maxWidth: double.IsFinite(width) ? Math.Max(50, width) : 0);
        int colon = text.LastIndexOf(": ", StringComparison.Ordinal);
        if (SelectedCell != null && colon > 0 && !text.EndsWith(BlockedText, StringComparison.Ordinal))
        {
            formatted.SetFontTypeface(BoldNumberFace, colon + 2, text.Length - colon - 2);
        }
        return formatted;
    }

    private double ValueSize => LabelSize * 0.8;

    /// <summary>Width of a typical number written in a square with this many decimals (a negative one, the widest).</summary>
    private double ValueWidth(int decimals) => Number(NumberText.ShortWithDecimals(-0.5, decimals), ValueSize, Foreground).Width;

    private (double Min, double Max) Range(double[,] values)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (double v in values)
        {
            if (!double.IsFinite(v)) continue;
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        if (!double.IsNaN(Minimum)) min = Minimum;
        if (!double.IsNaN(Maximum)) max = Maximum;
        if (!double.IsFinite(min) || !double.IsFinite(max)) (min, max) = (0, 1);
        return (min, max);
    }

    private Color RampColor(double v, double min, double max)
    {
        double t = max > min ? (v - min) / (max - min) : 0.5;
        if (double.IsPositiveInfinity(v)) t = 1;
        t = Math.Clamp(t, 0, 1);
        return t < 0.5 ? ChartMath.Mix(RampLowColor, RampMidColor, t * 2) : ChartMath.Mix(RampMidColor, RampHighColor, (t - 0.5) * 2);
    }

    private Brush HatchBrush()
    {
        var pen = new Pen(BlockedLineBrush, 1.5);
        var drawing = new DrawingGroup();
        using (var g = drawing.Open())
        {
            g.DrawRectangle(BlockedBrush, null, new Rect(0, 0, 10, 10));
            g.DrawLine(pen, new Point(0, 0), new Point(10, 10));
            g.DrawLine(pen, new Point(10, 0), new Point(0, 10));
        }
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 10, 10), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None,
        };
    }

    private static Rect Inflate(Rect r, double by)
    {
        r.Inflate(by, by);
        return r;
    }

    private GridCell? CellAt(Point p)
    {
        if (!HasData) return null;
        var l = ComputeLayout(ActualWidth);
        int c = (int)Math.Floor((p.X - l.GridLeft) / l.Cell), r = (int)Math.Floor((p.Y - l.GridTop) / l.Cell);
        return r >= 0 && c >= 0 && r < Values!.GetLength(0) && c < Values.GetLength(1) ? new GridCell(r, c) : null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (CellAt(e.GetPosition(this)) is { } cell) SelectedCell = cell;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        ToolTip = CellAt(e.GetPosition(this)) is { } cell ? Describe(cell) : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HasData) return;
        int rows = Values!.GetLength(0), cols = Values.GetLength(1);
        var cell = SelectedCell ?? new GridCell(0, 0);
        (int dr, int dc) = e.Key switch { Key.Up => (-1, 0), Key.Down => (1, 0), Key.Left => (0, -1), Key.Right => (0, 1), _ => (0, 0) };
        if (dr == 0 && dc == 0) return;
        SelectedCell = SelectedCell == null ? cell
            : new GridCell(Math.Clamp(cell.Row + dr, 0, rows - 1), Math.Clamp(cell.Column + dc, 0, cols - 1));
        e.Handled = true;
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(HeatMap), new FrameworkPropertyMetadata(fallback, Relayout));

    private static DependencyProperty RegisterColor(string name, Color fallback) =>
        DependencyProperty.Register(name, typeof(Color), typeof(HeatMap), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));
}
