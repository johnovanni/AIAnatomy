using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Base of the drawn controls. Colours come from the theme (resource references, so a theme swap redraws), text
/// sizes from the inherited font size, and numbers are drawn in the monospaced <c>NumberFont</c>.
/// </summary>
public abstract partial class ChartBase : FrameworkElement
{
    private const FrameworkPropertyMetadataOptions Redraw = FrameworkPropertyMetadataOptions.AffectsRender;
    private const FrameworkPropertyMetadataOptions Relayout =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(ChartBase),
        new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.Inherits | Relayout));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(ChartBase),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | Redraw));

    public static readonly DependencyProperty MutedBrushProperty = BrushProperty(nameof(MutedBrush));
    public static readonly DependencyProperty AxisBrushProperty = BrushProperty(nameof(AxisBrush));
    public static readonly DependencyProperty GridBrushProperty = BrushProperty(nameof(GridBrush));
    public static readonly DependencyProperty SurfaceBrushProperty = BrushProperty(nameof(SurfaceBrush));
    public static readonly DependencyProperty HighlightBrushProperty = BrushProperty(nameof(HighlightBrush));
    public static readonly DependencyProperty FocusBrushProperty = BrushProperty(nameof(FocusBrush));
    public static readonly DependencyProperty LinkBrushProperty = BrushProperty(nameof(LinkBrush));
    public static readonly DependencyProperty Series1BrushProperty = BrushProperty(nameof(Series1Brush));
    public static readonly DependencyProperty Series2BrushProperty = BrushProperty(nameof(Series2Brush));
    public static readonly DependencyProperty Series3BrushProperty = BrushProperty(nameof(Series3Brush));
    public static readonly DependencyProperty Series4BrushProperty = BrushProperty(nameof(Series4Brush));

    protected static readonly Typeface TextFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    protected static readonly Typeface BoldFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    protected static readonly Typeface NumberFace = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    protected static readonly Typeface BoldNumberFace = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    protected ChartBase()
    {
        SetResourceReference(ForegroundProperty, "TextBrush");
        SetResourceReference(MutedBrushProperty, "MutedTextBrush");
        SetResourceReference(AxisBrushProperty, "AxisBrush");
        SetResourceReference(GridBrushProperty, "GridLineBrush");
        SetResourceReference(SurfaceBrushProperty, "SurfaceBrush");
        SetResourceReference(HighlightBrushProperty, "HighlightBrush");
        SetResourceReference(FocusBrushProperty, "FocusBrush");
        SetResourceReference(LinkBrushProperty, "LinkBrush");
        SetResourceReference(Series1BrushProperty, "Series1Brush");
        SetResourceReference(Series2BrushProperty, "Series2Brush");
        SetResourceReference(Series3BrushProperty, "Series3Brush");
        SetResourceReference(Series4BrushProperty, "Series4Brush");
        SetResourceReference(FocusVisualStyleProperty, "InnerFocusRing");
        SnapsToDevicePixels = true;
    }

    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Brush MutedBrush { get => (Brush)GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public Brush AxisBrush { get => (Brush)GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush SurfaceBrush { get => (Brush)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public Brush HighlightBrush { get => (Brush)GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }
    public Brush FocusBrush { get => (Brush)GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public Brush LinkBrush { get => (Brush)GetValue(LinkBrushProperty); set => SetValue(LinkBrushProperty, value); }
    public Brush Series1Brush { get => (Brush)GetValue(Series1BrushProperty); set => SetValue(Series1BrushProperty, value); }
    public Brush Series2Brush { get => (Brush)GetValue(Series2BrushProperty); set => SetValue(Series2BrushProperty, value); }
    public Brush Series3Brush { get => (Brush)GetValue(Series3BrushProperty); set => SetValue(Series3BrushProperty, value); }
    public Brush Series4Brush { get => (Brush)GetValue(Series4BrushProperty); set => SetValue(Series4BrushProperty, value); }

    /// <summary>Labels inside a chart: a little smaller than the body text.</summary>
    protected double LabelSize => Math.Max(12, FontSize * 0.8);

    protected Brush SeriesBrush(int index) => (index % 4) switch
    {
        0 => Series1Brush,
        1 => Series2Brush,
        2 => Series3Brush,
        _ => Series4Brush,
    };

    protected FormattedText Text(string text, double size, Brush brush, Typeface? face = null, double maxWidth = 0)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face ?? TextFace, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (maxWidth > 0) formatted.MaxTextWidth = maxWidth;
        return formatted;
    }

    protected FormattedText Number(string text, double size, Brush brush, bool bold = false) =>
        Text(text, size, brush, bold ? BoldNumberFace : NumberFace);

    /// <summary>Width the control asks for when its parent gives it unlimited room.</summary>
    protected static double FiniteOr(double value, double fallback) => double.IsFinite(value) ? value : fallback;

    /// <summary>The quiet note shown when a chart has nothing to draw yet.</summary>
    protected void DrawNoData(DrawingContext dc)
    {
        var note = Text("No data yet", LabelSize, MutedBrush, maxWidth: Math.Max(1, ActualWidth));
        dc.DrawText(note, new Point((ActualWidth - note.Width) / 2, Math.Max(0, (ActualHeight - note.Height) / 2)));
    }

    protected Size NoDataSize(Size available) =>
        new(FiniteOr(available.Width, 300), Math.Min(FiniteOr(available.Height, double.MaxValue), LabelSize * 3));

    /// <summary>Draws a frame behind everything so the control is clickable everywhere.</summary>
    protected void DrawBackground(DrawingContext dc) =>
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

    private static DependencyProperty BrushProperty(string name) =>
        DependencyProperty.Register(name, typeof(Brush), typeof(ChartBase), new FrameworkPropertyMetadata(Brushes.Gray, Redraw));
}
