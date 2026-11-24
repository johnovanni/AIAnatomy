using AIAnatomy.Lessons;
using Microsoft.Msagl.Core.DataStructures;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Layout.ProximityOverlapRemoval;
using Microsoft.Msagl.Core.Layout.ProximityOverlapRemoval.MinimumSpanningTree;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.Routing;

namespace AIAnatomy.Presentation.IdeaMap;

/// <summary>
/// Lays the idea map out with Microsoft Automatic Graph Layout (MSAGL). Radial: multidimensional scaling (MDS) towards
/// the distances of a radial tree round the centre box, then MSAGL's overlap removal. Loop: a layered (Sugiyama)
/// top-to-bottom layout in which the group is first laid out on its own and then placed as one box. In both, every
/// line is then routed by MSAGL's spline router round the boxes (lines to the whole loop end at the group's frame)
/// and sampled to a polyline. Deterministic: the same input gives the same result.
/// </summary>
public sealed class MsaglGraphLayout : IGraphLayout
{
    /// <summary>Points per sampled curve segment (at least 16).</summary>
    public const int PointsPerSegment = 16;

    /// <summary>Empty space round the whole map, px.</summary>
    public const double Margin = 8;

    /// <summary>Least space between two boxes, px (Radial; Loop uses MSAGL's default of 10).</summary>
    public const double BoxGap = 10;

    // Radial settings, found by trying them against the shipped content at 10, 13 and 24 pt (see docs/decisions.md).
    private const double TargetAspect = 1040.0 / 660; // the map area at 13 pt
    private const double RingRadius = 1.8;            // centre to its children, in MDS units
    private const double ArcRadius = 1;               // a child to its own children (grows with their number)
    private const double ArcStepDegrees = 40;         // between neighbouring grandchildren on an arc of ArcRadius
    private const double ShareExponent = 0.8;         // a child's share of the circle grows as (its children)^this
    private const double AreaFactor = 1.2;            // map area / box area before overlap removal
    private const double RoutePadding = 4;            // least space between a line and a box it passes, px
    private const string GroupKey = "\u0001group";

    public GraphLayoutResult Layout(GraphInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Rectangle? frame = null;
        var centres = input.Mode == MapMode.Radial ? PlaceRadial(input) : PlaceLoop(input, out frame);
        return Route(input, centres, frame);
    }

    // ---- Placement -------------------------------------------------------------------------------------------------

    private static Dictionary<string, Point> PlaceRadial(GraphInput input)
    {
        var graph = new GeometryGraph();
        var nodes = AddNodes(graph, input.Nodes);

        // MDS places boxes to match given distances. The distances given are those of a radial tree: the centre's
        // children on a ring round it, each with a share of the circle that grows with its number of children, and each
        // child's own children on an arc round it, facing away from the centre; all in the given (contents) order.
        var tree = input.PlacementEdges.Where(e => nodes.ContainsKey(e.From) && nodes.ContainsKey(e.To)).ToList();
        var top = tree.Where(e => e.From == input.CentreId).Select(e => e.To).ToList();
        var kidsOf = tree.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());
        double Weight(string t) => Math.Pow(Math.Max(1, kidsOf.GetValueOrDefault(t)?.Count ?? 0), ShareExponent);
        double total = top.Sum(Weight), start = 0;
        var target = new Dictionary<string, Point>(StringComparer.Ordinal);
        if (input.CentreId != null && nodes.ContainsKey(input.CentreId)) target[input.CentreId] = new Point();
        foreach (var t in top)
        {
            double share = 2 * Math.PI * Weight(t) / total, a = start + share / 2;
            start += share;
            var at = new Point(Math.Cos(a), Math.Sin(a)) * RingRadius;
            target[t] = at;
            var kids = kidsOf.GetValueOrDefault(t) ?? [];
            double rho = ArcRadius * Math.Sqrt(Math.Max(1, kids.Count) / 2.0);
            double span = Math.Min(Math.PI * 1.2, (kids.Count - 1) * ArcStepDegrees * Math.PI / 180 * ArcRadius / rho);
            for (int j = 0; j < kids.Count; j++)
            {
                double b = a + (kids.Count == 1 ? 0 : span * ((double)j / (kids.Count - 1) - 0.5));
                target[kids[j]] = at + new Point(Math.Cos(b), Math.Sin(b)) * rho;
            }
        }
        var hinted = new HashSet<(string, string)>();
        void Hint(string a, string b)
        {
            if (a != b && target.ContainsKey(a) && target.ContainsKey(b) && hinted.Add((a, b)) && hinted.Add((b, a)))
                graph.Edges.Add(new Edge(nodes[a], nodes[b]) { Length = Math.Max(1e-3, (target[a] - target[b]).Length) });
        }
        foreach (var e in tree) Hint(e.From, e.To);
        for (int i = 0; i < top.Count; i++)
            for (int j = i + 1; j < top.Count; j++) Hint(top[i], top[j]);
        foreach (var kids in top.Select(t => kidsOf.GetValueOrDefault(t) ?? []))
            for (int i = 0; i < kids.Count; i++)
            {
                if (input.CentreId != null) Hint(input.CentreId, kids[i]);
                foreach (var t in top) Hint(t, kids[i]); // fixes which way round each arc goes
                for (int j = i + 1; j < kids.Count; j++) Hint(kids[i], kids[j]);
            }

        new MdsGraphLayout(new MdsLayoutSettings { RemoveOverlaps = false, RunInParallel = false, ScaleX = 1, ScaleY = 1 }, graph).Run();

        // MDS may give the mirror image or any rotation: turn it so the centre's children go clockwise, the first
        // at the upper left.
        var raw = nodes.ToDictionary(kv => kv.Key, kv => kv.Value.Center, StringComparer.Ordinal);
        var hub = input.CentreId != null && raw.TryGetValue(input.CentreId, out var c0) ? c0 : new Point();
        double signedArea = 0;
        for (int i = 0; i < top.Count; i++)
            signedArea += Point.CrossProduct(raw[top[i]] - hub, raw[top[(i + 1) % top.Count]] - hub);
        double mirror = signedArea > 0 ? -1 : 1; // anticlockwise (y up) -> mirror
        foreach (var id in raw.Keys.ToList()) raw[id] = new Point((raw[id].X - hub.X) * mirror, raw[id].Y - hub.Y);
        if (top.Count > 0)
        {
            var first = raw[top[0]];
            double turn = 3 * Math.PI / 4 - Math.Atan2(first.Y, first.X);
            foreach (var id in raw.Keys.ToList()) raw[id] = raw[id].Rotate(turn);
        }

        // Stretch the shape to the map area's proportions and scale it so the boxes cover about 1/AreaFactor of it;
        // MSAGL's overlap removal then pushes apart the boxes that still touch. The stretch is corrected after each of
        // three passes, measuring the boxes' own extent, so the finished map has about the map area's proportions.
        var box = new Rectangle(raw.Values);
        double w0 = Math.Max(box.Width, 1e-6), h0 = Math.Max(box.Height, 1e-6);
        double ax = Math.Sqrt(TargetAspect * h0 / w0);
        double area = nodes.Values.Sum(n => (n.Width + BoxGap) * (n.Height + BoxGap));
        double scale = Math.Sqrt(AreaFactor * area / (w0 * h0));
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var (id, p) in raw) nodes[id].Center = new Point(p.X * ax * scale, p.Y / ax * scale);
            new GTreeOverlapRemoval(new OverlapRemovalSettings { NodeSeparation = BoxGap }, nodes.Values.ToArray()).RemoveOverlaps();
            var extent = new Rectangle(nodes.Values.Select(n => n.BoundingBox));
            ax *= Math.Sqrt(TargetAspect * extent.Height / extent.Width);
        }

        var shift = input.CentreId != null && nodes.TryGetValue(input.CentreId, out var centre) ? centre.Center : new Point();
        return nodes.ToDictionary(kv => kv.Key, kv => kv.Value.Center - shift, StringComparer.Ordinal);
    }

    private static Dictionary<string, Point> PlaceLoop(GraphInput input, out Rectangle? groupBox)
    {
        var members = input.GroupMembers.Where(m => input.Nodes.Any(n => n.Id == m)).ToHashSet(StringComparer.Ordinal);
        groupBox = null;

        // 1. The group on its own: a small layered layout of its members and the lines between them.
        Dictionary<string, Point>? inner = null;
        Size frameSize = default;
        if (members.Count > 0)
        {
            var g = new GeometryGraph();
            var n = AddNodes(g, input.Nodes.Where(x => members.Contains(x.Id)));
            foreach (var e in input.PlacementEdges)
                if (members.Contains(e.From) && members.Contains(e.To)) g.Edges.Add(new Edge(n[e.From], n[e.To]));
            new LayeredLayout(g, new SugiyamaLayoutSettings()).Run();
            g.UpdateBoundingBox();
            var box = new Rectangle(g.Nodes.Select(x => x.BoundingBox));
            double rowHeight = input.Nodes.Where(x => members.Contains(x.Id)).Max(x => x.Height);
            double padSide = 0.4 * rowHeight, padTop = rowHeight; // room for the group's title above its boxes
            frameSize = new Size(box.Width + 2 * padSide, box.Height + padTop + padSide);
            // Offsets of member centres from the frame's centre (y up).
            var frameCentre = new Point(box.Left - padSide + frameSize.Width / 2, box.Top + padTop - frameSize.Height / 2);
            inner = n.ToDictionary(kv => kv.Key, kv => kv.Value.Center - frameCentre, StringComparer.Ordinal);
        }

        // 2. Everything else, with the group as one box.
        var graph = new GeometryGraph();
        var nodes = AddNodes(graph, input.Nodes.Where(x => !members.Contains(x.Id)));
        if (members.Count > 0)
        {
            var groupNode = new Node(CurveFactory.CreateRectangle(frameSize.Width, frameSize.Height, new Point()), GroupKey);
            nodes.Add(GroupKey, groupNode);
            graph.Nodes.Add(groupNode);
        }
        string Outer(string id) => id == ConnectionList.WholeLoop || members.Contains(id) ? GroupKey : id;
        var seen = new HashSet<(string, string)>();
        foreach (var e in input.PlacementEdges)
        {
            // A line to the whole loop places its lesson below the group (as if it hung from it).
            var (from, to) = e.To == ConnectionList.WholeLoop ? (GroupKey, e.From) : (Outer(e.From), Outer(e.To));
            if (from == to || !nodes.ContainsKey(from) || !nodes.ContainsKey(to) || !seen.Add((from, to))) continue;
            graph.Edges.Add(new Edge(nodes[from], nodes[to]));
        }
        var settings = new SugiyamaLayoutSettings();
        // Boxes with no lines at all (lessons still to come) go in the top row rather than the bottom one.
        var loose = nodes.Values.Where(n => !n.Edges.Any()).ToArray();
        var top = nodes.Values.FirstOrDefault(n => n.Edges.Any() && !n.InEdges.Any());
        if (top != null) foreach (var l in loose) graph.Edges.Add(new Edge(l, top));
        new LayeredLayout(graph, settings).Run();

        // They share the top row by themselves; put them in their given order, keeping the gaps between them.
        var byX = loose.OrderBy(n => n.Center.X).ToList();
        if (top != null && byX.Count > 1 && !byX.SequenceEqual(loose))
        {
            var gaps = byX.Zip(byX.Skip(1), (a, b) => b.BoundingBox.Left - a.BoundingBox.Right).ToList();
            double x = byX[0].BoundingBox.Left;
            for (int i = 0; i < loose.Length; i++)
            {
                loose[i].Center = new Point(x + loose[i].Width / 2, loose[i].Center.Y);
                x += loose[i].Width + (i < gaps.Count ? gaps[i] : 0);
            }
        }

        var centres = nodes.Where(kv => kv.Key != GroupKey).ToDictionary(kv => kv.Key, kv => kv.Value.Center, StringComparer.Ordinal);
        if (inner != null)
        {
            var g = nodes[GroupKey].Center;
            foreach (var (id, offset) in inner) centres[id] = g + offset;
            groupBox = new Rectangle(new Size(frameSize.Width, frameSize.Height), g);
        }
        return centres;
    }

    // ---- Routing ---------------------------------------------------------------------------------------------------

    private static GraphLayoutResult Route(GraphInput input, Dictionary<string, Point> centres, Rectangle? frame)
    {
        var graph = new GeometryGraph();
        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var n in input.Nodes)
        {
            var node = new Node(CurveFactory.CreateRectangle(n.Width, n.Height, centres[n.Id]), n.Id);
            nodes.Add(n.Id, node);
            graph.Nodes.Add(node);
        }
        Cluster? group = null;
        if (frame is { } f)
        {
            group = new Cluster(input.GroupMembers.Where(nodes.ContainsKey).Select(m => nodes[m]))
            {
                BoundaryCurve = CurveFactory.CreateRectangle(f),
                UserData = ConnectionList.WholeLoop,
            };
            graph.RootCluster.AddChild(group);
        }

        Node? End(string id) => id == ConnectionList.WholeLoop ? group : nodes.GetValueOrDefault(id);
        var edges = new List<(LayoutEdge Layout, Edge Edge)>();
        var seen = new HashSet<(string, string)>();
        foreach (var e in input.PlacementEdges.Concat(input.RoutedOnlyEdges))
        {
            if (End(e.From) is not { } from || End(e.To) is not { } to || !seen.Add((e.From, e.To))) continue;
            var edge = new Edge(from, to);
            graph.Edges.Add(edge);
            edges.Add((e, edge));
        }
        graph.UpdateBoundingBox();
        new SplineRouter(graph, new EdgeRoutingSettings { EdgeRoutingMode = EdgeRoutingMode.Spline, Padding = RoutePadding }).Run();

        var sampled = edges.Select(x => (x.Layout, Points: Sample(x.Edge.Curve ?? new LineSegment(x.Edge.Source.Center, x.Edge.Target.Center)))).ToList();
        var bounds = new Rectangle(nodes.Values.Select(n => n.BoundingBox));
        if (frame is { } fr) bounds.Add(fr);
        foreach (var (_, points) in sampled)
            foreach (var p in points) bounds.Add(p);

        // MSAGL's y axis points up; the map's points down.
        double left = bounds.Left - Margin, top = bounds.Top + Margin;
        (double X, double Y) ToMap(Point p) => (p.X - left, top - p.Y);
        var placed = input.Nodes.Select(n =>
        {
            var b = nodes[n.Id].BoundingBox;
            return new PlacedNode(n.Id, b.Left - left, top - b.Top, n.Width, n.Height);
        }).ToList();
        var routed = sampled.Select(s => new RoutedEdge(s.Layout.From, s.Layout.To, s.Points.Select(ToMap).ToList())).ToList();
        (double, double, double, double)? groupRect = frame is { } r ? (r.Left - left, top - r.Top, r.Width, r.Height) : null;
        return new GraphLayoutResult(placed, routed, bounds.Width + 2 * Margin, bounds.Height + 2 * Margin, groupRect);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private static Dictionary<string, Node> AddNodes(GeometryGraph graph, IEnumerable<LayoutNode> layoutNodes)
    {
        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var n in layoutNodes)
        {
            var node = new Node(CurveFactory.CreateRectangle(n.Width, n.Height, new Point()), n.Id);
            nodes.Add(n.Id, node);
            graph.Nodes.Add(node);
        }
        return nodes;
    }

    private static List<Point> Sample(ICurve curve)
    {
        var segments = curve is Curve c ? c.Segments : [curve];
        var points = new List<Point>();
        foreach (var s in segments)
            for (int i = points.Count == 0 ? 0 : 1; i <= PointsPerSegment; i++)
                points.Add(s[s.ParStart + (s.ParEnd - s.ParStart) * i / PointsPerSegment]);
        return points;
    }
}
