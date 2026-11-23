using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Steps one after another, top to bottom with arrows between them. Each step has a title, a plain description, and
/// either a vector of numbers or a list of named numbers. The <see cref="SelectedIndex"/> step has a thick outline
/// and a "current step" tag. Hovering a step shows all of its numbers exactly.
/// </summary>
public sealed class PipelineView : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double Pad = 12, ArrowGap = 34;
    public const string CurrentTag = "current step";

    public static readonly DependencyProperty StagesProperty = DependencyProperty.Register(nameof(Stages), typeof(IReadOnlyList<PipelineStage>),
        typeof(PipelineView), new FrameworkPropertyMetadata(null, Relayout));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int),
        typeof(PipelineView), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | Relayout, OnSelectedChanged));

    public PipelineView()
    {
        ToolTipService.SetInitialShowDelay(this, 300);
        Focusable = true;
    }

    /// <summary>Scrolls the newly marked step into view, so stepping with the keyboard never leaves it off screen.</summary>
    private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (PipelineView)d;
        view.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (view.Stages is not { Count: > 0 } stages || view.SelectedIndex < 0 || view.SelectedIndex >= stages.Count || view.ActualWidth <= 0) return;
            view.BringIntoView(view.ComputeLayout(view.ActualWidth)[view.SelectedIndex].Box);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public IReadOnlyList<PipelineStage>? Stages { get => (IReadOnlyList<PipelineStage>?)GetValue(StagesProperty); set => SetValue(StagesProperty, value); }
    /// <summary>The step to mark as current, or −1.</summary>
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    private sealed record StageLayout(Rect Box, FormattedText Title, FormattedText Description, FormattedText? Values,
        IReadOnlyList<(FormattedText Label, FormattedText Value)> Items, double ItemLabelWidth);

    private List<StageLayout> ComputeLayout(double width)
    {
        var result = new List<StageLayout>();
        double inner = Math.Max(60, width - Pad * 2 - 4);
        double y = 0;
        var stages = Stages!;
        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            bool current = i == SelectedIndex;
            var title = Text($"{i + 1}. {s.Title}", LabelSize * 1.05, Foreground, BoldFace,
                maxWidth: current ? Math.Max(40, inner - CurrentTagWidth() - 12) : inner);
            var description = Text(s.Description, LabelSize * 0.9, MutedBrush, maxWidth: inner);
            FormattedText? values = s.Values is { Length: > 0 } v ? Text(VectorText(v, inner), LabelSize * 0.85, Foreground, NumberFace, inner) : null;
            var items = (s.Items ?? []).Select(it => (Text(it.Label, LabelSize * 0.85, Foreground, NumberFace),
                Number(NumberText.Short(it.Value), LabelSize * 0.85, Foreground))).ToList();
            double labelWidth = items.Count == 0 ? 0 : items.Max(it => it.Item1.Width) + 16;
            double h = Pad + title.Height + 4 + description.Height + (values != null ? 8 + values.Height : 0)
                       + (items.Count > 0 ? 8 + items.Sum(it => Math.Max(it.Item1.Height, it.Item2.Height)) : 0) + Pad;
            result.Add(new StageLayout(new Rect(2, y + 2, Math.Max(60, width - 4), h), title, description, values, items, labelWidth));
            y += h + ArrowGap;
        }
        return result;
    }

    /// <summary>
    /// A vector as rows of 4, 8 or 16 numbers (the largest that fits), each right-aligned to the widest one, so number k
    /// is in the same place in every step.
    /// </summary>
    private string VectorText(double[] v, double inner)
    {
        var cells = v.Select(NumberText.Short).ToArray();
        int width = cells.Max(c => c.Length);
        double cell = Text(new string('0', width + 2), LabelSize * 0.85, Foreground, NumberFace).Width;
        int columns = new[] { 16, 8, 4 }.FirstOrDefault(c => c * cell <= inner, 4);
        var rows = new List<string>();
        for (int i = 0; i < cells.Length; i += columns)
            rows.Add(string.Join("  ", cells.Skip(i).Take(columns).Select(c => c.PadLeft(width))));
        return string.Join('\n', rows);
    }

    private double CurrentTagWidth() => Text(CurrentTag, LabelSize * 0.85, Foreground, BoldFace).Width + 16;

    protected override Size MeasureOverride(Size available)
    {
        if (Stages is not { Count: > 0 }) return NoDataSize(available);
        double width = FiniteOr(available.Width, 700);
        var layout = ComputeLayout(width);
        return new Size(width, layout[^1].Box.Bottom + 2);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (Stages is not { Count: > 0 }) { DrawNoData(dc); return; }
        var layout = ComputeLayout(ActualWidth);
        for (int i = 0; i < layout.Count; i++)
        {
            var s = layout[i];
            bool current = i == SelectedIndex;
            dc.DrawRoundedRectangle(SurfaceBrush, new Pen(current ? HighlightBrush : AxisBrush, current ? 4 : 1.5), s.Box, 8, 8);
            double x = s.Box.X + Pad, y = s.Box.Y + Pad;
            dc.DrawText(s.Title, new Point(x, y));
            if (current)
            {
                var tag = Text(CurrentTag, LabelSize * 0.85, Foreground, BoldFace);
                var plate = new Rect(s.Box.Right - Pad - tag.Width - 16, y, tag.Width + 16, tag.Height + 4);
                dc.DrawRoundedRectangle(null, new Pen(HighlightBrush, 2), plate, 6, 6);
                dc.DrawText(tag, new Point(plate.X + 8, plate.Y + 2));
            }
            y += s.Title.Height + 4;
            dc.DrawText(s.Description, new Point(x, y));
            y += s.Description.Height;
            if (s.Values != null)
            {
                y += 8;
                dc.DrawText(s.Values, new Point(x, y));
                y += s.Values.Height;
            }
            if (s.Items.Count > 0) y += 8;
            foreach (var (label, value) in s.Items)
            {
                dc.DrawText(label, new Point(x, y));
                dc.DrawText(value, new Point(x + s.ItemLabelWidth, y));
                y += Math.Max(label.Height, value.Height);
            }

            if (i < layout.Count - 1)
            {
                double cx = s.Box.X + 40, top = s.Box.Bottom + 4, bottom = layout[i + 1].Box.Top - 4;
                var pen = new Pen(AxisBrush, 2.5);
                dc.DrawLine(pen, new Point(cx, top), new Point(cx, bottom - 8));
                var head = new StreamGeometry();
                using (var g = head.Open())
                {
                    g.BeginFigure(new Point(cx, bottom), true, true);
                    g.LineTo(new Point(cx - 8, bottom - 12), true, true);
                    g.LineTo(new Point(cx + 8, bottom - 12), true, true);
                }
                head.Freeze();
                dc.DrawGeometry(AxisBrush, null, head);
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Stages is not { Count: > 0 } stages) { ToolTip = null; return; }
        var layout = ComputeLayout(ActualWidth);
        int i = layout.FindIndex(l => l.Box.Contains(e.GetPosition(this)));
        if (i < 0) { ToolTip = null; return; }
        var s = stages[i];
        var parts = new List<string> { s.Title };
        if (s.Values is { Length: > 0 } v) parts.Add(string.Join(",  ", v.Select(NumberText.Exact)));
        if (s.Items is { Count: > 0 } items) parts.AddRange(items.Select(it => $"{it.Label}: {NumberText.Exact(it.Value)}"));
        ToolTip = parts.Count > 1 ? string.Join("\n", parts) : null;
    }
}
