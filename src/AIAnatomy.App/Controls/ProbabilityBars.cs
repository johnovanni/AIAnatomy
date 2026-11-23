using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Choices and their probabilities as horizontal bars, largest first. Each bar has its text and its percentage; a full
/// bar is 100%. The <see cref="HighlightedLabel"/> (the chosen item) has a thick outline, bold text and a "chosen" tag.
/// Hovering a bar shows its exact probability.
/// </summary>
public sealed class ProbabilityBars : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    public const string ChosenTag = "chosen";

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IReadOnlyList<LabeledValue>),
        typeof(ProbabilityBars), new FrameworkPropertyMetadata(null, Relayout));

    public static readonly DependencyProperty HighlightedLabelProperty = DependencyProperty.Register(nameof(HighlightedLabel), typeof(string),
        typeof(ProbabilityBars), new FrameworkPropertyMetadata(null, Relayout));

    public static readonly DependencyProperty ScaleMaximumProperty = DependencyProperty.Register(nameof(ScaleMaximum), typeof(double),
        typeof(ProbabilityBars), new FrameworkPropertyMetadata(1.0, Relayout));

    public static readonly DependencyProperty KeepOrderProperty = DependencyProperty.Register(nameof(KeepOrder), typeof(bool),
        typeof(ProbabilityBars), new FrameworkPropertyMetadata(false, Relayout));

    public ProbabilityBars() => ToolTipService.SetInitialShowDelay(this, 200);

    public IReadOnlyList<LabeledValue>? Items { get => (IReadOnlyList<LabeledValue>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    /// <summary>The label of the chosen item, or null.</summary>
    public string? HighlightedLabel { get => (string?)GetValue(HighlightedLabelProperty); set => SetValue(HighlightedLabelProperty, value); }

    /// <summary>The value a full bar stands for (default 1, which is 100%). Charts that are compared side by side give the same value.</summary>
    public double ScaleMaximum { get => (double)GetValue(ScaleMaximumProperty); set => SetValue(ScaleMaximumProperty, value); }
    /// <summary>Draw the items in the order given instead of largest first, so two charts can line up row by row.</summary>
    public bool KeepOrder { get => (bool)GetValue(KeepOrderProperty); set => SetValue(KeepOrderProperty, value); }

    private double Scale => double.IsFinite(ScaleMaximum) && ScaleMaximum > 0 ? ScaleMaximum : 1;
    private bool HasTag => !string.IsNullOrEmpty(HighlightedLabel);

    /// <summary>The items in the order drawn: largest probability first (ties keep their order), or as given with <see cref="KeepOrder"/>.</summary>
    public IReadOnlyList<LabeledValue> Sorted => KeepOrder ? (Items ?? []).ToList() : (Items ?? []).OrderByDescending(i => i.Value).ToList();

    private double RowHeight => Number("Ag", LabelSize, Foreground).Height * 1.6;

    private (double LabelWidth, double PercentWidth, double TagWidth) Columns()
    {
        var items = Sorted;
        double label = items.Max(i => Number(i.Label, LabelSize, Foreground, bold: true).Width) + 16;
        double percent = Number("100.00%", LabelSize, Foreground, bold: true).Width + 16;
        double tag = HasTag ? Text(ChosenTag, LabelSize, Foreground, BoldFace).Width + 28 : 0;
        return (label, percent, tag);
    }

    protected override Size MeasureOverride(Size available)
    {
        if (Items is not { Count: > 0 }) return NoDataSize(available);
        var (label, percent, tag) = Columns();
        double width = FiniteOr(available.Width, label + 300 + percent + tag);
        return new Size(width, Items.Count * RowHeight + 4);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (Items is not { Count: > 0 }) { DrawNoData(dc); return; }
        var items = Sorted;
        var (labelWidth, percentWidth, tagWidth) = Columns();
        double rowHeight = RowHeight;
        double barLeft = labelWidth;
        double barRoom = Math.Max(20, ActualWidth - labelWidth - percentWidth - tagWidth);
        var gridPen = new Pen(GridBrush, 1);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            bool chosen = item.Label == HighlightedLabel;
            double top = i * rowHeight + 2;
            double mid = top + rowHeight / 2;

            var label = Number(item.Label, LabelSize, Foreground, bold: chosen);
            dc.DrawText(label, new Point(labelWidth - 12 - label.Width, mid - label.Height / 2));

            var track = new Rect(barLeft, top + rowHeight * 0.18, barRoom, rowHeight * 0.64);
            dc.DrawRectangle(null, gridPen, track);
            double p = double.IsFinite(item.Value) ? Math.Clamp(item.Value / Scale, 0, 1) : 0;
            dc.DrawRectangle(chosen ? Series2Brush : Series1Brush, null, new Rect(track.X, track.Y, track.Width * p, track.Height));
            if (double.IsFinite(item.Value) && item.Value > Scale) DrawCutOff(dc, track);

            var percent = Number(NumberText.Percent(item.Value), LabelSize, Foreground, bold: chosen);
            dc.DrawText(percent, new Point(track.Right + percentWidth - 8 - percent.Width, mid - percent.Height / 2));

            if (chosen)
            {
                var tag = Text(ChosenTag, LabelSize, Foreground, BoldFace);
                double tagLeft = track.Right + percentWidth + 8;
                dc.DrawRoundedRectangle(null, new Pen(HighlightBrush, 2), new Rect(tagLeft, mid - tag.Height / 2 - 2, tag.Width + 16, tag.Height + 4), 6, 6);
                dc.DrawText(tag, new Point(tagLeft + 8, mid - tag.Height / 2));
                dc.DrawRoundedRectangle(null, new Pen(HighlightBrush, 3), new Rect(1.5, top + 1.5, ActualWidth - 3, rowHeight - 3), 6, 6);
            }
        }
    }

    public const string CutOffTag = "cut off";

    /// <summary>
    /// A bar longer than the scale is drawn full width with a break near its end (two slanted gaps) and a "cut off" tag,
    /// so a clipped bar never looks like a true full-scale bar, whatever the colours (final review I5).
    /// </summary>
    private void DrawCutOff(DrawingContext dc, Rect track)
    {
        double h = track.Height;
        double x = track.Right - Math.Max(14, h * 0.9);
        var gap = new Pen(SurfaceBrush, Math.Max(3, h * 0.14));
        var edge = new Pen(Foreground, 1.5);
        for (int k = 0; k < 2; k++)
        {
            double dx = x + k * Math.Max(6, h * 0.3);
            dc.DrawLine(gap, new Point(dx + h * 0.25, track.Top - 2), new Point(dx - h * 0.25, track.Bottom + 2));
            dc.DrawLine(edge, new Point(dx + h * 0.25 + 2.5, track.Top - 2), new Point(dx - h * 0.25 + 2.5, track.Bottom + 2));
        }
        var tag = Text(CutOffTag, LabelSize * 0.8, Foreground, BoldFace);
        var box = new Rect(x - h * 0.4 - tag.Width - 14, track.Top + (h - tag.Height) / 2 - 1, tag.Width + 10, tag.Height + 2);
        // Too narrow for the words: the break alone still shows the cut (the caption names only the break; the tooltip says it too).
        if (box.Left <= track.Left) return;
        dc.DrawRoundedRectangle(SurfaceBrush, new Pen(Foreground, 1), box, 4, 4);
        dc.DrawText(tag, new Point(box.Left + 5, box.Top + 1));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var items = Sorted;
        int i = (int)Math.Floor((e.GetPosition(this).Y - 2) / RowHeight);
        ToolTip = i >= 0 && i < items.Count
            ? $"“{items[i].Label}”: probability {NumberText.Exact(items[i].Value)}" + (items[i].Label == HighlightedLabel ? " (chosen)" : "")
              + (items[i].Value > Scale ? " (longer than a full bar, so the bar is cut off)" : "")
            : null;
    }
}
