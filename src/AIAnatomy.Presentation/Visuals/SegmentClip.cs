namespace AIAnatomy.Presentation.Visuals;

/// <summary>Cuts a straight segment to a rectangle (Liang-Barsky), so charts never draw lines millions of pixels long.</summary>
public static class SegmentClip
{
    /// <summary>
    /// The part of the segment (x0, y0)-(x1, y1) inside the rectangle, or null when none of it is inside or a number is not finite.
    /// </summary>
    public static ((double X, double Y) From, (double X, double Y) To)? Clip(
        double x0, double y0, double x1, double y1, double xMin, double xMax, double yMin, double yMax)
    {
        if (!double.IsFinite(x0) || !double.IsFinite(y0) || !double.IsFinite(x1) || !double.IsFinite(y1)) return null;
        double dx = x1 - x0, dy = y1 - y0;
        double t0 = 0, t1 = 1;
        // Each edge: p is the direction towards the outside (negative = entering), q the distance to the edge.
        double[] p = [-dx, dx, -dy, dy];
        double[] q = [x0 - xMin, xMax - x0, y0 - yMin, yMax - y0];
        for (int i = 0; i < 4; i++)
        {
            if (p[i] == 0)
            {
                if (q[i] < 0) return null;   // parallel to this edge and outside it
                continue;
            }
            double r = q[i] / p[i];
            if (p[i] < 0) { if (r > t1) return null; t0 = Math.Max(t0, r); }
            else { if (r < t0) return null; t1 = Math.Min(t1, r); }
        }
        return ((x0 + t0 * dx, y0 + t0 * dy), (x0 + t1 * dx, y0 + t1 * dy));
    }

    public static bool Inside(double x, double y, double xMin, double xMax, double yMin, double yMax) =>
        x >= xMin && x <= xMax && y >= yMin && y <= yMax;
}
