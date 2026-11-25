using AIAnatomy.Models.Vision;

namespace AIAnatomy.Models.Tests.Vision;

public class DigitImageTests
{
    private const int Size = 8;

    private static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Strokes(params (double X, double Y)[][] strokes) => strokes;

    /// <summary>An ellipse as one closed stroke (a "0").</summary>
    private static (double X, double Y)[] Ellipse(double cx, double cy, double rx, double ry, int n = 48) =>
        Enumerable.Range(0, n + 1).Select(k => (cx + rx * Math.Cos(2 * Math.PI * k / n), cy + ry * Math.Sin(2 * Math.PI * k / n))).ToArray();

    /// <summary>A "2"-like digit (a hook, a diagonal and a base) inside the box (left, top, size).</summary>
    private static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Two(double left, double top, double size)
    {
        (double, double) P(double x, double y) => (left + x * size, top + y * size);
        var hook = Enumerable.Range(0, 13).Select(k =>
        {
            double a = Math.PI + 1.25 * Math.PI * k / 12;
            return P(0.5 + 0.28 * Math.Cos(a), 0.28 + 0.25 * Math.Sin(a));
        });
        return Strokes(hook.Concat([P(0.2, 1.0), P(0.85, 1.0)]).ToArray());
    }

    private sealed record Ink(int Height, int Width, int Top, int Bottom, double Mean, int NonZero, double CentreX, double CentreY);

    private static Ink Measure(double[] image)
    {
        var rows = Enumerable.Range(0, Size).Where(r => Enumerable.Range(0, Size).Any(c => image[r * Size + c] > 0)).ToList();
        var cols = Enumerable.Range(0, Size).Where(c => Enumerable.Range(0, Size).Any(r => image[r * Size + c] > 0)).ToList();
        double total = image.Sum();
        double cx = Enumerable.Range(0, image.Length).Sum(i => (i % Size) * image[i]) / total;
        double cy = Enumerable.Range(0, image.Length).Sum(i => (i / Size) * image[i]) / total;
        return new Ink(rows[^1] - rows[0] + 1, cols[^1] - cols[0] + 1, rows[0], rows[^1], total / image.Length,
            image.Count(v => v > 0), cx, cy);
    }

    [Fact]
    public void NoStrokes_GiveNull()
    {
        Assert.Null(DigitImage.FromStrokes([], Size));
        Assert.Null(DigitImage.FromStrokes(Strokes([]), Size));
    }

    [Fact]
    public void StrokesWithNoLength_GiveNull()
    {
        Assert.Null(DigitImage.FromStrokes(Strokes([(0.5, 0.5)]), Size));
        Assert.Null(DigitImage.FromStrokes(Strokes([(0.3, 0.4), (0.3, 0.4)], [(0.6, 0.6)]), Size));
    }

    [Fact]
    public void AVerticalLine_IsCentredAcross_AndFillsTheHeight()
    {
        var image = DigitImage.FromStrokes(Strokes([(0.3, 0.2), (0.3, 0.7)]), Size);
        Assert.NotNull(image);
        Assert.Equal(Size * Size, image.Length);
        var ink = Measure(image);
        // The training digits all touch the top and bottom rows (3,760 of 3,760).
        Assert.Equal(0, ink.Top);
        Assert.Equal(Size - 1, ink.Bottom);
        // Centred across: the drawing's box is centred, so a line's ink is symmetric about the middle (3.5).
        Assert.InRange(ink.CentreX, 3.4, 3.6);
        Assert.InRange(ink.CentreY, 3.4, 3.6);
        // Widened the way the training ones are (their 1s are 4 to 6 squares wide), but not to the full 6-square box.
        Assert.InRange(ink.Width, 2, 4);
    }

    [Fact]
    public void TheSameDigit_SmallInACorner_AndLargeInTheMiddle_GiveNearlyTheSameImage()
    {
        var small = DigitImage.FromStrokes(Two(0.02, 0.03, 0.25), Size);
        var large = DigitImage.FromStrokes(Two(0.1, 0.08, 0.8), Size);
        Assert.NotNull(small);
        Assert.NotNull(large);
        double meanDifference = small.Zip(large, (a, b) => Math.Abs(a - b)).Average();
        Assert.True(meanDifference < 0.05, $"Mean difference {meanDifference:F4} per pixel.");
    }

    [Fact]
    public void ADot_GivesASmallCentredBlob_NotABlownUpSquare()
    {
        var image = DigitImage.FromStrokes(Strokes([(0.5, 0.5), (0.503, 0.501)]), Size);
        Assert.NotNull(image);
        var ink = Measure(image);
        Assert.True(ink.NonZero <= 6, $"{ink.NonZero} squares have ink.");
        Assert.True(ink.Height <= 2 && ink.Width <= 3, $"The blob is {ink.Width} by {ink.Height} squares.");
        Assert.True(image.Sum() < 2.5, $"Total ink {image.Sum():F2}.");
        Assert.InRange(ink.CentreX, 3, 4);
        Assert.InRange(ink.CentreY, 3, 4);
    }

    [Fact]
    public void AllValues_AreBetweenZeroAndOne()
    {
        var random = new Random(7);
        for (int trial = 0; trial < 20; trial++)
        {
            var strokes = Enumerable.Range(0, random.Next(1, 4))
                .Select(_ => Enumerable.Range(0, random.Next(2, 30)).Select(_ => (random.NextDouble(), random.NextDouble())).ToArray())
                .ToArray();
            var image = DigitImage.FromStrokes(strokes, Size);
            Assert.NotNull(image);
            Assert.All(image, v => Assert.InRange(v, 0.0, 1.0));
            // Sums of 4x4 squares of a binary bitmap: multiples of 1/16.
            Assert.All(image, v => Assert.Equal(Math.Round(v * 16), v * 16, 9));
        }
    }

    [Fact]
    public void ImageSizesOtherThanEight_AreNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => DigitImage.FromStrokes(Strokes([(0, 0), (1, 1)])));
        Assert.Throws<NotSupportedException>(() => DigitImage.FromStrokes(Strokes([(0, 0), (1, 1)]), 16));
    }

    [Fact]
    public void NonFiniteCoordinates_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => DigitImage.FromStrokes(Strokes([(0, 0), (double.NaN, 1)]), Size));
    }

    public static TheoryData<string> DrawnDigits => new() { "0", "1" };

    private static (int Label, IReadOnlyList<IReadOnlyList<(double X, double Y)>> Strokes) Drawn(string name) => name switch
    {
        "0" => (0, Strokes(Ellipse(0.5, 0.5, 0.22, 0.35))),
        "1" => (1, Strokes([(0.5, 0.15), (0.5, 0.85)])),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static double Percentile(IEnumerable<double> values, double p)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted[(int)(p * (sorted.Length - 1))];
    }

    [Theory]
    [MemberData(nameof(DrawnDigits))]
    public void ADrawnDigit_HasInkLikeTheTrainingDigits(string name)
    {
        var (label, strokes) = Drawn(name);
        var image = DigitImage.FromStrokes(strokes, Size);
        Assert.NotNull(image);
        var ink = Measure(image);

        var train = BundledDigits.Train;
        var all = Enumerable.Range(0, train.Count).Select(i => Measure(train.Pixels(i))).ToList();
        Assert.Equal(Size, ink.Height);   // every training digit fills the height
        Assert.InRange(ink.Width, all.Min(s => s.Width), all.Max(s => s.Width));
        Assert.InRange(ink.Mean, Percentile(all.Select(s => s.Mean), 0.05), Percentile(all.Select(s => s.Mean), 0.95));
        Assert.InRange(ink.NonZero, Percentile(all.Select(s => (double)s.NonZero), 0.05), Percentile(all.Select(s => (double)s.NonZero), 0.95));

        // And it is closest to its own digit's mean training image.
        double Distance(int digit)
        {
            var indices = Enumerable.Range(0, train.Count).Where(i => train.Label(i) == digit).ToList();
            var mean = new double[Size * Size];
            foreach (int i in indices)
            {
                var px = train.Pixels(i);
                for (int j = 0; j < mean.Length; j++) mean[j] += px[j] / indices.Count;
            }
            return image.Zip(mean, (a, b) => Math.Abs(a - b)).Average();
        }
        var distances = Enumerable.Range(0, 10).Select(Distance).ToList();
        Assert.Equal(label, distances.IndexOf(distances.Min()));
    }
}
