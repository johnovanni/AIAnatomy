using System.Windows;
using AIAnatomy.App.Controls;

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

    [Fact]
    public void AWide8By8Map_WritesThreeDecimals_At13And24()
    {
        RunOnSta(() =>
        {
            Assert.Equal(3, LaidOut(13, 900).CellDecimals);
            Assert.Equal(3, LaidOut(24, 1000).CellDecimals);
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
