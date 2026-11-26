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
    /// Three decimals; zero, whole numbers from 1 up, and anything from 1,000 up, without decimals; very small numbers as
    /// 1.23e−5. (One parameter only, so <c>values.Select(NumberText.Short)</c> can never pick up the index.)
    /// </summary>
    public static string Short(double value) => ShortWithDecimals(value, 3);

    /// <summary><see cref="Short"/> with a chosen number of decimals (for squares too small for three).</summary>
    public static string ShortWithDecimals(double value, int decimals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        if (!double.IsFinite(value)) return Exact(value);
        if (value == 0) return "0";   // exactly zero (also −0): "0", not "0.000" (final review M12)
        double size = Math.Abs(value);
        string text = size >= 1000 || (size >= 1 && value == Math.Round(value)) ? value.ToString("#,0", Invariant)
            : size != 0 && size < 0.5 * Math.Pow(10, -decimals) ? value.ToString("0.##e0", Invariant)
            : value.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), Invariant);
        return WithMinus(text);
    }

    /// <summary>
    /// A number written inside a heat-map square: like <see cref="ShortWithDecimals"/>, but always fixed-point (a tiny
    /// value is "0.000", never 1.2e−4, which is too wide for a square), and a value that rounds to zero has no minus sign.
    /// </summary>
    public static string Cell(double value, int decimals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        if (!double.IsFinite(value)) return Exact(value);
        if (value == 0) return "0";
        double size = Math.Abs(value);
        if (size >= 1000 || (size >= 1 && value == Math.Round(value))) return WithMinus(value.ToString("#,0", Invariant));
        string text = value.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), Invariant);
        return WithMinus(text.TrimStart('-').Trim('0', '.').Length == 0 ? text.TrimStart('-') : text);
    }

    /// <summary>A share from 0 to 1 as a whole percentage without the % sign ("35"), rounding halves up.</summary>
    public static string WholePercent(double share)
    {
        if (!double.IsFinite(share)) return Exact(share);
        double percent = Math.Round(share * 100, MidpointRounding.AwayFromZero);
        return WithMinus((percent == 0 ? 0 : percent).ToString("0", Invariant));
    }

    /// <summary>An axis tick: no more decimals than it needs (2, 0.5, 1,000).</summary>
    public static string Tick(double value) =>
        double.IsFinite(value) ? WithMinus(value.ToString(Math.Abs(value) >= 1000 ? "#,0.###" : "0.######", Invariant)) : Exact(value);

    /// <summary>A probability (0 to 1) as a percentage with two decimals, such as 41.27%. A chance under 1 is never
    /// written 100.00% (it is "&gt;99.99%") and one over 0 never 0.00% ("&lt;0.01%"), so it never seems certain or
    /// impossible when it is not (final review M12).</summary>
    public static string Percent(double probability)
    {
        if (!double.IsFinite(probability)) return Exact(probability);
        string text = (probability * 100).ToString("0.00", Invariant);
        if (text == "100.00" && probability < 1) return ">99.99%";
        if (text == "0.00" && probability > 0) return "<0.01%";
        return WithMinus(text) + "%";
    }

    private static string WithMinus(string text) => text.Replace('-', Minus);
}
