using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A list of numbers (a vector) as a strip of bars: above the line for positive numbers, below it for negative ones,
/// with the height showing the size. Numbers are written under the bars when they fit, and the readout gives the
/// selected number exactly. Click a bar or use the arrow keys to select it. Long vectors wrap onto more rows.
/// </summary>
public sealed class VectorStrip : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double MaxCell = 72, MinCell = 10, Gap = 8;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(double[]),
        typeof(VectorStrip), new FrameworkPropertyMetadata(null, Relayout));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int),
        typeof(VectorStrip), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | Relayout));

    public VectorStrip()
    {
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 200);
    }

    public double[]? Values { get => (double[]?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    /// <summary>The selected number (0-based), or −1 for none.</summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    private bool HasData => Values is { Length: > 0 };

    private sealed record Layout(double Cell, int PerRow, int Rows, double BandHeight, double IndexHeight, bool WriteValues,
        double ValueHeight, int IndexEvery, double LegendTop, double LegendHeight, double ReadoutTop, double ReadoutHeight)
    {
        public double RowHeight => BandHeight + IndexHeight + ValueHeight + 8;
    }

    private Layout ComputeLayout(double width)
    {
        int n = Values!.Length;
        double room = double.IsFinite(width) ? width : n * 40;
        double cell = Math.Clamp(room / n, MinCell, MaxCell);
        int perRow = Math.Max(1, (int)Math.Floor(room / cell));
        int rows = (n + perRow - 1) / perRow;
        double label = LabelSize;
        double band = label * 3;
        var sample = Number(NumberText.Short(-0.5), label * 0.8, Foreground);
        bool writeValues = sample.Width + 4 <= cell;
        double indexWidth = Number((n - 1).ToString(), label * 0.8, Foreground).Width + 6;
        int every = new[] { 1, 2, 5, 10, 20, 50, 100, 200, 500 }.FirstOrDefault(k => k * cell >= indexWidth, 1000);
        double indexHeight = Number("0", label * 0.8, Foreground).Height + 2;
        double valueHeight = writeValues ? sample.Height + 2 : 0;
        double rowHeight = band + indexHeight + valueHeight + 8;
        double legendTop = rows * rowHeight + Gap;
        double legendHeight = LegendText(room).Height;
        double readoutTop = legendTop + legendHeight + Gap;
        return new Layout(cell, perRow, rows, band, indexHeight, writeValues, valueHeight, every, legendTop, legendHeight,
            readoutTop, ReadoutText(room).Height);
    }

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        var l = ComputeLayout(available.Width);
        return new Size(double.IsFinite(available.Width) ? available.Width : l.PerRow * l.Cell, l.ReadoutTop + l.ReadoutHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        var values = Values!;
        var l = ComputeLayout(ActualWidth);
        double largest = values.Where(double.IsFinite).Select(Math.Abs).DefaultIfEmpty(0).Max();
        if (largest == 0) largest = 1;
        double small = LabelSize * 0.8;

        for (int row = 0; row < l.Rows; row++)
        {
            double top = row * l.RowHeight;
            double mid = top + l.BandHeight / 2;
            int first = row * l.PerRow, last = Math.Min(values.Length, first + l.PerRow);
            dc.DrawRectangle(null, new Pen(GridBrush, 1), new Rect(0, top, (last - first) * l.Cell, l.BandHeight));
            for (int i = first; i < last; i++)
            {
                double x = (i - first) * l.Cell;
                double v = values[i];
                double h = double.IsFinite(v) ? Math.Abs(v) / largest * (l.BandHeight / 2 - 2) : 0;
                var bar = v >= 0 ? new Rect(x + 1, mid - h, Math.Max(1, l.Cell - 2), h) : new Rect(x + 1, mid, Math.Max(1, l.Cell - 2), h);
                dc.DrawRectangle(v >= 0 ? Series1Brush : Series2Brush, null, bar);
                if (i == SelectedIndex)
                    dc.DrawRectangle(null, new Pen(HighlightBrush, 3), new Rect(x + 1.5, top + 1.5, Math.Max(1, l.Cell - 3), l.BandHeight - 3));
                if (i % l.IndexEvery == 0)
                {
                    var index = Number(i.ToString(), small, MutedBrush, bold: i == SelectedIndex);
                    dc.DrawText(index, new Point(x + (l.Cell - index.Width) / 2, top + l.BandHeight + 2));
                }
                if (l.WriteValues)
                {
                    var text = Number(NumberText.Short(v), small, Foreground, bold: i == SelectedIndex);
                    dc.DrawText(text, new Point(x + (l.Cell - text.Width) / 2, top + l.BandHeight + l.IndexHeight + 2));
                }
            }
            dc.DrawLine(new Pen(AxisBrush, 1.5), new Point(0, mid), new Point((last - first) * l.Cell, mid));
        }

        dc.DrawText(LegendText(ActualWidth), new Point(0, l.LegendTop));
        dc.DrawText(ReadoutText(ActualWidth), new Point(0, l.ReadoutTop));
    }

    private FormattedText LegendText(double width)
    {
        var values = Values!;
        double largest = values.Where(double.IsFinite).Select(Math.Abs).DefaultIfEmpty(0).Max();
        return Text($"{values.Length} numbers. Above the line: positive; below: negative. A full bar is {NumberText.Short(largest)}.",
            LabelSize, MutedBrush, maxWidth: Math.Max(50, width));
    }

    private FormattedText ReadoutText(double width)
    {
        var values = Values!;
        if (SelectedIndex < 0 || SelectedIndex >= values.Length)
            return Text("Click a bar, or use the arrow keys, to see its exact value.", FontSize * 0.9, Foreground, maxWidth: Math.Max(50, width));
        string prefix = $"Number {SelectedIndex} (counting from 0): ";
        var text = Text(prefix + NumberText.Exact(values[SelectedIndex]), FontSize * 0.9, Foreground, maxWidth: Math.Max(50, width));
        text.SetFontTypeface(BoldNumberFace, prefix.Length, text.Text.Length - prefix.Length);
        return text;
    }

    private int IndexAt(Point p)
    {
        if (!HasData) return -1;
        var l = ComputeLayout(ActualWidth);
        int row = (int)Math.Floor(p.Y / l.RowHeight), col = (int)Math.Floor(p.X / l.Cell);
        if (row < 0 || row >= l.Rows || col < 0 || col >= l.PerRow || p.Y - row * l.RowHeight > l.BandHeight) return -1;
        int i = row * l.PerRow + col;
        return i < Values!.Length ? i : -1;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (IndexAt(e.GetPosition(this)) is var i and >= 0) SelectedIndex = i;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = IndexAt(e.GetPosition(this));
        ToolTip = i >= 0 ? $"Number {i}: {NumberText.Exact(Values![i])}" : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HasData) return;
        int step = e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 };
        if (step == 0) return;
        SelectedIndex = SelectedIndex < 0 ? 0 : Math.Clamp(SelectedIndex + step, 0, Values!.Length - 1);
        e.Handled = true;
    }
}
