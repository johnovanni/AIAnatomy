using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;

namespace AIAnatomy.Presentation.Tests.IdeaMap;

/// <summary>The MSAGL layout of the shipped idea map, with bubble sizes estimated from the text at a given body size.</summary>
public class GraphLayoutTests
{
    /// <summary>Bubble size for a body text size: 8.6 px per character plus 24 px padding, 36 px tall, at 13 pt.</summary>
    internal static Dictionary<string, (double Width, double Height)> Sizes(IdeaMapViewModel map, double bodyPt) =>
        map.Bubbles.ToDictionary(b => b.Id, b =>
        {
            var text = b.Number == null ? b.Label : $"{b.Number} {b.Label}";
            return (8.6 * text.Length * bodyPt / 13 + 24, 36 * bodyPt / 13);
        });

    private static GraphLayoutResult LayOut(MapMode mode, double bodyPt)
    {
        using var env = new TestEnvironment();
        var map = IdeaMapViewModelTests.NewMap(env);
        map.Mode = mode;
        map.Arrange(Sizes(map, bodyPt));
        return map.Layout!;
    }

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

        Assert.True(layout.Width <= 1040, $"{mode} is {layout.Width:F0} px wide at 13 pt.");
        Assert.True(layout.Height <= 660, $"{mode} is {layout.Height:F0} px high at 13 pt.");
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
        Assert.InRange(centre.X + centre.Width / 2, layout.Width / 3, 2 * layout.Width / 3);
        Assert.InRange(centre.Y + centre.Height / 2, layout.Height / 3, 2 * layout.Height / 3);
    }

    [Fact]
    public void Loop_WholeLoopLines_EndAtTheGroupFrame()
    {
        var layout = LayOut(MapMode.Loop, 13);

        Assert.NotNull(layout.Group);
        var toLoop = layout.Edges.Where(e => e.To == ConnectionList.WholeLoop).Select(e => e.From).Order().ToList();
        Assert.Equal(["architecture-map", "scale", "step-through"], toLoop);
    }

    /// <summary>The empty space between two boxes: the larger of the horizontal and vertical gaps (negative if they overlap).</summary>
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
