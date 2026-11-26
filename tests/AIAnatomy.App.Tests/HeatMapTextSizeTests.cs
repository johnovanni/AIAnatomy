using System.Windows;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Tests;

/// <summary>
/// The numbers written inside a heat map's squares, and the readout under it, are read as body text: at least the body
/// text size and never under 12 DIPs (Task 13 review, Important 1). When a square is too narrow, the numbers lose decimals
/// (3, then 2, then 1) and then are left out (the readout still gives every value); the text never gets smaller.
/// </summary>
public class HeatMapTextSizeTests
{
    private static double[,] Grid(int rows, int cols, double value)
    {
        var v = new double[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++) v[r, c] = value;
        return v;
    }

    /// <summary>A heat map at the given text size, laid out at the given width (8 by 8 of −0.5, the widest number).</summary>
    private static HeatMap LaidOut(double fontSize, double width, int size = 8)
    {
        var map = new HeatMap { FontSize = fontSize, Values = Grid(size, size, -0.5) };
        map.Measure(new Size(width, double.PositiveInfinity));
        map.Arrange(new Rect(0, 0, width, map.DesiredSize.Height));
        return map;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(18)]
    [InlineData(24)]
    public void NumbersInSquaresAndTheReadout_AreAtLeastTheBodySize_AndNeverUnder12(double fontSize)
    {
        RunOnSta(() =>
        {
            var map = LaidOut(fontSize, 1000);
            Assert.True(map.ValueTextSize >= fontSize, $"value text {map.ValueTextSize} at body {fontSize}");
            Assert.True(map.ValueTextSize >= 12, $"value text {map.ValueTextSize}");
            Assert.True(map.ReadoutTextSize >= fontSize && map.ReadoutTextSize >= 12, $"readout {map.ReadoutTextSize} at body {fontSize}");
        });
    }

    // Body sizes in DIPs, as the app sets them from points (1 pt = 96/72 DIP).
    private const double Body13 = 13 * 96.0 / 72.0, Body24 = 24 * 96.0 / 72.0;

    /// <summary>A heat map of these values at the given text size (DIPs), laid out at the given width.</summary>
    private static HeatMap LaidOut(double[,] values, double fontSize, double width, HeatMapCellFormat format = HeatMapCellFormat.Decimal)
    {
        var map = new HeatMap { FontSize = fontSize, Values = values, CellFormat = format };
        map.Measure(new Size(width, double.PositiveInfinity));
        map.Arrange(new Rect(0, 0, width, map.DesiredSize.Height));
        return map;
    }

    /// <summary>An 8 by 8 map like lesson 4.6's feature maps: mixed signs, three-decimal values such as −0.860.</summary>
    private static double[,] FeatureMap(double scale = 1)
    {
        var v = new double[8, 8];
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++) v[r, c] = scale * ((r * 8 + c) % 2 == 0 ? -0.860 : 0.253 + 0.01 * c);
        return v;
    }

    // Final review I3 / AA-036 (ruling 1): 8 by 8 maps keep three decimals at 13 pt and drop to two at 24 pt in a
    // 900-pixel pane, so two numbers never run together ("−0.860−0.253" read as one number at 24 pt).
    [Fact]
    public void An8By8FeatureMap_HasThreeDecimalsAt13_AndTwoAt24()
    {
        RunOnSta(() =>
        {
            Assert.Equal(3, LaidOut(FeatureMap(), Body13, 900).CellDecimals);
            Assert.Equal(2, LaidOut(FeatureMap(), Body24, 900).CellDecimals);
        });
    }

    // Ruling 1: whatever the values and the width, written numbers stay inside their squares and keep a gap of at least
    // 0.4 em from their neighbours; the text never shrinks.
    [Theory]
    [InlineData(1.0)]      // −0.860 and 0.253: lesson 4.6 with its preset filters
    [InlineData(29.8)]     // up to about ±27: weights typed up to ±10 (−25.628)
    [InlineData(0.0001)]   // tiny values: fixed-point "0.000", not 1.2e−4
    public void WrittenNumbers_FitTheirSquares_WithAGapThatGrowsWithTheFont(double scale)
    {
        RunOnSta(() =>
        {
            foreach (double size in new[] { Body13, Body24 })
            for (double width = 1100; width >= 60; width -= 20)
            {
                var map = LaidOut(FeatureMap(scale), size, width);
                Assert.Equal(Math.Max(12, size), map.ValueTextSize);
                if (map.CellDecimals < 0) continue;
                Assert.True(map.WidestCellNumber <= map.CellSize, $"widest {map.WidestCellNumber} in {map.CellSize} at {width}");
                Assert.True(map.NarrowestNumberGap >= 0.4 * map.ValueTextSize - 1e-9,
                    $"gap {map.NarrowestNumberGap} at {width}, size {size}, scale {scale}");
            }
        });
    }

    [Fact]
    public void TinyValues_AreWrittenFixedPoint_NotInENotation()
    {
        RunOnSta(() =>
        {
            var map = LaidOut(Grid(8, 8, 0.00012), Body13, 900);
            Assert.Equal(3, map.CellDecimals);
            Assert.Equal("0.000", map.CellNumberText(0, 0));
        });
    }

    // Final review I3(b): a map with no negative numbers does not keep room for a minus sign.
    [Fact]
    public void AMapWithNoNegatives_KeepsAtLeastAsManyDecimals_AndMoreAtSomeWidth()
    {
        RunOnSta(() =>
        {
            bool more = false;
            for (double width = 1000; width >= 60; width -= 10)
            {
                int positive = LaidOut(Grid(8, 8, 0.5), Body13, width).CellDecimals;
                int negative = LaidOut(Grid(8, 8, -0.5), Body13, width).CellDecimals;
                Assert.True(positive >= negative, $"at {width}: {positive} vs {negative}");
                more |= positive > negative;
            }
            Assert.True(more);
        });
    }

    /// <summary>A causal attention map: row i spreads its weight over columns 0..i (row 0 is all on itself); the rest blocked.</summary>
    private static double[,] Attention(int n)
    {
        var v = new double[n, n];
        for (int r = 0; r < n; r++)
        {
            double total = 0;
            for (int c = 0; c <= r; c++) total += v[r, c] = 1.0 / (1 + (r - c) * (r - c));
            for (int c = 0; c < n; c++) v[r, c] = c <= r ? v[r, c] / total : double.NegativeInfinity;
        }
        return v;
    }

    // Ruling 2: attention weights are written as whole percentages ("35", "100"), which fit lesson 2.4's squares at 13 pt.
    [Fact]
    public void AttentionMap_WritesWholePercentages_At13()
    {
        RunOnSta(() =>
        {
            var map = LaidOut(Attention(19), Body13, 720, HeatMapCellFormat.Percent);
            Assert.Equal(0, map.CellDecimals);
            Assert.Equal("100", map.CellNumberText(0, 0));
            Assert.Null(map.CellNumberText(0, 1));   // blocked
            Assert.Equal(NumberText.WholePercent(map.Values![5, 3]), map.CellNumberText(5, 3));
            Assert.True(map.NarrowestNumberGap >= 0.4 * map.ValueTextSize);
        });
    }

    [Fact]
    public void NarrowerSquares_LoseDecimalsThenTheNumbers_ButNeverShrinkTheText()
    {
        RunOnSta(() =>
        {
            var seen = new List<int>();
            double size = LaidOut(13, 1000).ValueTextSize;
            for (double width = 1000; width >= 60; width -= 10)
            {
                var map = LaidOut(13, width);
                Assert.Equal(size, map.ValueTextSize);   // never smaller text
                if (seen.Count == 0 || seen[^1] != map.CellDecimals) seen.Add(map.CellDecimals);
            }
            // 3, then 2, then 1 decimals, then no numbers in the squares, in that order as the squares narrow.
            Assert.Equal([3, 2, 1, -1], seen);
        });
    }

    [Fact]
    public void ShowCellValuesOff_WritesNoNumbers()
    {
        RunOnSta(() =>
        {
            var map = new HeatMap { FontSize = 13, Values = Grid(8, 8, -0.5), ShowCellValues = false };
            map.Measure(new Size(900, double.PositiveInfinity));
            map.Arrange(new Rect(0, 0, 900, map.DesiredSize.Height));
            Assert.Equal(-1, map.CellDecimals);
        });
    }

    // WPF objects must be created on a single-threaded-apartment thread.
    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("The STA work failed: " + failure.Message, failure);
    }
}
