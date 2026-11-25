using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Models.Reinforcement;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Visuals;
using GridCell = AIAnatomy.Models.Reinforcement.GridCell;

namespace AIAnatomy.App.Controls;

/// <summary>A square of the maze that the learner clicked, or chose with the keyboard and pressed Space on.</summary>
public sealed class MazeSquareEventArgs(GridCell cell) : EventArgs
{
    public GridCell Cell { get; } = cell;
}

/// <summary>
/// The learning-by-reward lab's maze (lesson 4.5). Each open square is shaded on a single-hue ramp by the agent's value,
/// scaled between the lowest and highest value on the grid (darker is higher), with its value written in it and an arrow
/// for the move it prefers. Walls are hatched, not just coloured; the start and the goal are lettered S and G; the agent
/// is a ring. A click, or Space or Enter on the keyboard square, asks for the lab's tool to be used there; the arrow keys
/// move the keyboard square. <see cref="ReadoutText"/> names the square under the pointer (or the keyboard square), for
/// a line under the grid.
/// </summary>
public sealed class GridWorldView : ChartBase
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
    private const double Gap = 10;

    public const string HintText =
        "Point at a square to see its value. Click a square, or Tab to the maze, move with the arrow keys and press Space, to use the tool.";

    public static readonly DependencyProperty SquaresProperty = DependencyProperty.Register(nameof(Squares), typeof(IReadOnlyList<MazeSquare>),
        typeof(GridWorldView), new FrameworkPropertyMetadata(null, Relayout, (d, _) => ((GridWorldView)d).UpdateReadout()));

    private static readonly DependencyPropertyKey ReadoutTextKey = DependencyProperty.RegisterReadOnly(nameof(ReadoutText), typeof(string),
        typeof(GridWorldView), new FrameworkPropertyMetadata(HintText));
    public static readonly DependencyProperty ReadoutTextProperty = ReadoutTextKey.DependencyProperty;

    public static readonly DependencyProperty BlockedBrushProperty = DependencyProperty.Register(nameof(BlockedBrush), typeof(Brush),
        typeof(GridWorldView), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BlockedLineBrushProperty = DependencyProperty.Register(nameof(BlockedLineBrush), typeof(Brush),
        typeof(GridWorldView), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RampLowColorProperty = RegisterColor(nameof(RampLowColor), Colors.White);
    public static readonly DependencyProperty RampMidColorProperty = RegisterColor(nameof(RampMidColor), Colors.LightBlue);
    public static readonly DependencyProperty RampHighColorProperty = RegisterColor(nameof(RampHighColor), Colors.Navy);

    private GridCell? _hover;
    private GridCell _focus = RewardLabViewModel.DefaultStart;

    public GridWorldView()
    {
        Focusable = true;
        ToolTipService.SetInitialShowDelay(this, 200);
        SetResourceReference(BlockedBrushProperty, "BlockedBrush");
        SetResourceReference(BlockedLineBrushProperty, "BlockedLineBrush");
        SetResourceReference(RampLowColorProperty, "RampLowColor");
        SetResourceReference(RampMidColorProperty, "RampMidColor");
        SetResourceReference(RampHighColorProperty, "RampHighColor");
    }

    /// <summary>The squares, row by row from the top left (<see cref="RewardLabViewModel.Squares"/>).</summary>
    public IReadOnlyList<MazeSquare>? Squares { get => (IReadOnlyList<MazeSquare>?)GetValue(SquaresProperty); set => SetValue(SquaresProperty, value); }

    /// <summary>"Square 3, 2: value 4.58, prefers right" for the square under the pointer, else the keyboard square while
    /// the maze has the keyboard, else <see cref="HintText"/>.</summary>
    public string ReadoutText => (string)GetValue(ReadoutTextProperty);

    public Brush BlockedBrush { get => (Brush)GetValue(BlockedBrushProperty); set => SetValue(BlockedBrushProperty, value); }
    public Brush BlockedLineBrush { get => (Brush)GetValue(BlockedLineBrushProperty); set => SetValue(BlockedLineBrushProperty, value); }
    public Color RampLowColor { get => (Color)GetValue(RampLowColorProperty); set => SetValue(RampLowColorProperty, value); }
    public Color RampMidColor { get => (Color)GetValue(RampMidColorProperty); set => SetValue(RampMidColorProperty, value); }
    public Color RampHighColor { get => (Color)GetValue(RampHighColorProperty); set => SetValue(RampHighColorProperty, value); }

    /// <summary>A click, or Space or Enter on the keyboard square, asks for the tool to be used on that square.</summary>
    public event EventHandler<MazeSquareEventArgs>? ApplyRequested;

    private const int Columns = RewardLabViewModel.Columns, Rows = RewardLabViewModel.Rows;

    private bool HasData => Squares is { Count: Columns * Rows };

    // Squares grow with the text, so the numbers in them stay readable at 24 pt; the pane's width is the limit.
    private double Cell(double width)
    {
        double wanted = Math.Max(Math.Max(80, ValueWidth() + 20), LabelSize * 5);
        return double.IsFinite(width) ? Math.Clamp(Math.Floor((width - 2) / Columns), 12, wanted) : wanted;
    }

    private double ValueSize => LabelSize;

    private double ValueWidth() => Number("−20.00", ValueSize, Foreground).Width;

    private double LineHeight => Text("Ag", LabelSize, Foreground).Height;

    private double LegendHeight => LineHeight + 8;

    protected override Size MeasureOverride(Size available)
    {
        double cell = Cell(available.Width);
        double width = double.IsFinite(available.Width) ? available.Width : Columns * cell + 2;
        return new Size(width, Rows * cell + 2 + Gap + LegendHeight);
    }

    private Rect GridRect()
    {
        double cell = Cell(ActualWidth);
        return new Rect(1, 1, Columns * cell, Rows * cell);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!HasData) { DrawNoData(dc); return; }
        var squares = Squares!;
        var grid = GridRect();
        double cell = grid.Width / Columns;
        var (min, max) = Range(squares);
        var hatch = HatchBrush();
        bool values = ValueWidth() + 6 <= cell;
        var axis = new Pen(AxisBrush, 1);

        foreach (var s in squares)
        {
            var rect = new Rect(grid.X + s.Cell.Column * cell, grid.Y + s.Cell.Row * cell, cell, cell);
            if (s.IsWall)
            {
                dc.DrawRectangle(hatch, axis, rect);
                continue;
            }
            if (s.IsGoal)
            {
                // The goal ends the episode and has no value of its own, so it is not shaded: a plain square with a
                // double frame and a large G.
                dc.DrawRectangle(SurfaceBrush, axis, rect);
                dc.DrawRectangle(null, new Pen(Foreground, 3), Inflate(rect, -3.5));
                var g = Text("G", LabelSize * 1.6, Foreground, BoldFace);
                dc.DrawText(g, new Point(rect.X + (cell - g.Width) / 2, rect.Y + (cell - g.Height) / 2));
                continue;
            }
            var colour = RampColor(s.Value, min, max);
            var ink = ChartMath.TextOn(colour);
            dc.DrawRectangle(new SolidColorBrush(colour), axis, rect);
            if (s.IsStart) dc.DrawText(Text("S", LabelSize, ink, BoldFace), new Point(rect.X + 4, rect.Y + 2));

            // The arrow and the ring sit a little right of and below the middle, clear of the S in the corner.
            var centre = new Point(rect.X + cell * 0.55, rect.Y + cell * 0.40);
            if (s.Arrow is { } move) DrawArrow(dc, centre, move, cell * 0.18, new Pen(ink, Math.Max(2, cell / 28)));
            if (s.HasAgent)
            {
                double r = cell * 0.26;
                dc.DrawEllipse(null, new Pen(ink, Math.Max(3, cell / 18)), centre, r, r);
                var other = ReferenceEquals(ink, Brushes.Black) ? Brushes.White : Brushes.Black;
                dc.DrawEllipse(null, new Pen(other, 1.5), centre, r - Math.Max(3, cell / 18) / 2 - 0.75, r - Math.Max(3, cell / 18) / 2 - 0.75);
            }
            if (values)
            {
                var text = Number(s.ValueText, ValueSize, ink);
                dc.DrawText(text, new Point(rect.X + (cell - text.Width) / 2, rect.Bottom - text.Height - 2));
            }
        }
        dc.DrawRectangle(null, new Pen(AxisBrush, 1.5), grid);

        if (IsKeyboardFocused)
        {
            var rect = new Rect(grid.X + _focus.Column * cell, grid.Y + _focus.Row * cell, cell, cell);
            dc.DrawRectangle(null, new Pen(SurfaceBrush, 2), Inflate(rect, -3));
            dc.DrawRectangle(null, new Pen(FocusBrush, 4), Inflate(rect, -1));
        }

        DrawLegend(dc, grid.Bottom + Gap, min, max, hatch);
    }

    /// <summary>The scale (lowest value, the ramp, highest value), then the wall swatch: hatching, never colour alone.</summary>
    private void DrawLegend(DrawingContext dc, double top, double min, double max, Brush hatch)
    {
        double label = LabelSize, h = LineHeight;
        double x = 0;
        var low = Number(NumberText.ShortWithDecimals(min, 2), label, Foreground);
        dc.DrawText(low, new Point(x, top));
        x += low.Width + 8;
        double bar = Math.Clamp(FiniteOr(ActualWidth, 600) * 0.25, 60, 220);
        var ramp = new LinearGradientBrush(new GradientStopCollection { new(RampLowColor, 0), new(RampMidColor, 0.5), new(RampHighColor, 1) }, 0);
        dc.DrawRectangle(ramp, new Pen(AxisBrush, 1), new Rect(x, top + 2, bar, h - 4));
        x += bar + 8;
        var high = Number(NumberText.ShortWithDecimals(max, 2), label, Foreground);
        dc.DrawText(high, new Point(x, top));
        x += high.Width + 24;
        var swatch = new Rect(x, top + 2, h - 4, h - 4);
        dc.DrawRectangle(hatch, new Pen(AxisBrush, 1), swatch);
        dc.DrawText(Text("wall", label, Foreground), new Point(swatch.Right + 8, top));
    }

    private static void DrawArrow(DrawingContext dc, Point c, Move move, double half, Pen pen)
    {
        var (dx, dy) = move switch { Move.Up => (0.0, -1.0), Move.Right => (1.0, 0.0), Move.Down => (0.0, 1.0), _ => (-1.0, 0.0) };
        var tail = new Point(c.X - dx * half, c.Y - dy * half);
        var tip = new Point(c.X + dx * half, c.Y + dy * half);
        dc.DrawLine(pen, tail, tip);
        double head = half * 0.7;
        // The two sides of the head, at 45 degrees back from the tip.
        var left = new Point(tip.X - dx * head - dy * head, tip.Y - dy * head + dx * head);
        var right = new Point(tip.X - dx * head + dy * head, tip.Y - dy * head - dx * head);
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(left, true, true);
            g.PolyLineTo([tip, right], true, true);
        }
        geometry.Freeze();
        dc.DrawGeometry(pen.Brush, pen, geometry);
    }

    // Between the lowest and highest value of the open squares other than the goal (whose value is always 0).
    private static (double Min, double Max) Range(IReadOnlyList<MazeSquare> squares)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (var s in squares)
        {
            if (s.IsWall || s.IsGoal || !double.IsFinite(s.Value)) continue;
            min = Math.Min(min, s.Value);
            max = Math.Max(max, s.Value);
        }
        return double.IsFinite(min) ? (min, max) : (0, 0);
    }

    private Color RampColor(double v, double min, double max)
    {
        double t = max > min ? Math.Clamp((v - min) / (max - min), 0, 1) : 0;
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
        var grid = GridRect();
        double cell = grid.Width / Columns;
        int c = (int)Math.Floor((p.X - grid.X) / cell), r = (int)Math.Floor((p.Y - grid.Y) / cell);
        return c >= 0 && r >= 0 && c < Columns && r < Rows ? new GridCell(c, r) : null;
    }

    private MazeSquare? SquareAt(GridCell cell) =>
        HasData ? Squares![cell.Row * Columns + cell.Column] : null;

    private void UpdateReadout()
    {
        string text = _hover is { } hover && SquareAt(hover) is { } h ? h.AutomationName
            : IsKeyboardFocused && SquareAt(_focus) is { } f ? f.AutomationName
            : HintText;
        SetValue(ReadoutTextKey, text);
        AutomationProperties.SetHelpText(this, SquareAt(_focus)?.AutomationName ?? HintText);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (CellAt(e.GetPosition(this)) is { } cell)
        {
            _focus = cell;
            ApplyRequested?.Invoke(this, new MazeSquareEventArgs(cell));
            UpdateReadout();
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var cell = CellAt(e.GetPosition(this));
        if (cell == _hover) return;
        _hover = cell;
        ToolTip = cell is { } c && SquareAt(c) is { } s ? s.AutomationName : null;
        UpdateReadout();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        UpdateReadout();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HasData) return;
        switch (e.Key)
        {
            case Key.Up: _focus = _focus with { Row = Math.Max(0, _focus.Row - 1) }; break;
            case Key.Down: _focus = _focus with { Row = Math.Min(Rows - 1, _focus.Row + 1) }; break;
            case Key.Left: _focus = _focus with { Column = Math.Max(0, _focus.Column - 1) }; break;
            case Key.Right: _focus = _focus with { Column = Math.Min(Columns - 1, _focus.Column + 1) }; break;
            case Key.Space or Key.Enter: ApplyRequested?.Invoke(this, new MazeSquareEventArgs(_focus)); break;
            default: return;
        }
        _hover = null;   // the keyboard square is what the line under the grid now names
        UpdateReadout();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusedChanged(e);
        UpdateReadout();
        InvalidateVisual();   // the keyboard square shows only while the maze has the keyboard
    }

    private static DependencyProperty RegisterColor(string name, Color fallback) =>
        DependencyProperty.Register(name, typeof(Color), typeof(GridWorldView), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));
}
