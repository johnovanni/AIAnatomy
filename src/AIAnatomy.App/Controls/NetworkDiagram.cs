using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Boxes joined by arrows, each box with a label and an optional value, each arrow with an optional label. Boxes with
/// a lesson link are underlined in the link colour; clicking one (or choosing it with the arrow keys and pressing
/// Enter) raises <see cref="LessonNavigation.NavigateRequestedEvent"/>.
/// </summary>
public sealed class NetworkDiagram : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double UnboundedHeight = 420, Padding = 10;

    public static readonly DependencyProperty NodesProperty = Register<IReadOnlyList<NetworkNode>?>(nameof(Nodes));
    public static readonly DependencyProperty EdgesProperty = Register<IReadOnlyList<NetworkEdge>?>(nameof(Edges));

    public static readonly RoutedEvent NavigateRequestedEvent = LessonNavigation.NavigateRequestedEvent.AddOwner(typeof(NetworkDiagram));

    private int _focusedLink;

    public NetworkDiagram()
    {
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 200);
    }

    public IReadOnlyList<NetworkNode>? Nodes { get => (IReadOnlyList<NetworkNode>?)GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public IReadOnlyList<NetworkEdge>? Edges { get => (IReadOnlyList<NetworkEdge>?)GetValue(EdgesProperty); set => SetValue(EdgesProperty, value); }

    public event NavigateRequestedEventHandler NavigateRequested
    {
        add => AddHandler(NavigateRequestedEvent, value);
        remove => RemoveHandler(NavigateRequestedEvent, value);
    }

    private bool HasData => Nodes is { Count: > 0 };

    private List<NetworkNode> Linked => (Nodes ?? []).Where(n => !string.IsNullOrEmpty(n.LinkLessonId)).ToList();

    protected override Size MeasureOverride(Size available)
    {
        if (!HasData) return NoDataSize(available);
        return new Size(FiniteOr(available.Width, 700), double.IsFinite(available.Height) && available.Height > 0 ? available.Height : Math.Max(UnboundedHeight, FontSize * 14));
    }

    private (FormattedText Label, FormattedText? Value) NodeTexts(NetworkNode n, double scale)
    {
        bool link = !string.IsNullOrEmpty(n.LinkLessonId);
        var label = Text(n.Label, LabelSize * scale, link ? LinkBrush : Foreground, BoldFace);
        if (link) label.SetTextDecorations(TextDecorations.Underline);
        var value = string.IsNullOrEmpty(n.Value) ? null : Number(n.Value, LabelSize * 0.85 * scale, Foreground);
        return (label, value);
    }

    /// <summary>
    /// The text scale (1 down to 0.6, never under 12 px) at which no two boxes overlap, so large text sizes still give a
    /// readable diagram; boxes that overlap even at 0.6 are drawn at 0.6.
    /// </summary>
    private double TextScale()
    {
        var nodes = Nodes!;
        for (double scale = 1; ; scale -= 0.1)
        {
            if (scale <= 0.6 || LabelSize * scale * 0.85 < 12) return Math.Max(scale, 0.6);
            var boxes = nodes.Select(n => Box(n, scale)).ToList();
            bool overlap = false;
            for (int i = 0; i < boxes.Count && !overlap; i++)
                for (int j = i + 1; j < boxes.Count && !overlap; j++)
                    overlap = Inflated(boxes[i], 6).IntersectsWith(boxes[j]);
            if (!overlap) return scale;
        }
    }

    private static Rect Inflated(Rect r, double by)
    {
        r.Inflate(by, by);
        return r;
    }

    private Rect Box(NetworkNode n, double scale)
    {
        var (label, value) = NodeTexts(n, scale);
        double w = Math.Max(label.Width, value?.Width ?? 0) + Padding * 2;
        double h = label.Height + (value?.Height ?? 0) + Padding * 1.4;
        double cx = Math.Clamp(n.X * ActualWidth, w / 2 + 2, Math.Max(w / 2 + 2, ActualWidth - w / 2 - 2));
        double cy = Math.Clamp(n.Y * ActualHeight, h / 2 + 2, Math.Max(h / 2 + 2, ActualHeight - h / 2 - 2));
        return new Rect(cx - w / 2, cy - h / 2, w, h);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        var nodes = Nodes!;
        double scale = TextScale();
        var boxes = nodes.ToDictionary(n => n.Id, n => Box(n, scale));
        var arrowPen = new Pen(AxisBrush, 2);

        var edges = Edges ?? [];
        foreach (var edge in edges)
        {
            if (!boxes.TryGetValue(edge.FromId, out var from) || !boxes.TryGetValue(edge.ToId, out var to)) continue;
            var (start, end) = Ends(edge, edges, from, to, scale);
            dc.DrawLine(arrowPen, start, end);
            DrawArrowHead(dc, start, end);
        }

        var linked = Linked;
        foreach (var node in nodes)
        {
            var box = boxes[node.Id];
            bool link = !string.IsNullOrEmpty(node.LinkLessonId);
            dc.DrawRoundedRectangle(SurfaceBrush, new Pen(link ? LinkBrush : AxisBrush, link ? 2.5 : 1.5), box, 8, 8);
            var (label, value) = NodeTexts(node, scale);
            dc.DrawText(label, new Point(box.X + (box.Width - label.Width) / 2, box.Y + Padding * 0.7));
            if (value != null) dc.DrawText(value, new Point(box.X + (box.Width - value.Width) / 2, box.Y + Padding * 0.7 + label.Height));
            if (IsKeyboardFocused && linked.Count > 0 && linked[Math.Min(_focusedLink, linked.Count - 1)] == node)
            {
                var ring = box;
                dc.DrawRoundedRectangle(null, new Pen(FocusBrush, 3), Inflated(ring, 5), 10, 10);
            }
        }

        // Arrow labels last, so no box paints over them, and placed clear of every box and of the labels already placed.
        var placed = new List<Rect>(boxes.Values);
        foreach (var edge in edges)
        {
            if (string.IsNullOrEmpty(edge.Label) || !boxes.TryGetValue(edge.FromId, out var from) || !boxes.TryGetValue(edge.ToId, out var to)) continue;
            var text = Text(edge.Label, LabelSize * 0.85 * scale, MutedBrush);
            var (start, end) = Ends(edge, edges, from, to, scale);
            var plate = LabelPlate(start, end, text, placed, ActualWidth, ActualHeight);
            placed.Add(plate);
            dc.DrawRoundedRectangle(SurfaceBrush, null, plate, 4, 4);
            dc.DrawText(text, new Point(plate.X + 4, plate.Y + 1));
        }
    }

    /// <summary>
    /// Where an arrow starts and ends. Two arrows between the same boxes in opposite directions ("reads" and "add back")
    /// are drawn side by side, each moved to its own right, so they never lie on top of each other (final review M1).
    /// </summary>
    private (Point Start, Point End) Ends(NetworkEdge edge, IReadOnlyList<NetworkEdge> edges, Rect from, Rect to, double scale)
    {
        var start = EdgePoint(from, Centre(to));
        var end = EdgePoint(to, Centre(from));
        if (!edges.Any(o => o.FromId == edge.ToId && o.ToId == edge.FromId)) return (start, end);
        var along = end - start;
        if (along.Length < 1e-9) return (start, end);
        along.Normalize();
        var right = new Vector(-along.Y, along.X) * Math.Max(8, LabelSize * 0.45 * scale);
        return (EdgePoint(from, Centre(to) + right) + right, EdgePoint(to, Centre(from) + right) + right);
    }

    /// <summary>
    /// Where an arrow's label goes: at the middle of the arrow or at 0.35 or 0.65 of its length, on it or moved sideways
    /// step by step, nearest first, at the first place that touches no box; if none is clear, the place that covers boxes
    /// least (never simply the middle, which at 32 pt put a label over the Feed-forward box).
    /// </summary>
    private static Rect LabelPlate(Point start, Point end, FormattedText text, IEnumerable<Rect> boxes, double areaWidth, double areaHeight)
    {
        double w = text.Width + 8, h = text.Height + 2;
        // The search is WPF-free (LabelPlacement.ForArrow, tested): along the arrow and sideways, clear of every box
        // (each kept 2 px away), or else the place that covers least.
        var obstacles = boxes.Select(b => Inflated(b, 2)).Select(b => new LabelBox(b.X, b.Y, b.Width, b.Height));
        var (x, y) = LabelPlacement.ForArrow((start.X, start.Y), (end.X, end.Y), w, h, areaWidth, areaHeight, obstacles);
        return new Rect(x, y, w, h);
    }

    private static Point Centre(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    /// <summary>Where the line from the centre of <paramref name="box"/> towards <paramref name="towards"/> leaves the box.</summary>
    private static Point EdgePoint(Rect box, Point towards)
    {
        var c = Centre(box);
        double dx = towards.X - c.X, dy = towards.Y - c.Y;
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return c;
        double scale = Math.Min(Math.Abs(dx) > 1e-9 ? box.Width / 2 / Math.Abs(dx) : double.MaxValue,
                                Math.Abs(dy) > 1e-9 ? box.Height / 2 / Math.Abs(dy) : double.MaxValue);
        return new Point(c.X + dx * scale, c.Y + dy * scale);
    }

    private void DrawArrowHead(DrawingContext dc, Point start, Point end)
    {
        var direction = end - start;
        if (direction.Length < 1) return;
        direction.Normalize();
        var side = new Vector(-direction.Y, direction.X);
        double size = Math.Max(10, LabelSize * 0.5);
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(end, true, true);
            g.LineTo(end - direction * size + side * size * 0.5, true, true);
            g.LineTo(end - direction * size - side * size * 0.5, true, true);
        }
        geometry.Freeze();
        dc.DrawGeometry(AxisBrush, null, geometry);
    }

    private NetworkNode? NodeAt(Point p)
    {
        if (!HasData) return null;
        double scale = TextScale();
        return Nodes!.LastOrDefault(n => Box(n, scale).Contains(p));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (NodeAt(e.GetPosition(this)) is { LinkLessonId: { Length: > 0 } id })
        {
            RaiseEvent(new NavigateRequestedEventArgs(NavigateRequestedEvent, this, id));
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var node = NodeAt(e.GetPosition(this));
        Cursor = node is { LinkLessonId: { Length: > 0 } } ? Cursors.Hand : null;
        ToolTip = node == null ? null
            : node.LinkLessonId is { Length: > 0 } ? $"{node.Label}{(node.Value != null ? ": " + node.Value : "")}. Click to open its lesson."
            : node.Value != null ? $"{node.Label}: {node.Value}" : null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var linked = Linked;
        if (linked.Count == 0) return;
        switch (e.Key)
        {
            case Key.Right or Key.Down: _focusedLink = (_focusedLink + 1) % linked.Count; break;
            case Key.Left or Key.Up: _focusedLink = (_focusedLink - 1 + linked.Count) % linked.Count; break;
            case Key.Enter or Key.Space:
                RaiseEvent(new NavigateRequestedEventArgs(NavigateRequestedEvent, this, linked[Math.Min(_focusedLink, linked.Count - 1)].LinkLessonId!));
                break;
            default: return;
        }
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }

    private static DependencyProperty Register<T>(string name) =>
        DependencyProperty.Register(name, typeof(T), typeof(NetworkDiagram), new FrameworkPropertyMetadata(null, Relayout));
}
