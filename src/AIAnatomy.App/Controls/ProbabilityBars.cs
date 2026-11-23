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

    public ProbabilityBars() => ToolTipService.SetInitialShowDelay(this, 200);

    public IReadOnlyList<LabeledValue>? Items { get => (IReadOnlyList<LabeledValue>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    /// <summary>The label of the chosen item, or null.</summary>
    public string? HighlightedLabel { get => (string?)GetValue(HighlightedLabelProperty); set => SetValue(HighlightedLabelProperty, value); }

    /// <summary>The items in the order drawn: largest probability first (ties keep their order).</summary>
    public IReadOnlyList<LabeledValue> Sorted => (Items ?? []).OrderByDescending(i => i.Value).ToList();

    private double RowHeight => Number("Ag", LabelSize, Foreground).Height * 1.6;

    private (double LabelWidth, double PercentWidth, double TagWidth) Columns()
    {
        var items = Sorted;
        double label = items.Max(i => Number(i.Label, LabelSize, Foreground, bold: true).Width) + 16;
        double percent = Number("100.00%", LabelSize, Foreground, bold: true).Width + 16;
        double tag = Text(ChosenTag, LabelSize, Foreground, BoldFace).Width + 28;
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
            double p = double.IsFinite(item.Value) ? Math.Clamp(item.Value, 0, 1) : 0;
            dc.DrawRectangle(chosen ? Series2Brush : Series1Brush, null, new Rect(track.X, track.Y, track.Width * p, track.Height));

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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var items = Sorted;
        int i = (int)Math.Floor((e.GetPosition(this).Y - 2) / RowHeight);
        ToolTip = i >= 0 && i < items.Count
            ? $"“{items[i].Label}”: probability {NumberText.Exact(items[i].Value)}" + (items[i].Label == HighlightedLabel ? " (chosen)" : "")
            : null;
    }
}
