using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Visuals;

public class SegmentClipTests
{
    [Fact]
    public void InsideSegment_IsUnchanged()
    {
        var cut = SegmentClip.Clip(-1, -1, 1, 1, -5, 5, -2, 2)!.Value;
        Assert.Equal((-1.0, -1.0), cut.From);
        Assert.Equal((1.0, 1.0), cut.To);
    }

    [Fact]
    public void HugeSegment_IsCutToTheRectangle()
    {
        // From (0, 1) to (0, 1.6e9): only the part up to y = 2 is kept.
        var cut = SegmentClip.Clip(0, 1, 0, 1.6e9, -5, 5, -2, 2)!.Value;
        Assert.Equal((0.0, 1.0), cut.From);
        Assert.Equal(0.0, cut.To.X, 12);
        Assert.Equal(2.0, cut.To.Y, 9);
        // A zig-zag step from (4, 1.5) down to (-4, -1.6e9) leaves through the bottom edge.
        var diagonal = SegmentClip.Clip(4, 1.5, -4, -1.6e9, -5, 5, -2, 2)!.Value;
        Assert.All(new[] { diagonal.From, diagonal.To }, p => Assert.True(SegmentClip.Inside(p.X, p.Y, -5 - 1e-9, 5 + 1e-9, -2 - 1e-9, 2 + 1e-9)));
        Assert.Equal(-2.0, diagonal.To.Y, 9);
    }

    [Fact]
    public void OutsideSegments_AreDropped()
    {
        Assert.Null(SegmentClip.Clip(6, 0, 9, 1, -5, 5, -2, 2));
        Assert.Null(SegmentClip.Clip(0, 3, 1, 1e9, -5, 5, -2, 2));
        Assert.Null(SegmentClip.Clip(double.NaN, 0, 1, 1, -5, 5, -2, 2));
        Assert.Null(SegmentClip.Clip(0, 0, double.PositiveInfinity, 1, -5, 5, -2, 2));
    }

    [Fact]
    public void SegmentCrossingTheRectangle_KeepsTheMiddle()
    {
        var cut = SegmentClip.Clip(-10, 0, 10, 0, -5, 5, -2, 2)!.Value;
        Assert.Equal((-5.0, 0.0), cut.From);
        Assert.Equal((5.0, 0.0), cut.To);
    }

    [Fact]
    public void NiceCeiling_RoundsUpToOneTwoOrFive()
    {
        Assert.Equal(0.1, AxisTicks.NiceCeiling(0.08), 12);
        Assert.Equal(0.2, AxisTicks.NiceCeiling(0.11), 12);
        Assert.Equal(0.5, AxisTicks.NiceCeiling(0.5), 12);
        Assert.Equal(1.0, AxisTicks.NiceCeiling(0.7), 12);
        Assert.Equal(1.0, AxisTicks.NiceCeiling(0), 12);
        Assert.Equal(1.0, AxisTicks.NiceCeiling(double.NaN), 12);
        Assert.Equal(0.01, AxisTicks.NiceCeiling(0.0071), 12);
    }
}
