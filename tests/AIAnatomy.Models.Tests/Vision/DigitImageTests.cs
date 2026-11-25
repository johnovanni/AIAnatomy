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
    public void StrokesWithNoLength_AreInk_NotNull()
    {
        // A click without a drag, or a stroke whose points are all the same, is a dot: the pad shows it, so the image must too.
        Assert.NotNull(DigitImage.FromStrokes(Strokes([(0.5, 0.5)]), Size));
        Assert.NotNull(DigitImage.FromStrokes(Strokes([(0.3, 0.4), (0.3, 0.4)], [(0.6, 0.6)]), Size));
    }

    /// <summary>"A small centred blob": at most 6 inked squares, at most 3 by 2, total ink under 2.5, centre of mass 3..4 both ways.</summary>
    private static void AssertSmallCentredBlob(double[]? image)
    {
        Assert.NotNull(image);
        var ink = Measure(image);
        Assert.True(ink.NonZero <= 6, $"{ink.NonZero} squares have ink.");
        Assert.True(ink.Height <= 2 && ink.Width <= 3, $"The blob is {ink.Width} by {ink.Height} squares.");
        Assert.True(image.Sum() < 2.5, $"Total ink {image.Sum():F2}.");
        Assert.InRange(ink.CentreX, 3, 4);
        Assert.InRange(ink.CentreY, 3, 4);
    }

    [Fact]
    public void AOnePointStroke_GivesASmallCentredBlob()
    {
        // What the pad records for a press and release without moving (it skips moves under 0.004 pad units).
        AssertSmallCentredBlob(DigitImage.FromStrokes(Strokes([(0.2, 0.7)]), Size));
        AssertSmallCentredBlob(DigitImage.FromStrokes(Strokes([(0.6, 0.3), (0.6, 0.3), (0.6, 0.3)]), Size));
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
    public void ATinyStroke_GivesASmallCentredBlob_NotABlownUpSquare() =>
        AssertSmallCentredBlob(DigitImage.FromStrokes(Strokes([(0.5, 0.5), (0.503, 0.501)]), Size));

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
    public void TheDefaultSize_IsEight()
    {
        var image = DigitImage.FromStrokes(Strokes([(0.3, 0.2), (0.3, 0.7)]));
        Assert.NotNull(image);
        Assert.Equal(Size * Size, image.Length);
        Assert.Equal(DigitImage.FromStrokes(Strokes([(0.3, 0.2), (0.3, 0.7)]), Size), image);
    }

    [Fact]
    public void ImageSizesOtherThanEight_AreNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => DigitImage.FromStrokes(Strokes([(0, 0), (1, 1)]), 28));
        Assert.Throws<NotSupportedException>(() => DigitImage.FromStrokes(Strokes([(0, 0), (1, 1)]), 16));
    }

    [Fact]
    public void StrokesPartlyOutsideThePad_GiveTheSameImageAsInside()
    {
        // Sensible: only the shape matters, not where it is, so a "0" hanging off the pad's top-right corner gives exactly
        // the image of the same "0" in the middle (the pad itself clamps to 0..1; other callers may not).
        var inside = DigitImage.FromStrokes(Strokes(Ellipse(0.5, 0.5, 0.22, 0.35)), Size);
        var outside = DigitImage.FromStrokes(Strokes(Ellipse(0.95, -0.1, 0.22, 0.35)), Size);
        Assert.NotNull(outside);
        Assert.Equal(inside, outside);
        var ink = Measure(outside);
        Assert.Equal(Size, ink.Height);
        Assert.InRange(ink.CentreX, 3.4, 3.6);
    }

    [Fact]
    public void AStrokeAlongThePadsEdge_IsCentredLikeOneInTheMiddle()
    {
        // Sensible: a line down the pad's left edge is the same "1" as a line down the middle: full height, centred across.
        var edge = DigitImage.FromStrokes(Strokes([(0, 0), (0, 1)]), Size);
        var middle = DigitImage.FromStrokes(Strokes([(0.5, 0), (0.5, 1)]), Size);
        Assert.NotNull(edge);
        Assert.Equal(middle, edge);
        var ink = Measure(edge);
        Assert.Equal(0, ink.Top);
        Assert.Equal(Size - 1, ink.Bottom);
        Assert.InRange(ink.CentreX, 3.4, 3.6);
    }

    [Fact]
    public void AMarkSmallerThanMinMark_IsNotBlownUpToFullSize()
    {
        // A "0" 0.04 by 0.06 pad units (under MinMark 0.1). Sensible: drawn at its size within a MinMark box, so it is centred
        // but does not reach the top or bottom rows, and has less ink than the same "0" drawn full size.
        var tiny = DigitImage.FromStrokes(Strokes(Ellipse(0.3, 0.3, 0.02, 0.03)), Size);
        var full = DigitImage.FromStrokes(Strokes(Ellipse(0.5, 0.5, 0.22, 0.35)), Size);
        Assert.NotNull(tiny);
        Assert.NotNull(full);
        var ink = Measure(tiny);
        Assert.True(ink.Top > 0 && ink.Bottom < Size - 1, $"The mark spans rows {ink.Top} to {ink.Bottom}.");
        Assert.True(tiny.Sum() < full.Sum(), $"Tiny ink {tiny.Sum():F2}, full-size ink {full.Sum():F2}.");
        Assert.InRange(ink.CentreX, 3.4, 3.6);
        Assert.InRange(ink.CentreY, 3.4, 3.6);
    }

    [Fact]
    public void AScribbleOverTheWholePad_FillsTheBox_WithValuesInRange()
    {
        // A zigzag across the whole pad, eleven times. Sensible: it fills the same 20-by-32 box as any digit (all 8 rows,
        // columns 1 to 6 only), is centred, its values stay in 0..1, and most of the box is inked.
        var zigzag = new List<(double X, double Y)>();
        for (int i = 0; i <= 10; i++) { zigzag.Add((0, i / 10.0)); zigzag.Add((1, i / 10.0 + 0.05)); }
        var image = DigitImage.FromStrokes(Strokes(zigzag.ToArray()), Size);
        Assert.NotNull(image);
        Assert.All(image, v => Assert.InRange(v, 0.0, 1.0));
        var ink = Measure(image);
        Assert.Equal(Size, ink.Height);
        Assert.True(ink.Width <= 6 && image.Where((_, i) => i % Size is 0 or Size - 1).All(v => v == 0), "Ink reaches the outer columns.");
        Assert.InRange(ink.CentreX, 3.3, 3.7);
        Assert.InRange(ink.CentreY, 3.3, 3.7);
        Assert.True(ink.Mean > 0.5, $"Mean ink {ink.Mean:F3}.");
    }

    [Fact]
    public void WideDrawings_ThePenTurnsTallAfterScaling()
    {
        // The pen is round in pad units; the drawing is then scaled separately across (to 20 pixels) and down (to 32), so on a
        // wide, flat drawing the pen becomes taller than it is wide, as when NIST scaled already-written ink. Intended.
        // A horizontal bar 0.8 long: a band centred down and across, 20 pixels wide (columns 1 to 6), much thicker than a
        // line (rows 1 to 6), but not reaching the top or bottom rows (its box is at least MinMark tall).
        var bar = DigitImage.FromStrokes(Strokes([(0.1, 0.5), (0.9, 0.5)]), Size);
        Assert.NotNull(bar);
        var ink = Measure(bar);
        Assert.Equal((1, 6), (ink.Top, ink.Bottom));
        Assert.Equal(6, ink.Width);
        Assert.InRange(ink.CentreX, 3.4, 3.6);
        Assert.InRange(ink.CentreY, 3.4, 3.6);

        // A flat ellipse (0.8 by 0.2): stretched to the full height like a "0", its top and bottom strokes thick (row 1's
        // middle squares are full) and its sides thin (the middle rows' centre squares have less ink than those).
        var flat = DigitImage.FromStrokes(Strokes(Ellipse(0.5, 0.5, 0.4, 0.1)), Size);
        Assert.NotNull(flat);
        var flatInk = Measure(flat);
        Assert.Equal((0, Size - 1), (flatInk.Top, flatInk.Bottom));
        Assert.Equal(1.0, flat[1 * Size + 3]);
        Assert.True(flat[3 * Size + 3] < flat[1 * Size + 3] && flat[4 * Size + 4] < flat[6 * Size + 4],
            "The ring's middle is as inked as its top and bottom.");
        Assert.InRange(flatInk.CentreX, 3.4, 3.6);
        Assert.InRange(flatInk.CentreY, 3.4, 3.6);
    }

    [Fact]
    public void TheTrainingSet_HasTheFactsTheRuleIsBuiltOn()
    {
        // Measured 2026-11-25 (docs/decisions.md, Task 8), reproduced here from the bundled training file.
        var train = BundledDigits.Train;
        var all = Enumerable.Range(0, train.Count).Select(i => Measure(train.Pixels(i))).ToList();
        Assert.Equal(3760, all.Count);
        Assert.All(all, ink => Assert.Equal((0, Size - 1), (ink.Top, ink.Bottom)));   // every digit touches the top and bottom rows
        Assert.Equal(6, all.Select(ink => ink.Width).Order().ElementAt(all.Count / 2));   // median ink width: 6 columns
        Assert.InRange(all.Average(ink => ink.Mean), 0.30, 0.32);   // mean ink about 0.31 (measured 0.308)
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
