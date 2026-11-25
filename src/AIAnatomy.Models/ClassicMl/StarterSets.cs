namespace AIAnatomy.Models.ClassicMl;

/// <summary>
/// Five fixed point sets on a 0 to 10 plot (no randomness). Groups are a centre plus a fixed list of offsets,
/// rounded to one decimal. "Two groups with odd points" is the default: the two groups plus three points that sit
/// in the other group's area, so a deep tree or k = 1 carves round them and a shallow tree or larger k does not.
/// </summary>
public static class StarterSets
{
    // Twelve distinct offsets, so no two points of a group share an x or a y.
    private static readonly (double Dx, double Dy)[] Offsets =
    [
        (-1.2, -0.9), (0.7, -1.3), (1.3, 0.6), (-0.5, 1.2), (0.2, 0.1), (-1.4, 0.4),
        (1.0, -0.3), (-0.8, -0.2), (0.5, 0.9), (-0.2, -0.6), (1.5, -1.1), (-1.0, 1.4),
    ];

    private static IEnumerable<LabeledPoint> Group(double cx, double cy, int label, int count, int shift) =>
        Enumerable.Range(0, count).Select(i =>
        {
            var (dx, dy) = Offsets[(i + shift) % Offsets.Length];
            return new LabeledPoint(Math.Round(cx + dx, 1), Math.Round(cy + dy, 1), label);
        });

    private static IReadOnlyList<LabeledPoint> TwoGroups() =>
        [.. Group(2.5, 2.5, 0, 12, 0), .. Group(7.5, 7.5, 1, 12, 5)];

    private static IReadOnlyList<LabeledPoint> TwoGroupsWithOddPoints() =>
    [
        .. TwoGroups(),
        new LabeledPoint(2.6, 2.9, 1),   // two of class 1 in class 0's area
        new LabeledPoint(3.3, 2.0, 1),
        new LabeledPoint(7.8, 7.9, 0),   // one of class 0 in class 1's area
    ];

    private static IReadOnlyList<LabeledPoint> Corners() =>
    [
        .. Group(2.5, 2.5, 0, 7, 0), .. Group(7.5, 7.5, 0, 7, 3),
        .. Group(2.5, 7.5, 1, 7, 6), .. Group(7.5, 2.5, 1, 7, 9),
    ];

    private static IReadOnlyList<LabeledPoint> Diagonal()
    {
        var points = new List<LabeledPoint>();
        for (int i = 0; i < 15; i++)
        {
            double x = 1.5 + 0.5 * i, gap = 0.5 + 0.3 * (i % 3);
            points.Add(new LabeledPoint(Math.Round(x, 2), Math.Round(x - gap, 2), 0));          // below the line y = x
            points.Add(new LabeledPoint(Math.Round(x + 0.25, 2), Math.Round(x + 0.25 + gap, 2), 1));   // above it
        }
        return points;
    }

    private static IReadOnlyList<LabeledPoint> ThreeGroups() =>
    [
        .. Group(2.0, 2.0, 0, 9, 0), .. Group(8.0, 3.0, 1, 9, 4), .. Group(5.0, 8.0, 2, 9, 8),
    ];

    public static IReadOnlyList<(string Name, IReadOnlyList<LabeledPoint> Points)> All { get; } =
    [
        ("Two groups", TwoGroups()),
        ("Two groups with odd points", TwoGroupsWithOddPoints()),
        ("Corners (XOR)", Corners()),
        ("Diagonal", Diagonal()),
        ("Three groups", ThreeGroups()),
    ];
}
