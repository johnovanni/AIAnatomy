namespace AIAnatomy.Presentation.Visuals;

/// <summary>Axis ranges and tick values for the charts. Always finishes, whatever the numbers.</summary>
public static class AxisTicks
{
    /// <summary>
    /// "Nice" tick values (steps of 1, 2 or 5 times a power of ten) inside min..max, about <paramref name="count"/>
    /// of them and never more than count·3 + 2. Each tick is computed from its index, so a step too small to change
    /// a large number cannot make the list run on.
    /// </summary>
    public static IReadOnlyList<double> Ticks(double min, double max, int count)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max)) return [];
        if (max <= min) return [min];
        count = Math.Max(1, count);
        double rough = (max - min) / count;
        double power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double step = new[] { 1.0, 2.0, 5.0, 10.0 }.Select(m => m * power).First(s => s >= rough);
        double first = Math.Ceiling(min / step) * step;
        int limit = count * 3 + 2;
        var ticks = new List<double>();
        for (int i = 0; i < limit; i++)
        {
            double t = first + i * step;
            if (t > max + step * 1e-9) break;
            if (ticks.Count > 0 && t <= ticks[^1]) break;   // the step no longer moves the value
            ticks.Add(Math.Abs(t) < step * 1e-9 ? 0 : t);
        }
        return ticks.Count > 0 ? ticks : [min];
    }

    /// <summary>
    /// Leaves a range alone unless its ends are (nearly) equal, measured relative to their size; then widens it by 1
    /// on each side (or by a millionth of the value when that is larger). Not-finite ends give 0..1.
    /// </summary>
    public static (double Min, double Max) Widen(double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max)) return (0, 1);
        double size = Math.Max(1, Math.Max(Math.Abs(min), Math.Abs(max)));
        if (max - min > 1e-9 * size) return (min, max);
        double by = Math.Max(1, size * 1e-6);
        return (min - by, max + by);
    }

    /// <summary><see cref="Widen"/>, then 5% more room at each end.</summary>
    public static (double Min, double Max) Pad(double min, double max)
    {
        (min, max) = Widen(min, max);
        double pad = (max - min) * 0.05;
        return (min - pad, max + pad);
    }

    /// <summary>The smallest "nice" number (1, 2 or 5 times a power of ten) that is at least <paramref name="value"/>; 1 for a value that is not above 0 or not finite.</summary>
    public static double NiceCeiling(double value)
    {
        if (!double.IsFinite(value) || value <= 0) return 1;
        double power = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (double m in new[] { 1.0, 2.0, 5.0, 10.0 })
            if (m * power >= value * (1 - 1e-12)) return m * power;
        return 10 * power;
    }
}
