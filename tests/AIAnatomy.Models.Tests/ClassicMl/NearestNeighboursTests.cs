using AIAnatomy.Models.ClassicMl;

namespace AIAnatomy.Models.Tests.ClassicMl;

public class NearestNeighboursTests
{
    private static LabeledPoint P(double x, double y, int l) => new(x, y, l);

    [Fact]
    public void Vote_of_the_k_nearest()
    {
        LabeledPoint[] pts = [P(0, 0, 0), P(1, 0, 0), P(5, 0, 1)];
        Assert.Equal(0, NearestNeighbours.Classify(pts, 2, 0, 1));
        Assert.Equal(0, NearestNeighbours.Classify(pts, 2, 0, 3));
        Assert.Equal(1, NearestNeighbours.Classify(pts, 4.9, 0, 1));
    }

    [Fact]
    public void Tied_vote_goes_to_the_nearest_tied_label()
    {
        LabeledPoint[] pts = [P(0, 0, 0), P(4, 0, 1)];
        Assert.Equal(0, NearestNeighbours.Classify(pts, 1, 0, 2));
        Assert.Equal(1, NearestNeighbours.Classify(pts, 3, 0, 2));
    }

    [Fact]
    public void Equal_distances_count_in_point_order()
    {
        // The two points are the same distance from the origin; with k = 1 the first listed wins.
        Assert.Equal(1, NearestNeighbours.Classify([P(-1, 0, 1), P(1, 0, 0)], 0, 0, 1));
        Assert.Equal(0, NearestNeighbours.Classify([P(1, 0, 0), P(-1, 0, 1)], 0, 0, 1));
    }

    [Fact]
    public void K_is_clamped()
    {
        LabeledPoint[] pts = [P(0, 0, 0), P(1, 0, 1), P(2, 0, 1)];
        Assert.Equal(NearestNeighbours.Classify(pts, 0, 0, 3), NearestNeighbours.Classify(pts, 0, 0, 50));
        Assert.Equal(1, NearestNeighbours.Classify(pts, 0, 0, 50));
        Assert.Equal(NearestNeighbours.Classify(pts, 0, 0, 1), NearestNeighbours.Classify(pts, 0, 0, 0));
        Assert.Equal(0, NearestNeighbours.Classify(pts, 0, 0, -4));
    }

    [Fact]
    public void No_points_throws()
    {
        Assert.Throws<ArgumentException>(() => NearestNeighbours.Classify([], 0, 0, 1));
    }

    [Fact]
    public void Odd_points_set_k1_fits_every_point_a_larger_k_does_not()
    {
        var odd = StarterSets.All.Single(s => s.Name == "Two groups with odd points").Points;
        Assert.All(odd, p => Assert.Equal(p.Label, NearestNeighbours.Classify(odd, p.X, p.Y, 1)));
        Assert.Contains(odd, p => NearestNeighbours.Classify(odd, p.X, p.Y, 9) != p.Label);
    }

    [Fact]
    public void Two_labels_at_the_same_place_k2_the_first_listed_wins()
    {
        // Both points are at the same distance (and the vote is tied 1 to 1), so the stable order decides: the first listed.
        Assert.Equal(1, NearestNeighbours.Classify([P(2, 2, 1), P(2, 2, 0)], 2, 2, 2));
        Assert.Equal(0, NearestNeighbours.Classify([P(2, 2, 0), P(2, 2, 1)], 2, 2, 2));
        Assert.Equal(1, NearestNeighbours.Classify([P(2, 2, 1), P(2, 2, 0)], 5, 5, 2));
    }
}
