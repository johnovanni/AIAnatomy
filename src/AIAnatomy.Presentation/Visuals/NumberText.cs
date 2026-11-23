using System.Globalization;

namespace AIAnatomy.Presentation.Visuals;

/// <summary>
/// How numbers are written on screen, the same in every culture. <see cref="Exact"/> is the shortest text that reads
/// back as the very same <c>double</c> (used in readouts and tooltips); <see cref="Short"/> is for labels inside
/// charts. Negative numbers use the true minus sign (U+2212), which is easier to see than a hyphen.
/// </summary>
public static class NumberText
{
    private const char Minus = '−';
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Exact(double value)
    {
        if (double.IsNaN(value)) return "not a number";
        if (double.IsPositiveInfinity(value)) return "∞";
        if (double.IsNegativeInfinity(value)) return Minus + "∞";
        return WithMinus(value.ToString("R", Invariant));
    }

    /// <summary>
    /// Three decimals; whole numbers from 1 up, and anything from 1,000 up, without decimals; very small numbers as
    /// 1.23e−5. (One parameter only, so <c>values.Select(NumberText.Short)</c> can never pick up the index.)
    /// </summary>
    public static string Short(double value) => ShortWithDecimals(value, 3);

    /// <summary><see cref="Short"/> with a chosen number of decimals (for squares too small for three).</summary>
    public static string ShortWithDecimals(double value, int decimals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        if (!double.IsFinite(value)) return Exact(value);
        double size = Math.Abs(value);
        string text = size >= 1000 || (size >= 1 && value == Math.Round(value)) ? value.ToString("#,0", Invariant)
            : size != 0 && size < 0.5 * Math.Pow(10, -decimals) ? value.ToString("0.##e0", Invariant)
            : value.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), Invariant);
        return WithMinus(text);
    }

    /// <summary>An axis tick: no more decimals than it needs (2, 0.5, 1,000).</summary>
    public static string Tick(double value) =>
        double.IsFinite(value) ? WithMinus(value.ToString(Math.Abs(value) >= 1000 ? "#,0.###" : "0.######", Invariant)) : Exact(value);

    /// <summary>A probability (0 to 1) as a percentage with two decimals, such as 41.27%.</summary>
    public static string Percent(double probability) =>
        double.IsFinite(probability) ? WithMinus((probability * 100).ToString("0.00", Invariant)) + "%" : Exact(probability);

    private static string WithMinus(string text) => text.Replace('-', Minus);
}
