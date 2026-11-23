namespace AIAnatomy.Presentation.Visuals;

/// <summary>A rectangle a label must not cover (another label, or a point's marker), in pixels, y downwards.</summary>
public readonly record struct LabelBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>The area this box shares with another (0 when they do not meet).</summary>
    public double Overlap(LabelBox other)
    {
        double w = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        double h = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return w > 0 && h > 0 ? w * h : 0;
    }
}

/// <summary>A line a label must not lie on (such as a point's arrow from the origin), in pixels.</summary>
public readonly record struct LabelLine(double X0, double Y0, double X1, double Y1);

/// <summary>
/// Picks where a label goes in a chart: the first candidate place that covers nothing, or else the one that covers least.
/// Covering a box counts its shared area; lying on a line counts the length of line under the label times the label's
/// height, so a label on top of a line is as bad as one on top of a box. Every place is first moved inside the area.
/// </summary>
public static class LabelPlacement
{
    public static (double X, double Y) Choose(IReadOnlyList<(double X, double Y)> candidates, double width, double height,
        double areaWidth, double areaHeight, IEnumerable<LabelBox> boxes, IEnumerable<LabelLine> lines)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) throw new ArgumentException("Give at least one place.", nameof(candidates));
        var boxList = boxes?.ToList() ?? throw new ArgumentNullException(nameof(boxes));
        var lineList = lines?.ToList() ?? throw new ArgumentNullException(nameof(lines));
        (double X, double Y) best = default;
        double bestCover = double.MaxValue;
        foreach (var (cx, cy) in candidates)
        {
            var box = new LabelBox(Math.Clamp(cx, 0, Math.Max(0, areaWidth - width)), Math.Clamp(cy, 0, Math.Max(0, areaHeight - height)), width, height);
            double cover = boxList.Sum(box.Overlap) + lineList.Sum(l => LengthInside(l, box) * height);
            if (cover < bestCover) { best = (box.X, box.Y); bestCover = cover; }
            if (cover == 0) break;
        }
        return best;
    }

    /// <summary>
    /// Where an arrow's label goes (top-left of a <paramref name="width"/> × <paramref name="height"/> plate). Places are
    /// tried at the middle of the arrow and at 0.35 and 0.65 of its length, each on the arrow and then moved sideways step
    /// by step on alternate sides; nearer the arrow comes first. The first place that touches no box wins; if none is
    /// clear, the one that covers boxes least is used, never simply the middle.
    /// </summary>
    public static (double X, double Y) ForArrow((double X, double Y) start, (double X, double Y) end, double width, double height,
        double areaWidth, double areaHeight, IEnumerable<LabelBox> boxes)
    {
        double ax = end.X - start.X, ay = end.Y - start.Y, length = Math.Sqrt(ax * ax + ay * ay);
        // Across the arrow, trying above (or right of) it first.
        double sx = length < 1e-9 ? 0 : ay / length, sy = length < 1e-9 ? -1 : -ax / length;
        if (sy > 0 || (sy == 0 && sx < 0)) { sx = -sx; sy = -sy; }
        double step = (Math.Abs(sx) * width + Math.Abs(sy) * height) * 0.75;   // most of the plate's size across the arrow
        var candidates = new List<(double X, double Y)>();
        foreach (int k in ArrowOffsets)
            foreach (double t in ArrowPlaces)
            {
                double cx = start.X + ax * t + sx * k * step, cy = start.Y + ay * t + sy * k * step;
                candidates.Add((cx - width / 2, cy - height / 2));
            }
        return Choose(candidates, width, height, areaWidth, areaHeight, boxes, []);
    }

    private static readonly int[] ArrowOffsets = [0, 1, -1, 2, -2, 3, -3];
    private static readonly double[] ArrowPlaces = [0.5, 0.35, 0.65];

    private static double LengthInside(LabelLine line, LabelBox box)
    {
        if (SegmentClip.Clip(line.X0, line.Y0, line.X1, line.Y1, box.X, box.Right, box.Y, box.Bottom) is not { } cut) return 0;
        double dx = cut.To.X - cut.From.X, dy = cut.To.Y - cut.From.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
