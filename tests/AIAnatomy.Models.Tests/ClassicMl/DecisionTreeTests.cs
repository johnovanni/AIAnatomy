using AIAnatomy.Models.ClassicMl;

namespace AIAnatomy.Models.Tests.ClassicMl;

public class DecisionTreeTests
{
    private static LabeledPoint P(double x, double y, int l) => new(x, y, l);

    private static double Accuracy(TreeNode tree, IReadOnlyList<LabeledPoint> pts) =>
        pts.Count(p => DecisionTree.Predict(tree, p.X, p.Y) == p.Label) / (double)pts.Count;

    [Fact]
    public void Gini_values()
    {
        Assert.Equal(0.5, DecisionTree.Gini([0, 0, 1, 1]), 12);
        Assert.Equal(0.0, DecisionTree.Gini([0, 0, 0]), 12);
        Assert.Equal(2.0 / 3.0, DecisionTree.Gini([0, 1, 2]), 12);
        Assert.Equal(0.0, DecisionTree.Gini([]), 12);
    }

    [Fact]
    public void Four_points_split_once_on_x_at_3()
    {
        var tree = DecisionTree.Grow([P(1, 1, 0), P(2, 1, 0), P(4, 1, 1), P(5, 1, 1)], 5);
        var split = Assert.IsType<SplitNode>(tree);
        Assert.Equal(Axis.X, split.Axis);
        Assert.Equal(3.0, split.Threshold);
        Assert.Equal(0.5, split.Gini, 12);
        Assert.Equal(4, split.Count);
        var below = Assert.IsType<Leaf>(split.Below);
        var above = Assert.IsType<Leaf>(split.AtOrAbove);
        Assert.Equal(0, below.Label);
        Assert.Equal(1, above.Label);
        Assert.Equal(2, below.Count);
        Assert.Equal(0.0, below.Gini);
        Assert.Equal(1, DecisionTree.Depth(tree));
    }

    [Fact]
    public void Threshold_value_goes_above()
    {
        var tree = DecisionTree.Grow([P(1, 1, 0), P(2, 1, 0), P(4, 1, 1), P(5, 1, 1)], 5);
        Assert.Equal(0, DecisionTree.Predict(tree, 2.999, 0));
        Assert.Equal(1, DecisionTree.Predict(tree, 3.0, 0));
    }

    private static readonly LabeledPoint[] Corners = [P(1, 1, 0), P(1, 9, 1), P(9, 1, 1), P(9, 9, 0)];

    [Fact]
    public void Corners_depth_1_gets_two_of_four()
    {
        var tree = DecisionTree.Grow(Corners, 1);
        var split = Assert.IsType<SplitNode>(tree);
        Assert.Equal(Axis.X, split.Axis);   // all splits tie; X, then the lower threshold, wins
        Assert.Equal(5.0, split.Threshold);
        Assert.Equal(0.5, Accuracy(tree, Corners));
    }

    [Fact]
    public void Corners_depth_2_splits_x_then_y_and_gets_all_four()
    {
        var tree = DecisionTree.Grow(Corners, 2);
        var split = Assert.IsType<SplitNode>(tree);
        Assert.Equal(Axis.X, split.Axis);
        Assert.Equal(5.0, split.Threshold);
        foreach (var child in new[] { split.Below, split.AtOrAbove })
        {
            var inner = Assert.IsType<SplitNode>(child);
            Assert.Equal(Axis.Y, inner.Axis);
            Assert.Equal(5.0, inner.Threshold);
        }
        Assert.Equal(1.0, Accuracy(tree, Corners));
        Assert.Equal(2, DecisionTree.Depth(tree));
    }

    [Fact]
    public void Depth_0_is_one_majority_leaf_ties_to_lower_label()
    {
        var tree = DecisionTree.Grow([P(1, 1, 1), P(2, 2, 1), P(3, 3, 0)], 0);
        var leaf = Assert.IsType<Leaf>(tree);
        Assert.Equal(1, leaf.Label);
        Assert.Equal(0, DecisionTree.Depth(tree));
        var tie = Assert.IsType<Leaf>(DecisionTree.Grow([P(1, 1, 1), P(2, 2, 0)], 0));
        Assert.Equal(0, tie.Label);
    }

    [Fact]
    public void Invalid_arguments()
    {
        Assert.Throws<ArgumentException>(() => DecisionTree.Grow([], 3));
        Assert.Throws<ArgumentException>(() => DecisionTree.Grow([P(1, 1, 0)], -1));
    }

    [Fact]
    public void One_point_is_a_leaf()
    {
        var leaf = Assert.IsType<Leaf>(DecisionTree.Grow([P(4, 4, 2)], 5));
        Assert.Equal(2, leaf.Label);
        Assert.Equal(1, leaf.Count);
    }

    [Fact]
    public void All_one_label_is_a_leaf()
    {
        var leaf = Assert.IsType<Leaf>(DecisionTree.Grow([P(1, 1, 1), P(2, 5, 1), P(7, 3, 1)], 5));
        Assert.Equal(1, leaf.Label);
    }

    [Fact]
    public void Two_labels_at_the_same_place_cannot_split()
    {
        var leaf = Assert.IsType<Leaf>(DecisionTree.Grow([P(3, 3, 1), P(3, 3, 0)], 5));
        Assert.Equal(0, leaf.Label);
        Assert.Equal(0.5, leaf.Gini, 12);
    }

    [Fact]
    public void Splits_on_y_when_x_is_all_the_same()
    {
        var tree = DecisionTree.Grow([P(3, 1, 0), P(3, 2, 0), P(3, 8, 1), P(3, 9, 1)], 3);
        var split = Assert.IsType<SplitNode>(tree);
        Assert.Equal(Axis.Y, split.Axis);
        Assert.Equal(5.0, split.Threshold);
    }

    [Fact]
    public void Ties_between_axes_go_to_x()
    {
        // Either axis separates the labels perfectly.
        var tree = DecisionTree.Grow([P(1, 1, 0), P(2, 2, 0), P(8, 8, 1), P(9, 9, 1)], 3);
        var split = Assert.IsType<SplitNode>(tree);
        Assert.Equal(Axis.X, split.Axis);
    }

    [Fact]
    public void Chooses_the_lowest_weighted_gini_not_the_first_threshold()
    {
        // Labels along x: 0 0 1 1 1 0 0 0. The best single cut is not the first one.
        var pts = new[] { P(1, 0, 0), P(2, 0, 0), P(3, 0, 1), P(4, 0, 1), P(5, 0, 1), P(6, 0, 0), P(7, 0, 0), P(8, 0, 0) };
        var split = Assert.IsType<SplitNode>(DecisionTree.Grow(pts, 1));
        Assert.Equal(Axis.X, split.Axis);
        Assert.Equal(5.5, split.Threshold);   // right = {6,7,8} pure 0; left = {0,0,1,1,1} gini .48
    }

    [Fact]
    public void Two_hundred_points_depth_10_is_correct()
    {
        var pts = Enumerable.Range(0, 200)
            .Select(i => P((i * 37 % 200) / 20.0, (i * 91 % 200) / 20.0 + 0.001 * i, (i * 7 + i / 3) % 3 == 0 ? 1 : 0))
            .ToList();
        var tree = DecisionTree.Grow(pts, 10);
        Assert.True(DecisionTree.Depth(tree) <= 10);
        Assert.Equal(200, tree.Count);
        // More depth never fits worse.
        Assert.True(Accuracy(tree, pts) >= Accuracy(DecisionTree.Grow(pts, 3), pts));
        var regions = DecisionTree.Regions(tree, 0, 11, 0, 11);
        Assert.Equal(121.0, regions.Sum(r => (r.XMax - r.XMin) * (r.YMax - r.YMin)), 6);
    }

    [Fact]
    public void Regions_tile_the_box_and_labels_match_predict_at_centres()
    {
        foreach (var set in StarterSets.All)
        foreach (var depth in new[] { 0, 1, 2, 4, 8 })
        {
            var tree = DecisionTree.Grow(set.Points, depth);
            var regions = DecisionTree.Regions(tree, 0, 10, 0, 10);
            Assert.Equal(100.0, regions.Sum(r => (r.XMax - r.XMin) * (r.YMax - r.YMin)), 9);
            foreach (var r in regions)
            {
                Assert.True(r.XMax > r.XMin && r.YMax > r.YMin);
                Assert.Equal(r.Label, DecisionTree.Predict(tree, (r.XMin + r.XMax) / 2, (r.YMin + r.YMax) / 2));
            }
            for (int i = 0; i < regions.Count; i++)
            for (int j = i + 1; j < regions.Count; j++)
            {
                var a = regions[i]; var b = regions[j];
                bool overlap = a.XMin < b.XMax && b.XMin < a.XMax && a.YMin < b.YMax && b.YMin < a.YMax;
                Assert.False(overlap);
            }
        }
    }

    [Fact]
    public void Regions_clip_a_threshold_outside_the_box()
    {
        var tree = DecisionTree.Grow([P(1, 1, 0), P(2, 1, 0), P(4, 1, 1), P(5, 1, 1)], 5);   // X at 3
        var regions = DecisionTree.Regions(tree, 3.5, 10, 0, 10);
        var only = Assert.Single(regions);
        Assert.Equal(1, only.Label);
        Assert.Equal(3.5, only.XMin);
    }

    [Fact]
    public void Odd_points_set_a_deep_tree_fits_every_point_a_shallow_one_does_not()
    {
        var odd = StarterSets.All.Single(s => s.Name == "Two groups with odd points").Points;
        Assert.Equal(1.0, Accuracy(DecisionTree.Grow(odd, 20), odd));
        Assert.True(Accuracy(DecisionTree.Grow(odd, 1), odd) < 1.0);
    }
}
