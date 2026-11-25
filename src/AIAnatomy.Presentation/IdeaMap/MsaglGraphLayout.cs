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

    // Loop: the group frame's padding, as a fraction of its tallest box (the top holds the "Block × N" title).
    private const double FramePadSide = 0.4, FramePadTop = 0.75;
    private const double LabelGap = 4;            // px, least space round a line label
    private const double CornerStep = 4;           // px, when looking for room in a corner
    private const double LoopNodeSeparation = 20;  // MSAGL default 10; leaves lines a way between boxes in a row
    private const double LoopLayerSeparation = 20; // MSAGL's default is 30; labels take a layer of their own between rows
    private const double TreeNodeSeparation = 20;  // px, between boxes in a row
    private const double TreeLayerSeparation = 40; // px, between rows
    private const string GroupKey = "\u0001group";

    public GraphLayoutResult Layout(GraphInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Rectangle? frame = null;
        Dictionary<(string, string), Point>? labels = null;
        var centres = input.Mode switch
        {
            MapMode.Radial => PlaceRadial(input),
            MapMode.Tree => PlaceTree(input),
            _ => PlaceLoop(input, out frame, out labels),
        };
        return Route(input, centres, frame, labels ?? []);
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

    private static Dictionary<string, Point> PlaceLoop(GraphInput input, out Rectangle? groupBox,
        out Dictionary<(string, string), Point> labels)
    {
        labels = [];
        var members = input.GroupMembers.Where(m => input.Nodes.Any(n => n.Id == m)).ToHashSet(StringComparer.Ordinal);
        groupBox = null;

        // 1. The group on its own: its members in one row, in the given order, inside a frame with room for its title.
        //    (One row rather than a layered layout of the members: it keeps the map a row shorter.)
        Dictionary<string, Point>? inner = null;
        Size frameSize = default;
        if (members.Count > 0)
        {
            var row = input.GroupMembers.Distinct().Select(m => input.Nodes.First(x => x.Id == m)).ToList();
            double rowHeight = row.Max(x => x.Height), rowWidth = row.Sum(x => x.Width) + (row.Count - 1) * BoxGap;
            double padSide = FramePadSide * rowHeight, padTop = FramePadTop * rowHeight;
            frameSize = new Size(rowWidth + 2 * padSide, rowHeight + padTop + padSide);
            // Offsets of member centres from the frame's centre (y up).
            inner = new Dictionary<string, Point>(StringComparer.Ordinal);
            double x = -frameSize.Width / 2 + padSide, y = frameSize.Height / 2 - padTop - rowHeight / 2;
            foreach (var m in row)
            {
                inner[m.Id] = new Point(x + m.Width / 2, y);
                x += m.Width + BoxGap;
            }
        }

        // 2. Sort the other boxes: those joined only to the whole loop go in a column beside the group, those with no
        //    lines at all in the corners; the rest are laid out in layers, with the group as one box.
        var edges = input.PlacementEdges.Where(e => e.From != e.To).ToList();
        bool Touches(LayoutEdge e, string id) => e.From == id || e.To == id;
        var outside = input.Nodes.Where(x => !members.Contains(x.Id)).ToList();
        var toLoop = outside.Where(x => edges.Any(e => Touches(e, x.Id))
            && edges.Where(e => Touches(e, x.Id)).All(e => e.To == ConnectionList.WholeLoop)).ToList();
        var loose = outside.Where(x => !edges.Any(e => Touches(e, x.Id))).ToList();
        var layered = outside.Except(toLoop).Except(loose).ToList();

        var graph = new GeometryGraph();
        var nodes = AddNodes(graph, layered);
        if (members.Count > 0)
        {
            var groupNode = new Node(CurveFactory.CreateRectangle(frameSize.Width, frameSize.Height, new Point()), GroupKey);
            nodes.Add(GroupKey, groupNode);
            graph.Nodes.Add(groupNode);
        }
        string Outer(string id) => id == ConnectionList.WholeLoop || members.Contains(id) ? GroupKey : id;
        var seen = new HashSet<(string, string)>();
        var labelled = new List<(LayoutEdge Layout, Edge Edge)>();
        foreach (var e in edges)
        {
            var (from, to) = (Outer(e.From), Outer(e.To));
            if (from == to || !nodes.ContainsKey(from) || !nodes.ContainsKey(to) || !seen.Add((from, to))) continue;
            // A labelled edge gets an MSAGL label of the asked size, so the layered layout keeps room for it.
            var edge = e.HasLabel ? new Edge(nodes[from], nodes[to], e.LabelWidth, e.LabelHeight, 1) : new Edge(nodes[from], nodes[to]);
            graph.Edges.Add(edge);
            if (e.HasLabel) labelled.Add((e, edge));
        }
        var settings = new SugiyamaLayoutSettings { LayerSeparation = LoopLayerSeparation, NodeSeparation = LoopNodeSeparation };
        // A box whose only line goes out to one other box (Rules vs learning and Train a model, to Learning) sits beside
        // that box in its row rather than in a row of its own above it: the first such box on the left, the next on the right.
        foreach (var target in nodes.Values.Where(t => t.InEdges.Any()).ToList())
        {
            var leaves = target.InEdges.Select(e => e.Source).Where(src => !src.InEdges.Any() && src.OutEdges.Count() == 1).ToList();
            if (leaves.Count == 0) continue;
            var row = new List<Node>(leaves.Take(1)) { target };
            row.AddRange(leaves.Skip(1).Take(1));
            settings.AddSameLayerNeighbors(row);
        }
        new LayeredLayout(graph, settings).Run();

        var centres = nodes.Where(kv => kv.Key != GroupKey).ToDictionary(kv => kv.Key, kv => kv.Value.Center, StringComparer.Ordinal);
        var taken = nodes.Values.Select(n => n.BoundingBox).ToList();
        // The layered layout's own lines count as taken too, so the boxes placed below do not block their way (such as
        // the way back from Next character to Your text, round the side).
        foreach (var edge in graph.Edges.Where(e => e.Curve != null))
            taken.AddRange(Sample(edge.Curve).Select(p => new Rectangle(p)));
        // Labels: where the layered layout put them, unless that touches a box or an earlier label; then the nearest
        // clear point along the edge's own line (from its middle outwards).
        var labelBoxes = new List<Rectangle>();
        foreach (var (layout, edge) in labelled)
        {
            if (edge.Label == null) continue;
            var size = new Size(layout.LabelWidth, layout.LabelHeight);
            var candidates = new List<Point> { edge.Label.Center };
            if (edge.Curve != null)
                foreach (double t in new[] { 0.5, 0.4, 0.6, 0.3, 0.7, 0.2, 0.8 })
                    candidates.Add(edge.Curve[edge.Curve.ParStart + t * (edge.Curve.ParEnd - edge.Curve.ParStart)]);
            bool Clear(Point c)
            {
                var r = new Rectangle(new Size(size.Width + 2 * LabelGap, size.Height + 2 * LabelGap), c);
                return !nodes.Values.Any(n => n.BoundingBox.Intersects(r)) && !labelBoxes.Any(l => l.Intersects(r));
            }
            var at = candidates.FirstOrDefault(Clear, candidates[0]);
            labels[(layout.From, layout.To)] = at;
            labelBoxes.Add(new Rectangle(size, at));
        }
        taken.AddRange(labelBoxes);
        if (inner != null)
        {
            var g = nodes[GroupKey].Center;
            foreach (var (id, offset) in inner) centres[id] = g + offset;
            groupBox = new Rectangle(new Size(frameSize.Width, frameSize.Height), g);
        }

        // 3. The whole-loop column: beside the group (or the layers), top-aligned with it, moved right until it is clear.
        if (toLoop.Count > 0)
        {
            var anchor = groupBox ?? new Rectangle(taken);
            double colWidth = toLoop.Max(x => x.Width);
            double colHeight = toLoop.Sum(x => x.Height) + (toLoop.Count - 1) * BoxGap;
            double left = anchor.Right + 2 * BoxGap, top = anchor.Top;
            Rectangle Column() => new(left, top - colHeight, left + colWidth, top);
            for (var hit = Clash(Column(), taken); hit != null; hit = Clash(Column(), taken)) left = hit.Value.Right + 2 * BoxGap + 1;
            double y = top;
            foreach (var x in toLoop)
            {
                centres[x.Id] = new Point(left + colWidth / 2, y - x.Height / 2);
                taken.Add(new Rectangle(new Size(x.Width, x.Height), centres[x.Id]));
                y -= x.Height + BoxGap;
            }
        }

        // 4. Boxes with no lines: the first in the top right corner, the others in the bottom right corner (stacked
        //    upwards). Each goes as near its corner as it can be clear of every box and line (moving down from the top,
        //    or up from the bottom, at most half the map's height); failing that, the same on the left; failing that,
        //    just outside the map on the right.
        if (loose.Count > 0)
        {
            var all = new Rectangle(taken);
            for (int i = 0; i < loose.Count; i++)
            {
                var x = loose[i];
                bool fromTop = i == 0;
                double y0 = fromTop ? all.Top - x.Height / 2 : all.Bottom + x.Height / 2 + (loose.Count - 1 - i) * (x.Height + BoxGap);
                Point? spot = null;
                foreach (double cx in new[] { all.Right - x.Width / 2, all.Left + x.Width / 2 })
                    for (double moved = 0; moved <= all.Height / 2 && spot == null; moved += CornerStep)
                    {
                        var at = new Point(cx, fromTop ? y0 - moved : y0 + moved);
                        if (Clash(new Rectangle(new Size(x.Width, x.Height), at), taken) == null) spot = at;
                    }
                var place = spot ?? new Point(all.Right + 2 * BoxGap + x.Width / 2, y0);
                centres[x.Id] = place;
                taken.Add(new Rectangle(new Size(x.Width, x.Height), place));
            }
        }
        return centres;
    }

    /// <summary>
    /// Tree: a top-down layered (Sugiyama) layout of parent → child edges. Each parent's children are passed to
    /// <c>AddSameLayerNeighbors</c> in the given order, so siblings sit left to right in that order.
    /// </summary>
    private static Dictionary<string, Point> PlaceTree(GraphInput input)
    {
        var graph = new GeometryGraph();
        var nodes = AddNodes(graph, input.Nodes);
        var seen = new HashSet<(string, string)>();
        var kidsOf = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in input.PlacementEdges)
        {
            if (e.From == e.To || !nodes.ContainsKey(e.From) || !nodes.ContainsKey(e.To) || !seen.Add((e.From, e.To))) continue;
            graph.Edges.Add(new Edge(nodes[e.From], nodes[e.To]));
            if (!kidsOf.TryGetValue(e.From, out var list)) kidsOf[e.From] = list = [];
            list.Add(e.To);
        }
        var settings = new SugiyamaLayoutSettings { LayerSeparation = TreeLayerSeparation, NodeSeparation = TreeNodeSeparation };
        foreach (var kids in kidsOf.Values.Where(k => k.Count > 1))
            settings.AddSameLayerNeighbors(kids.Select(k => nodes[k]).ToList());
        new LayeredLayout(graph, settings).Run();
        return nodes.ToDictionary(kv => kv.Key, kv => kv.Value.Center, StringComparer.Ordinal);
    }

    /// <summary>The first box in <paramref name="taken"/> that comes within 2 × <see cref="BoxGap"/> of <paramref name="r"/>.</summary>
    private static Rectangle? Clash(Rectangle r, IEnumerable<Rectangle> taken)
    {
        var grown = new Rectangle(r.Left - 2 * BoxGap, r.Bottom - 2 * BoxGap, r.Right + 2 * BoxGap, r.Top + 2 * BoxGap);
        foreach (var t in taken)
            if (t.Intersects(grown)) return t;
        return null;
    }

    // ---- Routing ---------------------------------------------------------------------------------------------------

    private static GraphLayoutResult Route(GraphInput input, Dictionary<string, Point> centres, Rectangle? frame,
        Dictionary<(string, string), Point> labels)
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
        // A line with a label is routed in two parts through a box the size of its label, which also keeps every other
        // line off the label.
        var edges = new List<(LayoutEdge Layout, Edge[] Parts, Node? Label)>();
        var seen = new HashSet<(string, string)>();
        foreach (var e in input.PlacementEdges.Concat(input.RoutedOnlyEdges))
        {
            if (End(e.From) is not { } from || End(e.To) is not { } to || !seen.Add((e.From, e.To))) continue;
            Edge[] parts;
            Node? labelNode = null;
            if (labels.TryGetValue((e.From, e.To), out var at))
            {
                labelNode = new Node(CurveFactory.CreateRectangle(e.LabelWidth, e.LabelHeight, at), e);
                graph.Nodes.Add(labelNode);
                parts = [new Edge(from, labelNode), new Edge(labelNode, to)];
            }
            else parts = [new Edge(from, to)];
            foreach (var part in parts) graph.Edges.Add(part);
            edges.Add((e, parts, labelNode));
        }
        graph.UpdateBoundingBox();
        new SplineRouter(graph, new EdgeRoutingSettings { EdgeRoutingMode = EdgeRoutingMode.Spline, Padding = RoutePadding }).Run();

        static List<Point> Line(Edge e) => Sample(e.Curve ?? new LineSegment(e.Source.Center, e.Target.Center));
        var sampled = edges.Select(x => (x.Layout, Points: x.Parts.SelectMany(Line).ToList(), x.Label)).ToList();
        var bounds = new Rectangle(nodes.Values.Select(n => n.BoundingBox));
        if (frame is { } fr) bounds.Add(fr);
        foreach (var x in sampled)
            if (x.Label != null) bounds.Add(x.Label.BoundingBox);
        foreach (var x in sampled)
            foreach (var p in x.Points) bounds.Add(p);

        // MSAGL's y axis points up; the map's points down.
        double left = bounds.Left - Margin, top = bounds.Top + Margin;
        (double X, double Y) ToMap(Point p) => (p.X - left, top - p.Y);
        var placed = input.Nodes.Select(n =>
        {
            var b = nodes[n.Id].BoundingBox;
            return new PlacedNode(n.Id, b.Left - left, top - b.Top, n.Width, n.Height);
        }).ToList();
        var routed = sampled.Select(s => new RoutedEdge(s.Layout.From, s.Layout.To, s.Points.Select(ToMap).ToList(),
            s.Label == null ? null : ToMap(s.Label.Center))).ToList();
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
