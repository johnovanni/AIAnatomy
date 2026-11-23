using System.Windows.Media;

namespace AIAnatomy.App.Controls;

/// <summary>Colour helpers shared by the charts (axis maths is in Presentation.Visuals.AxisTicks).</summary>
internal static class ChartMath
{
    public static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    /// <summary>Relative luminance (WCAG), 0 for black to 1 for white.</summary>
    public static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    /// <summary>Black or white, whichever has more contrast on <paramref name="background"/> (always at least 4.58:1).</summary>
    public static Brush TextOn(Color background) =>
        (Luminance(background) + 0.05) / 0.05 >= 1.05 / (Luminance(background) + 0.05) ? Brushes.Black : Brushes.White;
}
