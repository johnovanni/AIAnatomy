using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Visuals;

public class LineSeriesTests
{
    // AA-001 (owner's choice, 2026-11-24): the legend rounds the latest value to 3 decimals; the tooltip stays exact.
    [Fact]
    public void LatestText_RoundsToThreeDecimals() =>
        Assert.Equal("4.234", new LineSeries("loss", [(0, 5.0), (10, 4.234106504597519)]).LatestText);

    [Fact]
    public void LatestText_UsesTheLastPoint() =>
        Assert.Equal("−0.873", new LineSeries("loss", [(0, 1.0), (1, -0.87312)]).LatestText);

    [Fact]
    public void LatestText_WholeNumberHasNoDecimals() =>
        Assert.Equal("3", new LineSeries("loss", [(0, 3.0)]).LatestText);

    [Fact]
    public void LatestText_IsNullWithNoPoints() =>
        Assert.Null(new LineSeries("loss", []).LatestText);
}
