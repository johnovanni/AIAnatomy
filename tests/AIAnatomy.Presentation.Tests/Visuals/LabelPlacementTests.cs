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
