namespace AIAnatomy.Models.ClassicMl;

public enum Axis { X, Y }

/// <summary>A node of the tree: how many training points reached it and their Gini impurity.</summary>
public abstract record TreeNode(int Count, double Gini);

public sealed record Leaf(int Label, int Count, double Gini) : TreeNode(Count, Gini);

/// <summary>"Is the value on <see cref="Axis"/> below <see cref="Threshold"/>?" Yes goes to Below.</summary>
public sealed record SplitNode(Axis Axis, double Threshold, TreeNode Below, TreeNode AtOrAbove, int Count, double Gini)
    : TreeNode(Count, Gini);

/// <summary>A rectangle of the plot that the tree gives one label.</summary>
public sealed record Region(double XMin, double XMax, double YMin, double YMax, int Label);

/// <summary>
/// A CART decision tree grown by Gini impurity. A node splits when its depth is below the maximum, it is impure,
/// and some axis has two distinct values. Candidates are the midpoints between consecutive distinct values;
/// the one with the lowest weighted child Gini wins, even when no better than the parent (as scikit-learn does,
/// which lets corner data split). Ties go to axis X, then the lower threshold.
/// </summary>
public static class DecisionTree
{
    public static double Gini(IEnumerable<int> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var counts = new Dictionary<int, int>();
        int n = 0;
        foreach (var l in labels) { counts[l] = counts.GetValueOrDefault(l) + 1; n++; }
        return GiniOf(counts.Values, n);
    }

    private static double GiniOf(IEnumerable<int> counts, int n)
    {
        if (n == 0) return 0;
        double sumSquares = 0;
        foreach (var c in counts) sumSquares += (double)c * c;
        return 1 - sumSquares / ((double)n * n);
    }

    public static TreeNode Grow(IReadOnlyList<LabeledPoint> points, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0) throw new ArgumentException("There are no points to grow a tree from.", nameof(points));
        if (maxDepth < 0) throw new ArgumentException("The maximum depth cannot be negative.", nameof(maxDepth));
        if (points.Any(p => p.Label < 0)) throw new ArgumentException("Labels cannot be negative.", nameof(points));
        int labelCount = points.Max(p => p.Label) + 1;
        return Build(points.ToArray(), 0, maxDepth, labelCount);
    }

    private static TreeNode Build(LabeledPoint[] pts, int depth, int maxDepth, int labelCount)
    {
        int n = pts.Length;
        var counts = new int[labelCount];
        foreach (var p in pts) counts[p.Label]++;
        double gini = GiniOf(counts, n);
        int majority = 0;
        for (int l = 1; l < labelCount; l++) if (counts[l] > counts[majority]) majority = l;

        bool pure = counts[majority] == n;
        if (depth >= maxDepth || pure || BestSplit(pts, counts, labelCount) is not { } best)
            return new Leaf(majority, n, gini);

        var below = pts.Where(p => Value(p, best.Axis) < best.Threshold).ToArray();
        var above = pts.Where(p => Value(p, best.Axis) >= best.Threshold).ToArray();
        return new SplitNode(best.Axis, best.Threshold,
            Build(below, depth + 1, maxDepth, labelCount),
            Build(above, depth + 1, maxDepth, labelCount), n, gini);
    }

    private static double Value(LabeledPoint p, Axis axis) => axis == Axis.X ? p.X : p.Y;

    private readonly record struct Candidate(Axis Axis, double Threshold, Int128 Numerator, Int128 Denominator);

    /// <summary>
    /// Sort along each axis and sweep, keeping running label counts: O(n log n) per node. The weighted child Gini
    /// is n - (sumL / nL + sumR / nR) over n, where sumL and sumR are sums of squared label counts, so the best
    /// split maximises sumL / nL + sumR / nR. That is compared as exact fractions, so ties are real ties.
    /// </summary>
    private static Candidate? BestSplit(LabeledPoint[] pts, int[] totals, int labelCount)
    {
        int n = pts.Length;
        Candidate? best = null;
        foreach (var axis in new[] { Axis.X, Axis.Y })
        {
            var sorted = pts.OrderBy(p => Value(p, axis)).ToArray();
            var left = new int[labelCount];
            var right = (int[])totals.Clone();
            long sumL = 0, sumR = 0;
            foreach (var c in right) sumR += (long)c * c;

            for (int i = 0; i < n - 1; i++)
            {
                int l = sorted[i].Label;
                sumL += 2L * left[l] + 1; left[l]++;
                sumR -= 2L * right[l] - 1; right[l]--;

                double a = Value(sorted[i], axis), b = Value(sorted[i + 1], axis);
                if (a == b) continue;
                double mid = (a + b) / 2;
                if (!(mid > a)) continue;   // adjacent doubles that round back onto a cannot be told apart by "<"

                long nL = i + 1, nR = n - nL;
                // sumL / nL + sumR / nR as one fraction
                var num = (Int128)sumL * nR + (Int128)sumR * nL;
                var den = (Int128)nL * nR;
                if (best is null || num * best.Value.Denominator > best.Value.Numerator * den)
                    best = new Candidate(axis, mid, num, den);
            }
        }
        return best;
    }

    public static int Predict(TreeNode tree, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(tree);
        while (tree is SplitNode s)
            tree = (s.Axis == Axis.X ? x : y) < s.Threshold ? s.Below : s.AtOrAbove;
        return ((Leaf)tree).Label;
    }

    /// <summary>The leaves as rectangles that tile the box exactly (a split outside the box leaves one side out).</summary>
    public static IReadOnlyList<Region> Regions(TreeNode tree, double xMin, double xMax, double yMin, double yMax)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var result = new List<Region>();
        Walk(tree, xMin, xMax, yMin, yMax, result);
        return result;
    }

    private static void Walk(TreeNode node, double x0, double x1, double y0, double y1, List<Region> into)
    {
        if (node is Leaf leaf) { into.Add(new Region(x0, x1, y0, y1, leaf.Label)); return; }
        var s = (SplitNode)node;
        double lo = s.Axis == Axis.X ? x0 : y0, hi = s.Axis == Axis.X ? x1 : y1;
        double t = s.Threshold;
        if (t <= lo) { Walk(s.AtOrAbove, x0, x1, y0, y1, into); return; }
        if (t >= hi) { Walk(s.Below, x0, x1, y0, y1, into); return; }
        if (s.Axis == Axis.X)
        {
            Walk(s.Below, x0, t, y0, y1, into);
            Walk(s.AtOrAbove, t, x1, y0, y1, into);
        }
        else
        {
            Walk(s.Below, x0, x1, y0, t, into);
            Walk(s.AtOrAbove, x0, x1, t, y1, into);
        }
    }

    public static int Depth(TreeNode tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree is SplitNode s ? 1 + Math.Max(Depth(s.Below), Depth(s.AtOrAbove)) : 0;
    }
}
