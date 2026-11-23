using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Visuals;

public class LabelPlacementTests
{
    private static readonly IReadOnlyList<LabelBox> NoBoxes = [];
    private static readonly IReadOnlyList<LabelLine> NoLines = [];

    [Fact]
    public void FirstFreeCandidate_IsChosen()
    {
        var spot = LabelPlacement.Choose([(10, 10), (50, 50)], 20, 10, 400, 400, [new LabelBox(0, 0, 25, 25)], NoLines);
        Assert.Equal((50.0, 50.0), spot);
    }

    [Fact]
    public void ACandidateOverAPointsOwnLine_IsRejected()
    {
        // R37 / re-review: K1 at (200, 200) with its line from the origin at (100, 200). "Right" is taken by another label,
        // "left" lies on K1's own line, so the label goes above.
        var line = new LabelLine(100, 200, 200, 200);
        var right = (214.0, 195.0);
        var left = (156.0, 195.0);
        var above = (185.0, 176.0);
        var takenRight = new LabelBox(210, 190, 40, 14);
        var spot = LabelPlacement.Choose([right, left, above], 30, 10, 400, 400, [takenRight], [line]);
        Assert.Equal(above, spot);
    }

    [Fact]
    public void AnyOtherPointsLine_IsAlsoAvoided()
    {
        var other = new LabelLine(0, 0, 100, 100);   // passes through (20, 20)
        var spot = LabelPlacement.Choose([(15, 15), (60, 10)], 10, 10, 400, 400, NoBoxes, [other]);
        Assert.Equal((60.0, 10.0), spot);
    }

    [Fact]
    public void WhenNothingIsFree_TheLeastCoveredCandidateIsUsed()
    {
        var big = new LabelBox(0, 0, 100, 100);
        // The first candidate lies wholly inside the box; the second only partly.
        var spot = LabelPlacement.Choose([(10, 10), (95, 10)], 20, 10, 400, 400, [big], NoLines);
        Assert.Equal((95.0, 10.0), spot);
    }

    [Fact]
    public void CandidatesAreKeptInsideTheArea()
    {
        var spot = LabelPlacement.Choose([(-30, 395)], 20, 10, 400, 400, NoBoxes, NoLines);
        Assert.Equal((0.0, 390.0), spot);
    }
}

public class ArrowLabelPlacementTests
{
    private static bool Overlaps((double X, double Y) at, double w, double h, LabelBox box) =>
        new LabelBox(at.X, at.Y, w, h).Overlap(box) > 0;

    [Fact]
    public void ClearMiddle_IsUsed()
    {
        var at = LabelPlacement.ForArrow((300, 100), (300, 300), 80, 20, 1000, 1000, []);
        Assert.Equal((260.0, 190.0), at);   // centred on the middle of the arrow
    }

    [Fact]
    public void VerticalArrowBesideABox_TakesAPlaceAlongTheArrow()
    {
        // The "after the last block" case at 32 pt: a vertical arrow whose middle has a box close on both sides, so
        // no sideways step at the middle is clear; a place higher or lower along the arrow is.
        var right = new LabelBox(310, 180, 300, 40);   // the Feed-forward box
        var left = new LabelBox(0, 180, 290, 40);
        var at = LabelPlacement.ForArrow((300, 100), (300, 300), 120, 20, 1000, 1000, [right, left]);
        Assert.False(Overlaps(at, 120, 20, right));
        Assert.False(Overlaps(at, 120, 20, left));
        Assert.True(Math.Abs(at.Y + 10 - 200) > 20, $"still at the middle: {at}");
        Assert.InRange(at.Y + 10, 100, 300);   // still beside its own arrow
        Assert.InRange(at.X + 60, 300 - 120 * 2.5, 300 + 120 * 2.5);
    }

    [Fact]
    public void NothingClear_FallsBackToTheLeastCovered_NotTheMiddle()
    {
        // A box around the whole arrow, reaching far to the right: every place touches it, the furthest step least.
        var box = new LabelBox(0, 150, 520, 100);
        var at = LabelPlacement.ForArrow((300, 160), (300, 240), 120, 20, 1000, 1000, [box]);
        double middleCover = new LabelBox(240, 190, 120, 20).Overlap(box);
        double cover = new LabelBox(at.X, at.Y, 120, 20).Overlap(box);
        Assert.True(cover < middleCover, $"{at} covers {cover}, the middle {middleCover}");
    }

    [Fact]
    public void NearerTheArrow_ComesBeforeFurtherSideways()
    {
        // The middle is blocked by a small box; a place along the arrow (no sideways step) is preferred to a far one.
        var blocker = new LabelBox(250, 180, 100, 40);
        var at = LabelPlacement.ForArrow((300, 0), (300, 400), 80, 20, 1000, 1000, [blocker]);
        Assert.Equal(260.0, at.X);   // on the arrow, at 0.35 or 0.65 of it
    }
}
