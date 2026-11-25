namespace AIAnatomy.Models.Diffusion;

/// <summary>A drawn stroke turned into a shape: its points, or a message saying why it cannot be used.</summary>
public sealed record ShapeResult(IReadOnlyList<(double X, double Y)>? Points, string? Error);

/// <summary>
/// The shapes the diffusion network can learn: four presets and the user's own drawing, each 1,000 points inside the
/// square [-1, 1] x [-1, 1] (y up), evenly spaced along the curve. Also the sample-quality measure.
/// </summary>
public static class Shapes
{
    public const int PointCount = 1000;

    public const string TooShortMessage = "Draw a longer line: this one is too short to learn a shape from.";
    public const string TooSmallMessage = "Draw bigger: this shape is too small to learn from.";

    /// <summary>A stroke shorter than this fraction of the pad's size is refused.</summary>
    public const double MinLengthFraction = 0.25;

    /// <summary>A stroke whose bounding box is under this fraction of the pad on both axes is refused.</summary>
    public const double MinSizeFraction = 0.15;

    private const int DenseCount = 5000;   // points per preset curve before even resampling

    private static readonly Lazy<IReadOnlyList<(string Name, IReadOnlyList<(double X, double Y)> Points)>> LazyPresets = new(BuildPresets);

    /// <summary>"Spiral", "Circle", "Two moons" and "Letter S", in that order; each 1,000 points inside [-1, 1].</summary>
    public static IReadOnlyList<(string Name, IReadOnlyList<(double X, double Y)> Points)> Presets => LazyPresets.Value;

    /// <summary>
    /// Turns a stroke drawn on a square pad of side <paramref name="padSize"/> (pad coordinates: 0..padSize, y down) into a
    /// shape. Points outside the pad are moved onto its edge. The stroke is refused with <see cref="TooShortMessage"/> when
    /// it has fewer than 2 points or is shorter than 25% of the pad's size, and with <see cref="TooSmallMessage"/> when its
    /// bounding box is under 15% of the pad on both axes. Otherwise it is resampled to <see cref="PointCount"/> points
    /// evenly spaced along its length (both ends kept) and mapped into [-1, 1] with the pad's centre at 0 and y up.
    /// </summary>
    public static ShapeResult FromStroke(IReadOnlyList<(double X, double Y)> stroke, double padSize)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        if (!(padSize > 0) || double.IsInfinity(padSize)) throw new ArgumentOutOfRangeException(nameof(padSize), "The pad's size must be a positive number.");
        if (stroke.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            throw new ArgumentException("The stroke has a point that is not a number.", nameof(stroke));
        if (stroke.Count < 2) return new ShapeResult(null, TooShortMessage);

        var clamped = stroke.Select(p => (X: Math.Clamp(p.X, 0, padSize), Y: Math.Clamp(p.Y, 0, padSize))).ToList();
        if (Length(clamped) < MinLengthFraction * padSize) return new ShapeResult(null, TooShortMessage);
        double width = clamped.Max(p => p.X) - clamped.Min(p => p.X);
        double height = clamped.Max(p => p.Y) - clamped.Min(p => p.Y);
        if (width < MinSizeFraction * padSize && height < MinSizeFraction * padSize) return new ShapeResult(null, TooSmallMessage);

        double half = padSize / 2;
        var mapped = clamped.Select(p => ((p.X - half) / half, (half - p.Y) / half)).ToList();
        return new ShapeResult(Resample(mapped, PointCount, closed: false), null);
    }

    /// <summary>
    /// The sample-quality measure: the mean, over the samples, of the distance from each sample to the nearest point of
    /// the shape (lower is better; 0 when every sample lies on a shape point). It measures how close samples land to the
    /// shape, not whether they cover all of it.
    /// </summary>
    public static double MeanDistanceToShape(IReadOnlyList<(double X, double Y)> samples, IReadOnlyList<(double X, double Y)> shape)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(shape);
        if (samples.Count == 0) throw new ArgumentException("Give at least one sample.", nameof(samples));
        if (shape.Count == 0) throw new ArgumentException("The shape has no points.", nameof(shape));
        double total = 0;
        foreach (var s in samples)
        {
            double best = double.PositiveInfinity;
            foreach (var p in shape)
            {
                double dx = s.X - p.X, dy = s.Y - p.Y;
                double d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            total += Math.Sqrt(best);
        }
        return total / samples.Count;
    }

    private static IReadOnlyList<(string, IReadOnlyList<(double X, double Y)>)> BuildPresets()
    {
        // Spiral: one and a half turns, r = 0.15 + 0.75 * theta / (3 pi), so the arms are 0.5 apart.
        var spiral = Dense(u =>
        {
            double theta = 3 * Math.PI * u, r = 0.15 + 0.75 * u;
            return (r * Math.Cos(theta), r * Math.Sin(theta));
        });

        var circle = Dense(u => (0.8 * Math.Cos(2 * Math.PI * u), 0.8 * Math.Sin(2 * Math.PI * u)));

        // Two moons, as in scikit-learn's make_moons: (cos a, sin a) and (1 - cos a, 0.5 - sin a) for a in [0, pi],
        // centred and scaled by 0.6, so x is within [-0.9, 0.9] and y within [-0.45, 0.45]. Half the points on each.
        var upper = Dense(u => ((Math.Cos(Math.PI * u) - 0.5) * 0.6, (Math.Sin(Math.PI * u) - 0.25) * 0.6));
        var lower = Dense(u => ((1 - Math.Cos(Math.PI * u) - 0.5) * 0.6, (0.5 - Math.Sin(Math.PI * u) - 0.25) * 0.6));
        var moons = Resample(upper, PointCount / 2, closed: false).Concat(Resample(lower, PointCount - PointCount / 2, closed: false)).ToList();

        // Letter S: the upper circle (centre (0, 0.4), radius 0.4) from 20 degrees anticlockwise to its bottom, then the
        // lower circle (centre (0, -0.4)) from its top clockwise to 200 degrees; the two arcs meet smoothly at (0, 0).
        double start = 20 * Math.PI / 180, end = 270 * Math.PI / 180;
        var top = Dense(u => { double a = start + (end - start) * u; return (0.4 * Math.Cos(a), 0.4 + 0.4 * Math.Sin(a)); });
        var bottom = Dense(u => { double a = Math.PI / 2 - (end - start) * u; return (0.4 * Math.Cos(a), -0.4 + 0.4 * Math.Sin(a)); });
        var letterS = top.Concat(bottom.Skip(1)).ToList();

        return new (string, IReadOnlyList<(double X, double Y)>)[]
        {
            ("Spiral", Resample(spiral, PointCount, closed: false)),
            ("Circle", Resample(circle, PointCount, closed: true)),
            ("Two moons", moons),
            ("Letter S", Resample(letterS, PointCount, closed: false)),
        };
    }

    private static List<(double X, double Y)> Dense(Func<double, (double, double)> curve)
        => Enumerable.Range(0, DenseCount + 1).Select(i => curve((double)i / DenseCount)).ToList();

    private static double Length(IReadOnlyList<(double X, double Y)> line)
    {
        double total = 0;
        for (int i = 1; i < line.Count; i++) total += Distance(line[i - 1], line[i]);
        return total;
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// <paramref name="count"/> points evenly spaced by length along the polyline. Open: both ends kept, spacing L / (count - 1).
    /// Closed (the last point equals the first): spacing L / count, so the start is not repeated.
    /// </summary>
    private static IReadOnlyList<(double X, double Y)> Resample(IReadOnlyList<(double X, double Y)> line, int count, bool closed)
    {
        var cumulative = new double[line.Count];
        for (int i = 1; i < line.Count; i++) cumulative[i] = cumulative[i - 1] + Distance(line[i - 1], line[i]);
        double length = cumulative[^1];
        double spacing = length / (closed ? count : count - 1);
        var result = new List<(double X, double Y)>(count);
        int segment = 0;
        for (int k = 0; k < count; k++)
        {
            if (!closed && k == count - 1) { result.Add(line[^1]); break; }
            double s = k * spacing;
            while (segment < line.Count - 2 && cumulative[segment + 1] < s) segment++;
            double span = cumulative[segment + 1] - cumulative[segment];
            double f = span > 0 ? Math.Clamp((s - cumulative[segment]) / span, 0, 1) : 0;
            var (a, b) = (line[segment], line[segment + 1]);
            result.Add((a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f));
        }
        return result;
    }
}
