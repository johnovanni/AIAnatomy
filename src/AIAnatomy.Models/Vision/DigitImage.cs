namespace AIAnatomy.Models.Vision;

/// <summary>
/// Turns strokes drawn on the drawing pad into an image like the training digits (the UCI 8x8 optical digits), so the
/// network sees what it was trained on.
/// <para>
/// <b>How UCI made its digits.</b> UCI's page: NIST's preprocessing programs extracted normalised 32x32 bitmaps of the
/// handwritten digits, then "32x32 bitmaps are divided into nonoverlapping blocks of 4x4 and the number of on pixels are
/// counted in each block", giving 8x8 values 0..16. This class does the same: it draws the strokes onto a 32x32 binary
/// bitmap, counts each 4x4 block and divides by 16 (DigitSet gives the training pixels as v x 255 / 16, then 0..1).
/// </para>
/// <para>
/// <b>How the 32x32 bitmap is normalised: matched empirically.</b> UCI does not say how NIST sized and centred the digits,
/// and the NIST report (NISTIR 5469) could not be read to confirm it, so the rule is matched to the 3,760 bundled training
/// digits (measured 2026-11-25, Stage 2B Task 8): every one touches the top and bottom rows of the 8x8 grid; almost none
/// touch the left or right column (1.6% and 12.5%; the columns' mean ink is 0.000 and 0.006 against 0.49 to 0.61 in the
/// middle four); the ink's width is 6 squares in 76% of them (4 to 8 in all but two); a "1" is as wide and as inky as
/// the other digits (median 6 squares, mean ink 0.32), so narrow digits were widened; the mean ink is 0.31 (5th to 95th
/// percentile 0.255 to 0.368) with 33 squares inked (28 to 38). That fits a digit stretched to fill a box 20 pixels wide
/// and 32 tall, centred in the 32x32 bitmap.
/// </para>
/// <para>
/// <b>The rule.</b> The ink is the strokes' centre lines drawn with a pen that is round in pad units, whose width is
/// <see cref="PenFraction"/> of the drawing's larger side, so the pen scales with the drawing and a digit drawn small or
/// large gives the same image (the pad's visible line is only for show). The ink's box (centre lines plus the pen) is
/// scaled to 32 pixels tall and, separately, to 20 wide, but never stretched across by more than <see cref="MaxStretch"/> times the vertical
/// scale, so a straight "1" becomes a bar about 11 pixels wide (the training 1s are 4 to 6 squares wide) rather than a
/// solid block. The box's centre goes to the bitmap's centre. A pixel is on when its centre is within the pen of a
/// stroke. Because the two scales differ, the round pen becomes an ellipse in the bitmap: on a tall, narrow digit it is
/// wider than it is tall (a "1" becomes a wide bar), on a wide, flat drawing taller than it is wide. That is intended: it is
/// what scaling already-written ink does, as NIST scaled scanned digits written with a fixed pen. A one-point stroke (a
/// click without a drag) is ink too: a dot. Marks smaller than <see cref="MinMark"/> pad units are treated as if their box were that size, so a dot stays a
/// small blob in the middle instead of being blown up. The pen width and stretch limit were chosen so a drawn "0" and
/// "1" have the training digits' ink (0.31 and 0.31 mean, 32 squares inked) and are nearest their own digit's mean
/// training image.
/// </para>
/// <para>Strokes are in pad units: the drawing pad's square runs 0..1 each way, y downward. Only <see cref="MinMark"/>
/// depends on that.</para>
/// </summary>
public static class DigitImage
{
    /// <summary>The only image size supported: the UCI digits' 8x8.</summary>
    public const int SupportedSize = 8;

    /// <summary>The pen's width as a fraction of the drawing's larger side (matched to the training digits' ink).</summary>
    public const double PenFraction = 0.18;

    /// <summary>The most the drawing is stretched across, relative to the vertical scale.</summary>
    public const double MaxStretch = 2.25;

    /// <summary>The smallest box, in pad units, that a drawing is scaled from: smaller marks are not blown up.</summary>
    public const double MinMark = 0.1;

    private const int Bitmap = 32, Block = 4, InkHeight = 32, InkWidth = 20;

    /// <summary>
    /// The image (row by row, values 0..1 in steps of 1/16) for the strokes, or null when there is no ink (no points at
    /// all). A stroke with one point, or with all its points the same, is a dot. Only <paramref name="imageSize"/> 8 is
    /// supported (the default): the app's digits are UCI's 8x8, and the MNIST 28x28 method was not built
    /// (<see cref="NotSupportedException"/>).
    /// </summary>
    public static double[]? FromStrokes(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes, int imageSize = SupportedSize)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        if (imageSize != SupportedSize)
            throw new NotSupportedException($"Only {SupportedSize}x{SupportedSize} digit images are supported (the UCI digits), not {imageSize}x{imageSize}.");

        var lines = new List<IReadOnlyList<(double X, double Y)>>();
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        foreach (var stroke in strokes)
        {
            if (stroke is not { Count: > 0 }) continue;
            for (int i = 0; i < stroke.Count; i++)
            {
                var (x, y) = stroke[i];
                if (!double.IsFinite(x) || !double.IsFinite(y))
                    throw new ArgumentException("A stroke has a point that is not a finite number.", nameof(strokes));
                x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
                y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
            }
            lines.Add(stroke);
        }
        if (lines.Count == 0) return null;

        double width = x1 - x0, height = y1 - y0;
        double pen = PenFraction * Math.Max(MinMark, Math.Max(width, height));
        double radius = pen / 2;
        double scaleY = InkHeight / (Math.Max(height, MinMark) + pen);
        double scaleX = Math.Min(InkWidth / (Math.Max(width, MinMark * InkWidth / InkHeight) + pen), MaxStretch * scaleY);
        double centreX = (x0 + x1) / 2, centreY = (y0 + y1) / 2;

        var counts = new int[SupportedSize * SupportedSize];
        for (int row = 0; row < Bitmap; row++)
        {
            double y = centreY + (row + 0.5 - Bitmap / 2.0) / scaleY;
            for (int col = 0; col < Bitmap; col++)
            {
                double x = centreX + (col + 0.5 - Bitmap / 2.0) / scaleX;
                if (Inked(lines, x, y, radius)) counts[row / Block * SupportedSize + col / Block]++;
            }
        }
        return counts.Select(c => c / (double)(Block * Block)).ToArray();
    }

    /// <summary>Whether (x, y) is within the pen's radius of any stroke (a one-point stroke is a dot).</summary>
    private static bool Inked(List<IReadOnlyList<(double X, double Y)>> lines, double x, double y, double radius)
    {
        double r2 = radius * radius;
        foreach (var stroke in lines)
        {
            if (stroke.Count == 1 && Distance2(x, y, stroke[0], stroke[0]) <= r2) return true;
            for (int i = 1; i < stroke.Count; i++)
                if (Distance2(x, y, stroke[i - 1], stroke[i]) <= r2) return true;
        }
        return false;
    }

    /// <summary>The squared distance from (x, y) to the segment a-b.</summary>
    private static double Distance2(double x, double y, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, length2 = dx * dx + dy * dy;
        double t = length2 == 0 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / length2, 0, 1);
        double ex = x - a.X - t * dx, ey = y - a.Y - t * dy;
        return ex * ex + ey * ey;
    }
}
