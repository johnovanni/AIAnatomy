namespace AIAnatomy.Presentation.IdeaMap;

/// <summary>The two idea-map layouts: the contents as a tree round a centre, or the path text takes through the model.</summary>
public enum MapMode { Radial, Loop }

/// <summary>A box to place, with its size in px.</summary>
public sealed record LayoutNode(string Id, double Width, double Height);

/// <summary>A line between two boxes. In <see cref="MapMode.Loop"/>, an end equal to
/// <see cref="Lessons.ConnectionList.WholeLoop"/> means the group's frame rather than a box. A label size (px) above
/// zero asks for room for a label on the line (Loop placement edges only).</summary>
public sealed record LayoutEdge(string From, string To, double LabelWidth = 0, double LabelHeight = 0)
{
    public bool HasLabel => LabelWidth > 0 && LabelHeight > 0;
}

/// <summary>What to lay out. <see cref="PlacementEdges"/> decide where the boxes go; <see cref="RoutedOnlyEdges"/> are
/// routed after the boxes are placed, round them. <see cref="CentreId"/> is the box kept in the middle (Radial);
/// <see cref="GroupMembers"/> are the boxes the group frame is drawn round (Loop).</summary>
public sealed record GraphInput(
    MapMode Mode,
    IReadOnlyList<LayoutNode> Nodes,
    IReadOnlyList<LayoutEdge> PlacementEdges,
    IReadOnlyList<LayoutEdge> RoutedOnlyEdges,
    string? CentreId,
    IReadOnlyList<string> GroupMembers);

/// <summary>A placed box; X and Y are its top-left corner in px, y downwards.</summary>
public sealed record PlacedNode(string Id, double X, double Y, double Width, double Height);

/// <summary>A routed line as a polyline from the <see cref="From"/> box's edge to the <see cref="To"/> box's edge
/// (or the group frame). For a line with a label, <see cref="LabelCentre"/> is where the label goes: the line passes
/// through it, and no box, frame border, other label or other line is under a label of the size asked for.</summary>
public sealed record RoutedEdge(string From, string To, IReadOnlyList<(double X, double Y)> Points,
    (double X, double Y)? LabelCentre = null);

/// <summary>Where everything goes. All coordinates are within (0, 0) to (<see cref="Width"/>, <see cref="Height"/>).
/// <see cref="Group"/> is the group frame (top-left and size), or null when there is no group.</summary>
public sealed record GraphLayoutResult(
    IReadOnlyList<PlacedNode> Nodes,
    IReadOnlyList<RoutedEdge> Edges,
    double Width,
    double Height,
    (double X, double Y, double Width, double Height)? Group);
