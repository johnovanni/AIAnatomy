using System.Windows.Media;

namespace AIAnatomy.App.Controls;

/// <summary>Small helpers shared by the charts: axis ticks and colour mixing.</summary>
internal static class ChartMath
{
    /// <summary>"Nice" tick values (steps of 1, 2 or 5 times a power of ten) covering min..max, about <paramref name="count"/> of them.</summary>
    public static IReadOnlyList<double> Ticks(double min, double max, int count)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max)) return [];
        if (max <= min) return [min];
        double rough = (max - min) / Math.Max(1, count);
        double power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double step = new[] { 1.0, 2.0, 5.0, 10.0 }.Select(m => m * power).First(s => s >= rough);
        var ticks = new List<double>();
        for (double t = Math.Ceiling(min / step) * step; t <= max + step * 1e-9; t += step)
            ticks.Add(Math.Abs(t) < step * 1e-9 ? 0 : t);
        return ticks;
    }

    /// <summary>Widens a range so it has room and is never empty.</summary>
    public static (double Min, double Max) Pad(double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max)) return (0, 1);
        if (max - min < 1e-12) return (min - 1, max + 1);
        double pad = (max - min) * 0.05;
        return (min - pad, max + pad);
    }

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
