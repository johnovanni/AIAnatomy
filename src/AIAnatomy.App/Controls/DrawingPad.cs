using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A square pad to draw a digit on with the mouse, a pen or a finger, for the image networks lab (lesson 4.7). Pen input
/// arrives as mouse input (flicks and press-and-hold are turned off on the pad); touch is handled as touch and captured, so
/// a finger drag draws instead of panning a surrounding ScrollViewer. Each press-drag-release adds a stroke, drawn as a
/// thick round line while it is made; a press and release without a drag adds a one-point stroke, a dot.
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
    private TouchDevice? _touch;     // the finger drawing it, when it is drawn by touch

    public DrawingPad()
    {
        Focusable = true;
        Cursor = Cursors.Pen;
        // A pen drag draws: no flick gestures, and no press-and-hold right-click circle.
        Stylus.SetIsFlicksEnabled(this, false);
        Stylus.SetIsPressAndHoldEnabled(this, false);
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
        var touch = _touch;
        _current = null;   // first, so losing the captures below finishes nothing
        _touch = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (touch is not null && touch.Captured == this) ReleaseTouchCapture(touch);
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

    /// <summary>A place in pad units, kept inside the pad.</summary>
    private Point PadPoint(Point at)
    {
        double side = Side;
        return new Point(Math.Clamp(at.X / side, 0, 1), Math.Clamp(at.Y / side, 0, 1));
    }

    private bool InsideSquare(Point at) => at.X <= Side && at.Y <= Side;

    private void StartStroke(Point at)
    {
        Focus();
        _current = [PadPoint(at)];
        InvalidateVisual();
    }

    private void ExtendStroke(Point at)
    {
        if (_current is null) return;
        var p = PadPoint(at);
        if ((p - _current[^1]).Length < MinStep) return;
        _current.Add(p);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_touch is not null || !InsideSquare(e.GetPosition(this))) return;   // a finger is drawing, or outside the square
        StartStroke(e.GetPosition(this));
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_current is null || _touch is not null) return;
        // The button was released where no Up event reached the pad (outside the window, say): the stroke ends here.
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FinishStroke();
            return;
        }
        ExtendStroke(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_current is null || _touch is not null) return;
        ExtendStroke(e.GetPosition(this));
        FinishStroke();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_touch is null) FinishStroke();   // capture taken away mid-stroke (a window switch): keep what was drawn
    }

    // Touch is handled as touch (captured, and the events marked handled), so a finger drawing on the pad is not promoted to
    // a pan of a surrounding ScrollViewer. Only one finger draws at a time.
    protected override void OnTouchDown(TouchEventArgs e)
    {
        base.OnTouchDown(e);
        e.Handled = true;
        var at = e.GetTouchPoint(this).Position;
        if (_current is not null || !InsideSquare(at)) return;
        _touch = e.TouchDevice;
        CaptureTouch(e.TouchDevice);
        StartStroke(at);
    }

    protected override void OnTouchMove(TouchEventArgs e)
    {
        base.OnTouchMove(e);
        e.Handled = true;
        if (e.TouchDevice == _touch) ExtendStroke(e.GetTouchPoint(this).Position);
    }

    protected override void OnTouchUp(TouchEventArgs e)
    {
        base.OnTouchUp(e);
        e.Handled = true;
        if (e.TouchDevice != _touch) return;
        ExtendStroke(e.GetTouchPoint(this).Position);
        FinishStroke();
    }

    protected override void OnLostTouchCapture(TouchEventArgs e)
    {
        base.OnLostTouchCapture(e);
        if (e.TouchDevice == _touch) FinishStroke();
    }

    private void FinishStroke()
    {
        var stroke = _current;
        var touch = _touch;
        _current = null;
        _touch = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (touch is not null && touch.Captured == this) ReleaseTouchCapture(touch);
        if (stroke is null) return;
        Strokes = [.. Strokes, stroke.ToArray()];
    }

    private static void OnStrokesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pad = (DrawingPad)d;
        pad.StrokesChanged?.Invoke(pad, EventArgs.Empty);
    }
}
