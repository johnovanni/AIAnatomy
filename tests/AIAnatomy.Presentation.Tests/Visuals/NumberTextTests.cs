using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Visuals;

public class NumberTextTests
{
    [Theory]
    [InlineData(0.1, "0.1")]
    [InlineData(-0.41273849201, "\u22120.41273849201")]
    [InlineData(1.0 / 3.0, "0.3333333333333333")]
    [InlineData(double.NegativeInfinity, "\u2212\u221e")]
    [InlineData(double.PositiveInfinity, "\u221e")]
    [InlineData(double.NaN, "not a number")]
    [InlineData(1e-20, "1E−20")]
    public void Exact_RoundTrips(double value, string text) => Assert.Equal(text, NumberText.Exact(value));

    [Fact]
    public void Exact_ParsesBackToTheSameDouble()
    {
        double v = 0.1 + 0.2;
        Assert.Equal(v, double.Parse(NumberText.Exact(v).Replace('\u2212', '-'), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0.41273849201, "0.413")]
    [InlineData(-1.2, "\u22121.200")]
    [InlineData(0, "0")]          // final review M12: zero is written "0", not "0.000"
    [InlineData(-0.0, "0")]       // never "−0"
    [InlineData(12345.6, "12,346")]
    [InlineData(0.0000123, "1.23e\u22125")]
    [InlineData(21, "21")]
    [InlineData(1, "1")]
    [InlineData(0.5, "0.500")]
    public void Short_IsCompact(double value, string text) => Assert.Equal(text, NumberText.Short(value));

    [Theory]
    [InlineData(0.41273849201, 2, "0.41")]
    [InlineData(-0.455, 2, "−0.46")]
    [InlineData(21, 2, "21")]
    public void ShortWithDecimals_CanUseFewerDecimals(double value, int decimals, string text) =>
        Assert.Equal(text, NumberText.ShortWithDecimals(value, decimals));

    // Final review I3: the numbers in heat-map squares are fixed-point (never 1.2e−4), and a value that rounds to zero
    // carries no minus sign (never "−0.000").
    [Theory]
    [InlineData(0.00012, 3, "0.000")]
    [InlineData(-0.00012, 3, "0.000")]
    [InlineData(0.00012, 1, "0.0")]
    [InlineData(-25.625, 3, "−25.625")]
    [InlineData(-25.625, 2, "−25.63")]
    [InlineData(90, 3, "90")]
    [InlineData(0, 2, "0")]
    [InlineData(0.41273849201, 2, "0.41")]
    [InlineData(12345.6, 3, "12,346")]
    public void Cell_IsFixedPoint(double value, int decimals, string text) => Assert.Equal(text, NumberText.Cell(value, decimals));

    // Final review I3 / ruling 2: attention weights as whole percentages, without the % sign (the caption says so).
    [Theory]
    [InlineData(0.35, "35")]
    [InlineData(1.0, "100")]
    [InlineData(0.004, "0")]
    [InlineData(0.005, "1")]
    [InlineData(0.125, "13")]
    [InlineData(0.0, "0")]
    public void WholePercent_RoundsToAWholeNumber(double probability, string text) =>
        Assert.Equal(text, NumberText.WholePercent(probability));

    [Fact]
    public void Short_AsAMethodGroup_IgnoresTheIndex()
    {
        // Select(NumberText.Short) once bound to Select((value, index) => ...) and used the index as the decimals.
        Assert.Equal(new[] { "0.412", "−0.873", "0.051" }, new[] { 0.412, -0.873, 0.051 }.Select(NumberText.Short));
    }

    [Fact]
    public void Short_RoundsHalfAwayFromZero() => Assert.Equal("0.063", NumberText.Short(0.0625));

    [Theory]
    [InlineData(2, "2")]
    [InlineData(-0.5, "\u22120.5")]
    [InlineData(3000, "3,000")]
    [InlineData(0, "0")]
    public void Tick_HasNoNeedlessDecimals(double value, string text) => Assert.Equal(text, NumberText.Tick(value));

    [Theory]
    [InlineData(0.41273849201, "41.27%")]
    [InlineData(1, "100.00%")]
    [InlineData(0, "0.00%")]
    // Final review M12: never "100.00%" for a chance under 1 nor "0.00%" for one over 0, the rule lesson 4.7's prediction
    // line follows too ("over 99%"), so the bars and the line never seem to disagree.
    [InlineData(0.99996, ">99.99%")]
    [InlineData(0.00001, "<0.01%")]
    [InlineData(0.99994, "99.99%")]
    public void Percent_HasTwoDecimals(double value, string text) => Assert.Equal(text, NumberText.Percent(value));

    [Fact]
    public void IgnoresTheUsersCulture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("0.413", NumberText.Short(0.41273849201));
            Assert.Equal("0.5", NumberText.Exact(0.5));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }
}
