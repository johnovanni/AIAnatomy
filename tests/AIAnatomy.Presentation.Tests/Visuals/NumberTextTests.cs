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
    [InlineData(0, "0.000")]
    [InlineData(12345.6, "12,346")]
    [InlineData(0.0000123, "1.23e\u22125")]
    [InlineData(21, "21")]
    [InlineData(1, "1")]
    [InlineData(0.5, "0.500")]
    public void Short_IsCompact(double value, string text) => Assert.Equal(text, NumberText.Short(value));

    [Theory]
    [InlineData(2, "2")]
    [InlineData(-0.5, "\u22120.5")]
    [InlineData(3000, "3,000")]
    [InlineData(0, "0")]
    public void Tick_HasNoNeedlessDecimals(double value, string text) => Assert.Equal(text, NumberText.Tick(value));

    [Theory]
    [InlineData(0.41273849201, "41.27%")]
    [InlineData(1, "100.00%")]
    [InlineData(0.00001, "0.00%")]
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
