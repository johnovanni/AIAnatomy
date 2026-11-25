using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A 3 by 3 filter drawn as nine squares on the diverging ramp (blue below zero, near-white at zero, red above zero),
/// scaled to the filter's own largest size, so each filter's pattern shows however large its numbers are. Each square
/// with a clear sign also carries "+" or "−", so the sign never rests on colour alone; the exact numbers are in the
/// owner's tooltip and in the lab's editable filter. The side grows with the text size.
/// </summary>
public sealed class FilterSwatch : FrameworkElement
{
    private const FrameworkPropertyMetadataOptions Redraw = FrameworkPropertyMetadataOptions.AffectsRender;

    public static readonly DependencyProperty WeightsProperty = DependencyProperty.Register(nameof(Weights),
        typeof(IReadOnlyList<double>), typeof(FilterSwatch), new FrameworkPropertyMetadata(null, Redraw));
    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(FilterSwatch),
        new FrameworkPropertyMetadata(13.0, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | Redraw));
    public static readonly DependencyProperty NegativeColorProperty = ColorProperty(nameof(NegativeColor), Color.FromRgb(0x21, 0x66, 0xAC));
    public static readonly DependencyProperty ZeroColorProperty = ColorProperty(nameof(ZeroColor), Color.FromRgb(0xF7, 0xF7, 0xF7));
    public static readonly DependencyProperty PositiveColorProperty = ColorProperty(nameof(PositiveColor), Color.FromRgb(0xB2, 0x18, 0x2B));
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(nameof(LineBrush), typeof(Brush),
        typeof(FilterSwatch), new FrameworkPropertyMetadata(Brushes.Gray, Redraw));

    private static readonly Typeface Face = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public FilterSwatch()
    {
        SetResourceReference(NegativeColorProperty, "DivergingLowColor");
        SetResourceReference(ZeroColorProperty, "DivergingMidColor");
        SetResourceReference(PositiveColorProperty, "DivergingHighColor");
        SetResourceReference(LineBrushProperty, "BorderBrush");
    }

    /// <summary>The nine numbers, row by row.</summary>
    public IReadOnlyList<double>? Weights { get => (IReadOnlyList<double>?)GetValue(WeightsProperty); set => SetValue(WeightsProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public Color NegativeColor { get => (Color)GetValue(NegativeColorProperty); set => SetValue(NegativeColorProperty, value); }
    public Color ZeroColor { get => (Color)GetValue(ZeroColorProperty); set => SetValue(ZeroColorProperty, value); }
    public Color PositiveColor { get => (Color)GetValue(PositiveColorProperty); set => SetValue(PositiveColorProperty, value); }
    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    private double Cell => Math.Max(14, FontSize * 1.2);

    protected override Size MeasureOverride(Size availableSize) => new(3 * Cell + 1, 3 * Cell + 1);

    protected override void OnRender(DrawingContext dc)
    {
        var w = Weights;
        double cell = Cell;
        var line = new Pen(LineBrush, 1);
        if (w is not { Count: 9 })
        {
            dc.DrawRectangle(null, line, new Rect(0.5, 0.5, 3 * cell, 3 * cell));
            return;
        }
        double limit = w.Max(Math.Abs);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (int i = 0; i < 9; i++)
        {
            double t = limit > 0 ? w[i] / limit : 0;
            var colour = t < 0 ? ChartMath.Mix(ZeroColor, NegativeColor, -t) : ChartMath.Mix(ZeroColor, PositiveColor, t);
            var rect = new Rect(0.5 + i % 3 * cell, 0.5 + i / 3 * cell, cell, cell);
            dc.DrawRectangle(new SolidColorBrush(colour), line, rect);
            if (Math.Abs(t) < 0.25) continue;   // near zero: no sign, the square is near-white
            var sign = new FormattedText(t > 0 ? "+" : "−", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face,
                cell * 0.7, ChartMath.TextOn(colour), dpi);
            dc.DrawText(sign, new Point(rect.X + (cell - sign.Width) / 2, rect.Y + (cell - sign.Height) / 2));
        }
    }

    private static DependencyProperty ColorProperty(string name, Color fallback) =>
        DependencyProperty.Register(name, typeof(Color), typeof(FilterSwatch), new FrameworkPropertyMetadata(fallback, Redraw));
}
