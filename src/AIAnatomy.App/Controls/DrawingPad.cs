using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A square pad to draw a digit on with the mouse or a pen (WPF turns pen and touch input into mouse input), for the image
/// networks lab (lesson 4.7). Each press-drag-release adds a stroke, drawn as a thick round line while it is made.
/// <see cref="Strokes"/> holds them in pad units: the square runs 0..1 each way, y downward, so the drawing survives a
/// change of size (a text-size switch) and goes straight to <c>DigitImage.FromStrokes</c> through <see cref="ToDigitStrokes"/>.
/// <see cref="StrokesChanged"/> is raised when a stroke is finished, when <see cref="Clear"/> empties the pad, and when
/// <see cref="Strokes"/> is set. The pad can take the keyboard focus but cannot be drawn on from the keyboard: each lab that
/// uses it offers ready-made samples instead. An empty pad says "Draw here" in muted words.
/// </summary>
public sealed class DrawingPad : ChartBase
{
    private static readonly IReadOnlyList<IReadOnlyList<Point>> NoStrokes = [];

    public static readonly DependencyProperty StrokesProperty = DependencyProperty.Register(nameof(Strokes),
        typeof(IReadOnlyList<IReadOnlyList<Point>>), typeof(DrawingPad),
        new FrameworkPropertyMetadata(NoStrokes, FrameworkPropertyMetadataOptions.AffectsRender, OnStrokesChanged));

    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(nameof(BorderBrush), typeof(Brush),
        typeof(DrawingPad), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The words shown on an empty pad.</summary>
    public const string EmptyText = "Draw here";

    /// <summary>What a screen reader hears on reaching the pad.</summary>
    public const string HelpText = "Draw a digit here with the mouse or a pen. From the keyboard, choose one of the samples instead.";

    // The smallest move, in pad units, that adds a point to the stroke being drawn.
    private const double MinStep = 0.004;

    private List<Point>? _current;   // the stroke being drawn, in pad units

    public DrawingPad()
    {
        Focusable = true;
        Cursor = Cursors.Pen;
        SetResourceReference(BorderBrushProperty, "BorderBrush");
        AutomationProperties.SetName(this, "Drawing pad");
        AutomationProperties.SetHelpText(this, HelpText);
    }

    /// <summary>The finished strokes, in pad units (0..1 each way, y downward). Never null.</summary>
    public IReadOnlyList<IReadOnlyList<Point>> Strokes
    {
        get => (IReadOnlyList<IReadOnlyList<Point>>)GetValue(StrokesProperty);
        set => SetValue(StrokesProperty, value ?? NoStrokes);
    }

    public Brush BorderBrush { get => (Brush)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>Raised when the strokes change: a stroke finished, the pad cleared, or <see cref="Strokes"/> set.</summary>
    public event EventHandler? StrokesChanged;

    /// <summary>Whether the pad has no strokes (finished or being drawn).</summary>
    public bool IsEmpty => Strokes.Count == 0 && _current is null;

    /// <summary>Empties the pad (and drops a stroke being drawn).</summary>
    public void Clear()
    {
        _current = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (Strokes.Count == 0) InvalidateVisual();
        else Strokes = NoStrokes;
    }

    /// <summary>The strokes in the form <c>DigitImage.FromStrokes</c> takes.</summary>
    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> ToDigitStrokes(IReadOnlyList<IReadOnlyList<Point>> strokes) =>
        strokes.Select(s => (IReadOnlyList<(double X, double Y)>)s.Select(p => (p.X, p.Y)).ToArray()).ToArray();

    /// <summary>The pad's side: it is square (measured at 14 times the font size, at least 220, within the room given).</summary>
    private double Side => Math.Max(1, Math.Min(ActualWidth, ActualHeight));

    // The pen drawn on screen; only for show (the digit image draws its own pen from the strokes' centre lines).
    private double InkWidth => Math.Max(4, Side * 0.05);

    protected override Size MeasureOverride(Size available)
    {
        double side = Math.Max(220, FontSize * 14);
        side = Math.Min(side, FiniteOr(available.Width, side));
        side = Math.Min(side, FiniteOr(available.Height, side));
        return new Size(side, side);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double side = Side;
        dc.DrawRectangle(SurfaceBrush, new Pen(BorderBrush, 1.5), new Rect(0.75, 0.75, side - 1.5, side - 1.5));
        if (IsEmpty)
        {
            var note = Text(EmptyText, FontSize, MutedBrush, maxWidth: Math.Max(1, side - 8));
            dc.DrawText(note, new Point((side - note.Width) / 2, (side - note.Height) / 2));
            return;
        }
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, side, side)));
        var pen = new Pen(Foreground, InkWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        foreach (var stroke in Strokes) DrawStroke(dc, pen, stroke, side);
        if (_current is not null) DrawStroke(dc, pen, _current, side);
        dc.Pop();
    }

    private void DrawStroke(DrawingContext dc, Pen pen, IReadOnlyList<Point> stroke, double side)
    {
        if (stroke.Count == 0) return;
        Point At(Point p) => new(p.X * side, p.Y * side);
        if (stroke.Count == 1)
        {
            dc.DrawEllipse(Foreground, null, At(stroke[0]), pen.Thickness / 2, pen.Thickness / 2);
            return;
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(At(stroke[0]), false, false);
            g.PolyLineTo(stroke.Skip(1).Select(At).ToList(), true, true);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>The pointer's place in pad units, kept inside the pad.</summary>
    private Point PadPoint(MouseEventArgs e)
    {
        var at = e.GetPosition(this);
        double side = Side;
        return new Point(Math.Clamp(at.X / side, 0, 1), Math.Clamp(at.Y / side, 0, 1));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var at = e.GetPosition(this);
        if (at.X > Side || at.Y > Side) return;   // outside the square
        _current = [PadPoint(e)];
        CaptureMouse();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_current is null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = PadPoint(e);
        if ((p - _current[^1]).Length < MinStep) return;
        _current.Add(p);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_current is null) return;
        var p = PadPoint(e);
        if ((p - _current[^1]).Length >= MinStep) _current.Add(p);
        FinishStroke();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        FinishStroke();   // capture taken away mid-stroke (a window switch): keep what was drawn
    }

    private void FinishStroke()
    {
        var stroke = _current;
        if (stroke is null) return;
        _current = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Strokes = [.. Strokes, stroke.ToArray()];
    }

    private static void OnStrokesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pad = (DrawingPad)d;
        pad.StrokesChanged?.Invoke(pad, EventArgs.Empty);
    }
}
