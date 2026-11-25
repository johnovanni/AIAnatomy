using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;

namespace AIAnatomy.Presentation.Tests.IdeaMap;

/// <summary>The MSAGL layout of the shipped idea map, with bubble sizes estimated from the text at a given body size.</summary>
public class GraphLayoutTests
{
    /// <summary>
    /// A bubble's size as IdeaMapView builds it, estimated without WPF: small text (0.85 × body, never under 9 pt; 4/3 px
    /// per pt), a number line (Cascadia Mono, 0.6 em a character) when the bubble has a number or a mark, then the label
    /// (about 0.5 em a character, wrapping at 11 em), each line 1.25 em high, plus 10 px padding and a 3 px border on each
    /// side. Every lesson is taken as checked ("  ✓ checked" on its number line), the widest a mark makes it.
    /// </summary>
    internal static Dictionary<string, (double Width, double Height)> Sizes(IdeaMapViewModel map, double bodyPt) =>
        map.Bubbles.ToDictionary(b => b.Id, b => BubbleSize(b, bodyPt));

    internal static (double Width, double Height) BubbleSize(IdeaBubble b, double bodyPt)
    {
        double small = Math.Max(9, 0.85 * bodyPt) * 4 / 3;
        string head = (b.Number ?? "") + (b.IsLesson && !b.IsLater ? "  ✓ checked" : b.IsLater ? "  later" : "");
        double headWidth = head.Length * 0.6 * small;
        double labelWidth = b.Label.Length * 0.5 * small, wrap = 11 * small;
        int lines = (head.Length > 0 ? 1 : 0) + (int)Math.Ceiling(labelWidth / wrap);
        return (Math.Ceiling(Math.Max(headWidth, Math.Min(labelWidth, wrap)) + 26), Math.Ceiling(lines * 1.25 * small + 16));
    }

    /// <summary>A line label's size as IdeaMapView's chip: small text (0.5 em a character, one line of 1.25 em) plus 5/1 px
    /// padding and a 1 px border.</summary>
    internal static (double Width, double Height) LabelSize(string text, double bodyPt)
    {
        double small = Math.Max(9, 0.85 * bodyPt) * 4 / 3;
        return (Math.Ceiling(text.Length * 0.5 * small + 12), Math.Ceiling(1.25 * small + 4));
    }

    internal static Dictionary<string, (double Width, double Height)> LabelSizes(IdeaMapViewModel map, double bodyPt) =>
        map.LineLabels.ToDictionary(l => l, l => LabelSize(l, bodyPt));

    private static GraphLayoutResult LayOut(MapMode mode, double bodyPt) => LayOut(mode, bodyPt, out _);

    private static GraphLayoutResult LayOut(MapMode mode, double bodyPt, out IdeaMapViewModel map)
    {
        using var env = new TestEnvironment();
        map = IdeaMapViewModelTests.NewMap(env);
        map.Mode = mode;
        map.Arrange(Sizes(map, bodyPt), LabelSizes(map, bodyPt));
        return map.Layout!;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(24)]
    public void Loop_FlowLabels_HaveRoomOfTheirOwn(double bodyPt)
    {
        var layout = LayOut(MapMode.Loop, bodyPt, out var map);
        var labelled = layout.Edges.Where(e => map.FlowLabel(e) != null).ToList();
        Assert.Equal(IdeaMapViewModelTests.Map().FlowEdges.Count, labelled.Count);
        var rects = labelled.Select(e =>
        {
            Assert.NotNull(e.LabelCentre);
            var (w, h) = LabelSize(map.FlowLabel(e)!, bodyPt);
            var c = e.LabelCentre!.Value;
            return (Edge: e, X: c.X - w / 2, Y: c.Y - h / 2, W: w, H: h);
        }).ToList();
        var g = layout.Group!.Value;

        foreach (var r in rects)
        {
            string name = $"'{map.FlowLabel(r.Edge)}' at {bodyPt} pt";
            Assert.True(r.X >= 0 && r.Y >= 0 && r.X + r.W <= layout.Width && r.Y + r.H <= layout.Height, $"{name} is outside the map.");
            foreach (var n in layout.Nodes)
                Assert.False(Overlap(r.X, r.Y, r.W, r.H, n.X, n.Y, n.Width, n.Height), $"{name} covers {n.Id}.");
            bool inside = r.X > g.X && r.Y > g.Y && r.X + r.W < g.X + g.Width && r.Y + r.H < g.Y + g.Height;
            Assert.True(inside || !Overlap(r.X, r.Y, r.W, r.H, g.X, g.Y, g.Width, g.Height), $"{name} lies across the Block frame's border.");
            foreach (var o in rects.Where(o => o.Edge != r.Edge))
                Assert.False(Overlap(r.X, r.Y, r.W, r.H, o.X, o.Y, o.W, o.H), $"{name} covers '{map.FlowLabel(o.Edge)}'.");
            // Its own line runs through it; no other line does.
            Assert.Contains(r.Edge.Points, p => DistanceToRect(p, r.X, r.Y, r.W, r.H) <= 2);
            foreach (var other in layout.Edges.Where(e => e != r.Edge))
                for (int i = 0; i + 1 < other.Points.Count; i++)
                    Assert.False(SegmentHitsRect(other.Points[i], other.Points[i + 1], r.X + 2, r.Y + 2, r.X + r.W - 2, r.Y + r.H - 2),
                        $"The line {other.From} -> {other.To} crosses {name}.");
        }
    }

    private static bool Overlap(double ax, double ay, double aw, double ah, double bx, double by, double bw, double bh) =>
        ax < bx + bw && bx < ax + aw && ay < by + bh && by < ay + ah;

    [Theory]
    [InlineData(MapMode.Radial, 10)]
    [InlineData(MapMode.Radial, 24)]
    [InlineData(MapMode.Loop, 10)]
    [InlineData(MapMode.Loop, 24)]
    public void Layout_NoOverlaps_AllInsideBounds(MapMode mode, double bodyPt)
    {
        var layout = LayOut(mode, bodyPt);

        for (int i = 0; i < layout.Nodes.Count; i++)
            for (int j = i + 1; j < layout.Nodes.Count; j++)
                Assert.True(Gap(layout.Nodes[i], layout.Nodes[j]) > 0,
                    $"{layout.Nodes[i].Id} and {layout.Nodes[j].Id} overlap or touch.");
        foreach (var n in layout.Nodes)
            Assert.True(n.X >= 0 && n.Y >= 0 && n.X + n.Width <= layout.Width && n.Y + n.Height <= layout.Height,
                $"{n.Id} is outside the map.");
        foreach (var e in layout.Edges)
            Assert.All(e.Points, p => Assert.True(p.X >= 0 && p.Y >= 0 && p.X <= layout.Width && p.Y <= layout.Height,
                $"The line {e.From} -> {e.To} leaves the map."));
        if (layout.Group is { } g)
        {
            Assert.True(g.X >= 0 && g.Y >= 0 && g.X + g.Width <= layout.Width && g.Y + g.Height <= layout.Height);
            // The frame holds its members and nothing else.
            foreach (var n in layout.Nodes)
            {
                bool inside = n.X >= g.X && n.Y >= g.Y && n.X + n.Width <= g.X + g.Width && n.Y + n.Height <= g.Y + g.Height;
                bool touches = n.X < g.X + g.Width && g.X < n.X + n.Width && n.Y < g.Y + g.Height && g.Y < n.Y + n.Height;
                bool member = IdeaMapViewModelTests.Map().GroupMembers.Contains(n.Id);
                Assert.True(member ? inside : !touches, $"{n.Id} is {(member ? "outside" : "inside")} the group frame.");
            }
        }
    }

    [Theory]
    [InlineData(MapMode.Radial)]
    [InlineData(MapMode.Loop)]
    public void Layout_NoLineCrossesABubbleItDoesNotJoin(MapMode mode)
    {
        foreach (var bodyPt in new[] { 10.0, 13, 24 })
        {
            var layout = LayOut(mode, bodyPt);
            var byId = layout.Nodes.ToDictionary(n => n.Id);
            Assert.NotEmpty(layout.Edges);
            foreach (var e in layout.Edges)
            {
                Assert.True(e.Points.Count >= MsaglGraphLayout.PointsPerSegment, $"{e.From} -> {e.To} has too few points.");
                // Each line starts at its own bubble and ends at its own bubble (or the group frame).
                Assert.True(DistanceToRect(e.Points[0], byId[e.From]) <= 2, $"{e.From} -> {e.To} does not start at {e.From}.");
                var end = e.Points[^1];
                double toEnd = e.To == ConnectionList.WholeLoop && layout.Group is { } g
                    ? DistanceToRect(end, g.X, g.Y, g.Width, g.Height)
                    : DistanceToRect(end, byId[e.To]);
                Assert.True(toEnd <= 2, $"{e.From} -> {e.To} does not end at {e.To} ({bodyPt} pt).");

                foreach (var n in layout.Nodes.Where(n => n.Id != e.From && n.Id != e.To))
                    for (int i = 0; i + 1 < e.Points.Count; i++)
                        Assert.False(SegmentHitsRect(e.Points[i], e.Points[i + 1], n.X + 2, n.Y + 2, n.X + n.Width - 2, n.Y + n.Height - 2),
                            $"At {bodyPt} pt the line {e.From} -> {e.To} crosses {n.Id}.");
            }
        }
    }

    [Theory]
    [InlineData(MapMode.Radial)]
    [InlineData(MapMode.Loop)]
    public void Layout_At13pt_FitsTheMapArea(MapMode mode)
    {
        var layout = LayOut(mode, 13);

        // Radial now carries ten section-4 lessons (Stage 2), so its map is allowed to be up to 1.4 times the area and the
        // view scrolls it; Loop shows only the first of them and keeps the area.
        double allowance = mode == MapMode.Radial ? 1.4 : 1;
        Assert.True(layout.Width <= 1040 * allowance, $"{mode} is {layout.Width:F0} px wide at 13 pt.");
        Assert.True(layout.Height <= 660 * allowance, $"{mode} is {layout.Height:F0} px high at 13 pt.");
    }

    [Fact]
    public void Layout_IsDeterministic()
    {
        foreach (var mode in new[] { MapMode.Radial, MapMode.Loop })
        {
            var first = LayOut(mode, 13);
            var second = LayOut(mode, 13);

            Assert.Equal(first.Width, second.Width);
            Assert.Equal(first.Height, second.Height);
            Assert.Equal(first.Group, second.Group);
            Assert.Equal(first.Nodes, second.Nodes);
            Assert.Equal(first.Edges.Count, second.Edges.Count);
            for (int i = 0; i < first.Edges.Count; i++)
            {
                Assert.Equal((first.Edges[i].From, first.Edges[i].To), (second.Edges[i].From, second.Edges[i].To));
                Assert.Equal(first.Edges[i].Points, second.Edges[i].Points);
            }
        }
    }

    [Fact]
    public void Radial_CentreIsNearTheMiddle_AndEveryBubbleIsPlacedOnce()
    {
        using var env = new TestEnvironment();
        var map = IdeaMapViewModelTests.NewMap(env);
        map.Arrange(Sizes(map, 13));
        var layout = map.Layout!;

        Assert.Equal(map.Bubbles.Select(b => b.Id).Order(), layout.Nodes.Select(n => n.Id).Order());
        var centre = layout.Nodes.Single(n => n.Id == "centre");
        // Near the middle: the middle third, except that the left bound is 31% of the width. A built lesson's bubble keeps
        // room for its widest mark ("✓ checked"), so it is wider than a "later" placeholder's (at 13 pt 150 px against
        // 115); once lesson 4.4 was built (Stage 2A Task 11) the centre sat at 32.8% of the width, just left of a third.
        // 31% leaves 1.8 points (about 25 px) of margin (decisions.md, Task 11 review fixes).
        Assert.InRange(centre.X + centre.Width / 2, 0.31 * layout.Width, 2 * layout.Width / 3);
        Assert.InRange(centre.Y + centre.Height / 2, layout.Height / 3, 2 * layout.Height / 3);
    }

    [Fact]
    public void Loop_WholeLoopLines_EndAtTheGroupFrame()
    {
        var layout = LayOut(MapMode.Loop, 13);

        Assert.NotNull(layout.Group);
        var toLoop = layout.Edges.Where(e => e.To == ConnectionList.WholeLoop).Select(e => e.From).Order().ToList();
        Assert.Equal(["architecture-map", "residual-stream", "scale", "step-through"], toLoop);   // three connections and the flow
    }

    /// <summary>The empty space between two boxes: the larger of the horizontal and vertical gaps (negative if they overlap).</summary>
    // ---- Tree (the family tree, spec 5.1) ----------------------------------------------------------------------------

    private static readonly (string Parent, string[] Kids)[] TreeShape =
    [
        ("root", ["a", "b", "c"]),
        ("a", ["a1", "a2"]),
        ("b", ["b1", "b2"]),
        ("c", ["c1"]),
        ("b2", ["b2a", "b2b", "b2c", "b2d"]),
    ];

    private static GraphInput TreeInput(double w, double h)
    {
        var ids = new List<string> { "root" };
        foreach (var (_, kids) in TreeShape) ids.AddRange(kids);
        var edges = TreeShape.SelectMany(t => t.Kids.Select(k => new LayoutEdge(t.Parent, k))).ToList();
        // Nodes in file order: parents before children, siblings in order (a tree walked depth first).
        var order = new List<string>();
        void Walk(string id)
        {
            order.Add(id);
            foreach (var k in TreeShape.Where(t => t.Parent == id).SelectMany(t => t.Kids)) Walk(k);
        }
        Walk("root");
        Assert.Equal(13, order.Count);
        return new GraphInput(MapMode.Tree, order.Select(i => new LayoutNode(i, w, h)).ToList(), edges, [], "root", []);
    }

    public static TheoryData<double, double> TreeBoxSizes => new() { { 120, 40 }, { 300, 90 } };

    [Theory]
    [MemberData(nameof(TreeBoxSizes))]
    public void Tree_ParentsSitAboveTheirChildren(double w, double h)
    {
        var input = TreeInput(w, h);
        var r = new MsaglGraphLayout().Layout(input);
        var by = r.Nodes.ToDictionary(n => n.Id);
        foreach (var e in input.PlacementEdges)
            Assert.True(by[e.From].Y + by[e.From].Height <= by[e.To].Y - 10, $"{e.From} is not clear above {e.To}.");
    }

    [Theory]
    [MemberData(nameof(TreeBoxSizes))]
    public void Tree_SiblingsKeepTheirOrderLeftToRight(double w, double h)
    {
        var input = TreeInput(w, h);
        var r = new MsaglGraphLayout().Layout(input);
        var by = r.Nodes.ToDictionary(n => n.Id);
        foreach (var g in input.PlacementEdges.GroupBy(e => e.From))
        {
            var xs = input.Nodes.Where(n => g.Any(e => e.To == n.Id)).Select(n => by[n.Id].X + by[n.Id].Width / 2).ToList();
            for (int i = 1; i < xs.Count; i++)
                Assert.True(xs[i] > xs[i - 1], $"Children of {g.Key} are out of order: {string.Join(", ", xs)}.");
        }
    }

    /// <summary>Not only siblings: a whole row keeps the order of the branches above it, so no two lines cross.</summary>
    [Theory]
    [MemberData(nameof(TreeBoxSizes))]
    public void Tree_EachRowKeepsTheOrderOfTheBranchesAboveIt(double w, double h)
    {
        var r = new MsaglGraphLayout().Layout(TreeInput(w, h));
        var by = r.Nodes.ToDictionary(n => n.Id);
        foreach (var row in new[] { new[] { "a", "b", "c" }, ["a1", "a2", "b1", "b2", "c1"], ["b2a", "b2b", "b2c", "b2d"] })
            for (int i = 1; i < row.Length; i++)
                Assert.True(by[row[i]].X >= by[row[i - 1]].X + by[row[i - 1]].Width, $"{row[i]} is not right of {row[i - 1]}.");
    }

    [Theory]
    [MemberData(nameof(TreeBoxSizes))]
    public void Tree_NoTwoBoxesOverlap(double w, double h)
    {
        var r = new MsaglGraphLayout().Layout(TreeInput(w, h));
        for (int i = 0; i < r.Nodes.Count; i++)
            for (int j = i + 1; j < r.Nodes.Count; j++)
            {
                var a = r.Nodes[i]; var b = r.Nodes[j];
                Assert.False(Overlap(a.X, a.Y, a.Width, a.Height, b.X, b.Y, b.Width, b.Height), $"{a.Id} overlaps {b.Id}.");
            }
    }

    [Theory]
    [MemberData(nameof(TreeBoxSizes))]
    public void Tree_EveryEdgeIsRoutedFromParentToChild(double w, double h)
    {
        var input = TreeInput(w, h);
        var r = new MsaglGraphLayout().Layout(input);
        var by = r.Nodes.ToDictionary(n => n.Id);
        Assert.Equal(input.PlacementEdges.Count, r.Edges.Count);
        foreach (var e in input.PlacementEdges)
        {
            var routed = Assert.Single(r.Edges, x => x.From == e.From && x.To == e.To);
            Assert.True(DistanceToRect(routed.Points[0], by[e.From]) <= 2, $"{e.From}->{e.To} does not start at the parent.");
            Assert.True(DistanceToRect(routed.Points[^1], by[e.To]) <= 2, $"{e.From}->{e.To} does not end at the child.");
        }
    }

    [Fact]
    public void Tree_SameInputGivesTheSameLayout()
    {
        var a = new MsaglGraphLayout().Layout(TreeInput(120, 40));
        var b = new MsaglGraphLayout().Layout(TreeInput(120, 40));
        Assert.Equal(a.Nodes, b.Nodes);
        Assert.Equal(a.Width, b.Width);
        Assert.Equal(a.Height, b.Height);
        Assert.Equal(a.Edges.Select(e => e.Points), b.Edges.Select(e => e.Points));
    }

    internal static double Gap(PlacedNode a, PlacedNode b) => Math.Max(
        Math.Max(b.X - (a.X + a.Width), a.X - (b.X + b.Width)),
        Math.Max(b.Y - (a.Y + a.Height), a.Y - (b.Y + b.Height)));

    private static double DistanceToRect((double X, double Y) p, PlacedNode n) => DistanceToRect(p, n.X, n.Y, n.Width, n.Height);

    /// <summary>Distance from a point to a rectangle's outline.</summary>
    private static double DistanceToRect((double X, double Y) p, double x, double y, double w, double h)
    {
        double dx = Math.Max(Math.Max(x - p.X, 0), p.X - (x + w));
        double dy = Math.Max(Math.Max(y - p.Y, 0), p.Y - (y + h));
        if (dx > 0 || dy > 0) return Math.Sqrt(dx * dx + dy * dy);
        return Math.Min(Math.Min(p.X - x, x + w - p.X), Math.Min(p.Y - y, y + h - p.Y));
    }

    /// <summary>True if the segment p–q has any point inside the rectangle (x0, y0)–(x1, y1) (Liang–Barsky clipping).</summary>
    private static bool SegmentHitsRect((double X, double Y) p, (double X, double Y) q, double x0, double y0, double x1, double y1)
    {
        double t0 = 0, t1 = 1, dx = q.X - p.X, dy = q.Y - p.Y;
        foreach (var (pp, qq) in new[] { (-dx, p.X - x0), (dx, x1 - p.X), (-dy, p.Y - y0), (dy, y1 - p.Y) })
        {
            if (pp == 0)
            {
                if (qq < 0) return false;
                continue;
            }
            double r = qq / pp;
            if (pp < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
        }
        return t0 <= t1;
    }
}
