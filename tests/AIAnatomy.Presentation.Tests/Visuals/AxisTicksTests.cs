using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Visuals;

public class AxisTicksTests
{
    [Fact]
    public void Ticks_AreNiceSteps()
    {
        Assert.Equal(new[] { 0.0, 1000, 2000, 3000 }, AxisTicks.Ticks(0, 3000, 4));
        Assert.Equal(new[] { -2.0, -1, 0, 1, 2 }, AxisTicks.Ticks(-2.2, 2.2, 5));
    }

    [Fact]
    public void Ticks_NearlyEqualLargeValues_Finish()
    {
        double a = 10000, b = Math.BitIncrement(10000);   // one ulp apart: a step below half an ulp would never move
        var ticks = AxisTicks.Ticks(a, b, 8);
        Assert.InRange(ticks.Count, 1, 8 * 3 + 2);
    }

    [Fact]
    public void Ticks_StopEvenWhenTheStepCannotMove()
    {
        var ticks = AxisTicks.Ticks(1e300, 1e300 * (1 + 1e-15), 8);
        Assert.InRange(ticks.Count, 1, 8 * 3 + 2);
    }

    [Fact]
    public void Ticks_HandleBadInput()
    {
        Assert.Empty(AxisTicks.Ticks(double.NaN, 1, 4));
        Assert.Equal(new[] { 5.0 }, AxisTicks.Ticks(5, 5, 4));
        Assert.Equal(new[] { 5.0 }, AxisTicks.Ticks(5, 4, 4));
    }

    [Fact]
    public void Widen_OnlyNearlyEqualRanges_UsingARelativeThreshold()
    {
        Assert.Equal((0.0, 10.0), AxisTicks.Widen(0, 10));
        var (min, max) = AxisTicks.Widen(10000, Math.BitIncrement(10000));
        Assert.True(max - min >= 1, $"{min}..{max}");
        Assert.Equal((-1.0, 1.0), AxisTicks.Widen(0, 0));
        Assert.Equal((0.0, 1.0), AxisTicks.Widen(double.NaN, 1));
    }

    [Fact]
    public void WidenTo_GivesAtLeastTheSpanFromTheFirstValue()
    {
        // Final review M12: one loss point at step 0 gives a 0 to 20 axis (one chunk), not -1 to 1.
        Assert.Equal((0.0, 20.0), AxisTicks.WidenTo(0, 0, 20));
        Assert.Equal((0.0, 20.0), AxisTicks.WidenTo(0, 10, 20));
        Assert.Equal((0.0, 400.0), AxisTicks.WidenTo(0, 400, 20));   // wider data keeps its own range
        Assert.Equal((-1.0, 1.0), AxisTicks.WidenTo(0, 0, 0));         // no span asked: as Widen
        Assert.Equal((0.0, 1.0), AxisTicks.WidenTo(double.NaN, 1, 20));
    }

    [Fact]
    public void Pad_AddsFivePercent()
    {
        Assert.Equal((-0.5, 10.5), AxisTicks.Pad(0, 10));
        var (min, max) = AxisTicks.Pad(10000, Math.BitIncrement(10000));
        Assert.True(max - min >= 1);
    }
}
