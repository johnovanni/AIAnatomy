using AIAnatomy.Models.ClassicMl;

namespace AIAnatomy.Models.Tests.ClassicMl;

public class StarterSetsTests
{
    [Fact]
    public void Five_named_sets()
    {
        Assert.Equal(
            ["Two groups", "Two groups with odd points", "Corners (XOR)", "Diagonal", "Three groups"],
            StarterSets.All.Select(s => s.Name).ToArray());
    }

    [Fact]
    public void Every_set_has_20_to_40_points_inside_the_plot_and_labels_from_0_with_none_skipped()
    {
        foreach (var (name, points) in StarterSets.All)
        {
            Assert.InRange(points.Count, 20, 40);
            Assert.All(points, p =>
            {
                Assert.InRange(p.X, 0, 10);
                Assert.InRange(p.Y, 0, 10);
            });
            var labels = points.Select(p => p.Label).Distinct().OrderBy(l => l).ToArray();
            Assert.Equal(Enumerable.Range(0, labels.Length), labels);
            Assert.Equal(name == "Three groups" ? 3 : 2, labels.Length);
        }
    }

    [Fact]
    public void No_two_points_share_a_place()
    {
        foreach (var (_, points) in StarterSets.All)
            Assert.Equal(points.Count, points.Select(p => (p.X, p.Y)).Distinct().Count());
    }

    [Fact]
    public void Odd_points_set_is_the_two_groups_plus_three()
    {
        var two = StarterSets.All.Single(s => s.Name == "Two groups").Points;
        var odd = StarterSets.All.Single(s => s.Name == "Two groups with odd points").Points;
        Assert.Equal(two.Count + 3, odd.Count);
        Assert.True(two.All(odd.Contains));
    }
}
