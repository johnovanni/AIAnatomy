using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A list of 2-D maps (such as the feature maps of a convolution layer, or a memory laid out as a grid) drawn as small
/// heat maps in a wrapping grid, each with a caption in words ("map 3"). Colours follow a symmetric diverging ramp:
/// blue below zero, near-white at zero, red above zero, with the same scale (−L to +L) for every map so they can be
/// compared, shown in a legend with its numbers. Point at a square, or use the arrow keys (Page Up and Page Down move
/// between maps, Home and End go to the first and last), and the line under the grid gives the map, row, column and
/// exact value in text. The maps scroll (never clip) when the control's height is limited, as at large text sizes;
/// the legend and the line stay in view.
/// </summary>
public sealed class FeatureMapGrid : UserControl
{
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

    public static readonly DependencyProperty MapsProperty = Register<IReadOnlyList<double[,]>?>(nameof(Maps), null);
    public static readonly DependencyProperty CaptionsProperty = Register<IReadOnlyList<string>?>(nameof(Captions), null);
    public static readonly DependencyProperty LimitProperty = Register(nameof(Limit), double.NaN);
    public static readonly DependencyProperty MapSizeProperty = Register(nameof(MapSize), double.NaN);
    public static readonly DependencyProperty NegativeColorProperty = Register(nameof(NegativeColor), Color.FromRgb(0x21, 0x66, 0xAC));
    public static readonly DependencyProperty ZeroColorProperty = Register(nameof(ZeroColor), Color.FromRgb(0xF7, 0xF7, 0xF7));
    public static readonly DependencyProperty PositiveColorProperty = Register(nameof(PositiveColor), Color.FromRgb(0xB2, 0x18, 0x2B));

    private static readonly DependencyPropertyKey ReadoutTextKey = DependencyProperty.RegisterReadOnly(nameof(ReadoutText), typeof(string),
        typeof(FeatureMapGrid), new PropertyMetadata(HintText));
    public static readonly DependencyProperty ReadoutTextProperty = ReadoutTextKey.DependencyProperty;

    public const string HintText = "Point at a square, or use the arrow keys, to read its value (Page Up and Page Down change the map).";

    private readonly FeatureMapSurface _surface;
    private readonly FeatureMapLegend _legend;
    private readonly TextBlock _readout;

    public FeatureMapGrid()
    {
        SetResourceReference(NegativeColorProperty, "DivergingLowColor");
        SetResourceReference(ZeroColorProperty, "DivergingMidColor");
        SetResourceReference(PositiveColorProperty, "DivergingHighColor");
        Focusable = false;
        IsTabStop = false;

        _surface = new FeatureMapSurface(this);
        _legend = new FeatureMapLegend(this) { Margin = new Thickness(0, 8, 0, 0) };
        _readout = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontSize = FontSize * 0.9 };
        _readout.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        _readout.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ReadoutText)) { Source = this });
        // Read out (politely) when the keyboard moves the square, not on every pointer movement (as in the reward lab).
        _readout.SetValue(AutomationProperties.LiveSettingProperty, AutomationLiveSetting.Polite);

        var scroller = new ScrollViewer
        {
            Content = _surface,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,   // the maps are the tab stop; the wheel and the scroll bar still scroll
        };
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_readout, Dock.Bottom);
        DockPanel.SetDock(_legend, Dock.Bottom);
        panel.Children.Add(_readout);
        panel.Children.Add(_legend);
        panel.Children.Add(scroller);
        Content = panel;
    }

    /// <summary>The maps, each [rows, columns]. They may differ in size; each is drawn in a square tile.</summary>
    public IReadOnlyList<double[,]>? Maps { get => (IReadOnlyList<double[,]>?)GetValue(MapsProperty); set => SetValue(MapsProperty, value); }
    /// <summary>Each map's caption; "map 1", "map 2" … (counting from 1) when not given or too short.</summary>
    public IReadOnlyList<string>? Captions { get => (IReadOnlyList<string>?)GetValue(CaptionsProperty); set => SetValue(CaptionsProperty, value); }
    /// <summary>The scale runs from −Limit to +Limit (values beyond are drawn at the ends). NaN (the default) means the
    /// largest size of any value shown, so the scale stays symmetric about zero.</summary>
    public double Limit { get => (double)GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
    /// <summary>The side of each map's tile in pixels; NaN (the default) grows with the text size.</summary>
    public double MapSize { get => (double)GetValue(MapSizeProperty); set => SetValue(MapSizeProperty, value); }
    public Color NegativeColor { get => (Color)GetValue(NegativeColorProperty); set => SetValue(NegativeColorProperty, value); }
    public Color ZeroColor { get => (Color)GetValue(ZeroColorProperty); set => SetValue(ZeroColorProperty, value); }
    public Color PositiveColor { get => (Color)GetValue(PositiveColorProperty); set => SetValue(PositiveColorProperty, value); }
    /// <summary>The line under the grid: the square under the pointer or the keyboard square, or a hint.</summary>
    public string ReadoutText => (string)GetValue(ReadoutTextProperty);

    internal bool HasData => Maps is { Count: > 0 } maps && maps.All(m => m is { } && m.GetLength(0) > 0 && m.GetLength(1) > 0);

    internal string Caption(int map) => Captions is { } c && map < c.Count && !string.IsNullOrWhiteSpace(c[map]) ? c[map] : $"map {map + 1}";

    /// <summary>The end of the scale, L: the scale is −L to +L.</summary>
    internal double ScaleLimit
    {
        get
        {
            if (double.IsFinite(Limit) && Limit > 0) return Limit;
            double largest = 0;
            if (HasData)
                foreach (var map in Maps!)
                    foreach (double v in map)
                        if (double.IsFinite(v)) largest = Math.Max(largest, Math.Abs(v));
            return largest > 0 ? largest : 1;
        }
    }

    internal Color ColorFor(double v, double limit)
    {
        double t = Math.Clamp(v / limit, -1, 1);
        return t < 0 ? ChartMath.Mix(ZeroColor, NegativeColor, -t) : ChartMath.Mix(ZeroColor, PositiveColor, t);
    }

    /// <summary>The readout for one square (all numbers counted from 1).</summary>
    public string Describe(int map, GridCell cell)
    {
        var values = Maps![map];
        double v = values[cell.Row, cell.Column];
        return $"{Capitalised(Caption(map))}, row {cell.Row + 1} of {values.GetLength(0)}, column {cell.Column + 1} of {values.GetLength(1)}: "
               + NumberText.Exact(v);
    }

    private static string Capitalised(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;

    internal void SetReadout(string text, bool fromKeyboard)
    {
        string previous = ReadoutText;
        SetValue(ReadoutTextKey, text);
        if (fromKeyboard && !string.Equals(previous, text, StringComparison.Ordinal) && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            (UIElementAutomationPeer.FromElement(_readout) ?? UIElementAutomationPeer.CreatePeerForElement(_readout))
                ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_surface == null) return;
        if (e.Property == MapsProperty || e.Property == CaptionsProperty) _surface.DataChanged();
        if (e.Property == MapsProperty || e.Property == CaptionsProperty || e.Property == LimitProperty || e.Property == MapSizeProperty
            || e.Property == NegativeColorProperty || e.Property == ZeroColorProperty || e.Property == PositiveColorProperty
            || e.Property == FontSizeProperty)
        {
            _surface.InvalidateMeasure();
            _surface.InvalidateVisual();
            _legend.InvalidateMeasure();
            _legend.InvalidateVisual();
        }
        if (e.Property == FontSizeProperty) _readout.FontSize = FontSize * 0.9;   // as the other charts' readouts
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(FeatureMapGrid), new FrameworkPropertyMetadata(fallback, Relayout));
}

/// <summary>The maps themselves, inside the grid's scroll area: the tab stop that takes the arrow keys.</summary>
internal sealed class FeatureMapSurface : ChartBase
{
    private const double Gap = 14;
    private readonly FeatureMapGrid _owner;
    private int _map;
    private GridCell _cell;
    private (int Map, GridCell Cell)? _hover;

    public FeatureMapSurface(FeatureMapGrid owner)
    {
        _owner = owner;
        Focusable = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        ToolTipService.SetInitialShowDelay(this, 200);
        AutomationProperties.SetName(this, "Maps");
        AutomationProperties.SetHelpText(this, FeatureMapGrid.HintText);
    }

    private IReadOnlyList<double[,]> Maps => _owner.Maps!;

    public void DataChanged()
    {
        if (_owner.HasData)
        {
            _map = Math.Clamp(_map, 0, Maps.Count - 1);
            var m = Maps[_map];
            _cell = new GridCell(Math.Clamp(_cell.Row, 0, m.GetLength(0) - 1), Math.Clamp(_cell.Column, 0, m.GetLength(1) - 1));
        }
        _hover = null;
        UpdateReadout(false);
    }

    private sealed record Layout(double Tile, double CaptionHeight, int Columns, double ItemWidth, double ItemHeight);

    private Layout ComputeLayout(double width)
    {
        double tile = double.IsFinite(_owner.MapSize) && _owner.MapSize > 0 ? _owner.MapSize : Math.Max(72, FontSize * 4.5);
        if (_owner.HasData) tile = Math.Max(tile, Maps.Max(m => Math.Max(m.GetLength(0), m.GetLength(1))) * 2);   // at least 2 pixels a square
        double caption = Text("Ag", LabelSize, Foreground).Height;
        double widestCaption = _owner.HasData ? Enumerable.Range(0, Maps.Count).Max(i => Text(_owner.Caption(i), LabelSize, Foreground, BoldFace).Width) : 0;
        double itemWidth = Math.Max(tile, Math.Min(widestCaption, tile * 2)) + Gap;
        int columns = double.IsFinite(width) ? Math.Max(1, (int)Math.Floor((width + Gap) / itemWidth)) : Math.Min(8, Math.Max(1, _owner.Maps?.Count ?? 1));
        // Long captions wrap within the item; the item is as tall as its tallest caption.
        double captionHeight = _owner.HasData
            ? Enumerable.Range(0, Maps.Count).Max(i => Text(_owner.Caption(i), LabelSize, Foreground, BoldFace, itemWidth - Gap).Height)
            : caption;
        return new Layout(tile, captionHeight, columns, itemWidth, tile + 4 + captionHeight + Gap);
    }

    private Rect TileRect(Layout l, int map) =>
        new((map % l.Columns) * l.ItemWidth, (map / l.Columns) * l.ItemHeight + 3, l.Tile, l.Tile);

    /// <summary>Where a map's squares are inside its tile: square cells, centred.</summary>
    private static (Rect Area, double Cell) MapArea(Rect tile, double[,] map)
    {
        int rows = map.GetLength(0), cols = map.GetLength(1);
        double cell = Math.Min(tile.Width / cols, tile.Height / rows);
        double w = cell * cols, h = cell * rows;
        return (new Rect(tile.X + (tile.Width - w) / 2, tile.Y + (tile.Height - h) / 2, w, h), cell);
    }

    protected override Size MeasureOverride(Size available)
    {
        if (!_owner.HasData) return NoDataSize(available);
        var l = ComputeLayout(available.Width);
        int rows = (Maps.Count + l.Columns - 1) / l.Columns;
        double width = double.IsFinite(available.Width) ? available.Width : l.Columns * l.ItemWidth;
        return new Size(width, rows * l.ItemHeight + 3);
    }

    protected override void OnRender(DrawingContext dc)
    {
        DrawBackground(dc);
        if (!_owner.HasData) { DrawNoData(dc); return; }
        var l = ComputeLayout(ActualWidth);
        double limit = _owner.ScaleLimit;
        var outline = new Pen(AxisBrush, 1);
        for (int i = 0; i < Maps.Count; i++)
        {
            var map = Maps[i];
            var tile = TileRect(l, i);
            var (area, cell) = MapArea(tile, map);
            dc.DrawImage(Bitmap(map, limit), area);
            bool current = IsKeyboardFocused && i == _map;
            dc.DrawRectangle(null, current ? new Pen(Foreground, 3) : outline, area);
            var caption = Text(_owner.Caption(i), LabelSize, Foreground, current ? BoldFace : TextFace, l.ItemWidth - Gap);
            dc.DrawText(caption, new Point(tile.X, tile.Bottom + 4));

            if (_hover is { } h && h.Map == i) DrawCellMark(dc, area, cell, h.Cell, false);
            if (current) DrawCellMark(dc, area, cell, _cell, true);
        }
    }

    private void DrawCellMark(DrawingContext dc, Rect area, double cell, GridCell at, bool keyboard)
    {
        var rect = new Rect(area.X + at.Column * cell, area.Y + at.Row * cell, cell, cell);
        // Never smaller than a few pixels, so the mark is seen on tiny squares; the inner line keeps it visible on any colour.
        double grow = Math.Max(0, (8 - cell) / 2);
        rect.Inflate(grow, grow);
        dc.DrawRectangle(null, new Pen(SurfaceBrush, keyboard ? 5 : 4), rect);
        dc.DrawRectangle(null, new Pen(HighlightBrush, keyboard ? 3 : 2) { DashStyle = keyboard ? DashStyles.Solid : DashStyles.Dash }, rect);
    }

    private BitmapSource Bitmap(double[,] map, double limit)
    {
        int rows = map.GetLength(0), cols = map.GetLength(1);
        var pixels = new byte[rows * cols * 4];
        var blocked = (BlockedColor as SolidColorBrush)?.Color ?? Colors.Gray;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            double v = map[r, c];
            var colour = double.IsFinite(v) ? _owner.ColorFor(v, limit) : blocked;
            int k = (r * cols + c) * 4;
            pixels[k] = colour.B;
            pixels[k + 1] = colour.G;
            pixels[k + 2] = colour.R;
            pixels[k + 3] = 255;
        }
        var bitmap = BitmapSource.Create(cols, rows, 96, 96, PixelFormats.Bgra32, null, pixels, cols * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private Brush BlockedColor => TryFindResource("BlockedBrush") as Brush ?? Brushes.Gray;   // squares that are not a number

    private (int Map, GridCell Cell)? CellAt(Point p)
    {
        if (!_owner.HasData) return null;
        var l = ComputeLayout(ActualWidth);
        for (int i = 0; i < Maps.Count; i++)
        {
            var (area, cell) = MapArea(TileRect(l, i), Maps[i]);
            if (!area.Contains(p)) continue;
            int r = Math.Min(Maps[i].GetLength(0) - 1, (int)((p.Y - area.Y) / cell));
            int c = Math.Min(Maps[i].GetLength(1) - 1, (int)((p.X - area.X) / cell));
            return (i, new GridCell(r, c));
        }
        return null;
    }

    private void UpdateReadout(bool fromKeyboard)
    {
        if (!_owner.HasData)
        {
            ToolTip = null;
            _owner.SetReadout(FeatureMapGrid.HintText, false);
            AutomationProperties.SetHelpText(this, FeatureMapGrid.HintText);
            return;
        }
        ToolTip = _hover is { } h ? _owner.Describe(h.Map, h.Cell) : null;
        string keyboard = _owner.Describe(_map, _cell);
        string text = _hover is { } hover ? _owner.Describe(hover.Map, hover.Cell) : IsKeyboardFocused ? keyboard : FeatureMapGrid.HintText;
        _owner.SetReadout(text, fromKeyboard);
        AutomationProperties.SetHelpText(this, keyboard + ". Arrow keys move the square; Page Up and Page Down change the map.");
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (CellAt(e.GetPosition(this)) is { } hit) (_map, _cell) = hit;
        UpdateReadout(false);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = CellAt(e.GetPosition(this));
        if (hit == _hover) return;
        _hover = hit;
        UpdateReadout(false);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        UpdateReadout(false);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_owner.HasData) return;
        int map = _map;
        var cell = _cell;
        switch (e.Key)
        {
            case Key.Up: cell = cell with { Row = cell.Row - 1 }; break;
            case Key.Down: cell = cell with { Row = cell.Row + 1 }; break;
            case Key.Left: cell = cell with { Column = cell.Column - 1 }; break;
            case Key.Right: cell = cell with { Column = cell.Column + 1 }; break;
            case Key.PageUp: map--; break;
            case Key.PageDown: map++; break;
            case Key.Home: map = 0; break;
            case Key.End: map = Maps.Count - 1; break;
            default: return;
        }
        _map = Math.Clamp(map, 0, Maps.Count - 1);
        var values = Maps[_map];
        _cell = new GridCell(Math.Clamp(cell.Row, 0, values.GetLength(0) - 1), Math.Clamp(cell.Column, 0, values.GetLength(1) - 1));
        _hover = null;   // the keyboard square is what the line under the grid now names
        UpdateReadout(true);
        InvalidateVisual();
        var l = ComputeLayout(ActualWidth);
        var tile = TileRect(l, _map);
        BringIntoView(new Rect(tile.X, tile.Y - 3, l.ItemWidth, l.ItemHeight));
        e.Handled = true;
    }

    protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusedChanged(e);
        UpdateReadout(false);
        InvalidateVisual();   // the keyboard square shows only while the maps have the keyboard
    }
}

/// <summary>The grid's colour scale: −L, the ramp, +L, with zero marked in the middle and the two ends named in words.</summary>
internal sealed class FeatureMapLegend : ChartBase
{
    private readonly FeatureMapGrid _owner;

    public FeatureMapLegend(FeatureMapGrid owner) => _owner = owner;

    private (FormattedText Low, FormattedText High, FormattedText Zero, FormattedText Words) Texts(double width)
    {
        double limit = _owner.ScaleLimit;
        return (Number(NumberText.Short(-limit), LabelSize, Foreground), Number("+" + NumberText.Short(limit), LabelSize, Foreground),
            Number("0", LabelSize, Foreground),
            Text("The same scale for every map: blue below zero, white at zero, red above zero, darker further from zero.",
                LabelSize, MutedBrush, maxWidth: Math.Max(50, FiniteOr(width, 600))));
    }

    private double BarHeight => Text("Ag", LabelSize, Foreground).Height;

    protected override Size MeasureOverride(Size available)
    {
        if (!_owner.HasData) return new Size(0, 0);
        var t = Texts(available.Width);
        return new Size(FiniteOr(available.Width, 400), BarHeight + 2 + t.Zero.Height + 4 + t.Words.Height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_owner.HasData) return;
        var t = Texts(ActualWidth);
        double bar = BarHeight;
        double barWidth = Math.Clamp(ActualWidth - t.Low.Width - t.High.Width - 16, 40, 320);
        double x = 0;
        dc.DrawText(t.Low, new Point(x, (bar - t.Low.Height) / 2));
        x += t.Low.Width + 8;
        var ramp = new LinearGradientBrush(new GradientStopCollection
        {
            new(_owner.NegativeColor, 0), new(_owner.ZeroColor, 0.5), new(_owner.PositiveColor, 1),
        }, 0);
        var rect = new Rect(x, 0, barWidth, bar);
        dc.DrawRectangle(ramp, new Pen(AxisBrush, 1), rect);
        double middle = rect.X + rect.Width / 2;
        dc.DrawLine(new Pen(AxisBrush, 1.5), new Point(middle, rect.Bottom - 4), new Point(middle, rect.Bottom + 3));
        dc.DrawText(t.Zero, new Point(middle - t.Zero.Width / 2, rect.Bottom + 2));
        dc.DrawText(t.High, new Point(rect.Right + 8, (bar - t.High.Height) / 2));
        dc.DrawText(t.Words, new Point(0, rect.Bottom + 2 + t.Zero.Height + 4));
    }
}
