namespace AIAnatomy.Models.ClassicMl;

/// <summary>k-nearest-neighbours classification by Euclidean distance.</summary>
public static class NearestNeighbours
{
    /// <summary>
    /// The majority label among the <paramref name="k"/> nearest points (k clamped to 1 to n). Equal distances
    /// keep point order; a tied vote goes to the tied label whose point is nearest.
    /// </summary>
    public static int Classify(IReadOnlyList<LabeledPoint> points, double x, double y, int k)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0) throw new ArgumentException("There are no points to look at.", nameof(points));
        k = Math.Clamp(k, 1, points.Count);

        // OrderBy is stable, so equal distances stay in point order.
        var nearest = points
            .OrderBy(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y))
            .Take(k)
            .ToList();

        var votes = new Dictionary<int, int>();
        foreach (var p in nearest) votes[p.Label] = votes.GetValueOrDefault(p.Label) + 1;
        int most = votes.Values.Max();
        // The first of the nearest points whose label has the top count is the nearest of the tied labels.
        return nearest.First(p => votes[p.Label] == most).Label;
    }
}
