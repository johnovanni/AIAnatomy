using System.Windows;
using AIAnatomy.App.Controls;

namespace AIAnatomy.App.Tests;

/// <summary>
/// Lesson 4.9's plot (Task 16): at 24 pt in a narrow pane the legend's entries wrap instead of running past the plot's
/// edge, and a lab can ask for a larger square than the default 420 pixels, so the finished shape (−1 to 1 on axes from −3
/// to 3) is large enough to see.
/// </summary>
public class PointCloudPlotLayoutTests
{
    private static PointCloudPlot Plot(double fontSize) => new()
    {
        FontSize = fontSize,
        Points = [new Point(0, 0), new Point(0.5, -0.5)],
        TargetPoints = [new Point(0.8, 0), new Point(0, 0.8)],
        PointsLabel = "points of the cloud",
        TargetLabel = "shape being made",
    };

    [Theory]
    [InlineData(32, 340)]   // 24 pt in the narrowest lab pane (a 900-pixel window)
    [InlineData(32, 200)]
    [InlineData(17.33, 340)]
    public void TheLegend_StaysInsideTheWidth(double fontSize, double width)
    {
        RunOnSta(() =>
        {
            var plot = Plot(fontSize);
            var legend = plot.LegendSize(width);
            Assert.True(legend.Width <= width, $"legend {legend.Width:F0} wide in {width}");
            Assert.True(legend.Height > 0);
        });
    }

    [Fact]
    public void AWrappedLegend_IsTallerThanOneLine()
    {
        RunOnSta(() =>
        {
            var plot = Plot(32);
            Assert.True(plot.LegendSize(200).Height > plot.LegendSize(2000).Height);
        });
    }

    [Fact]
    public void PreferredPlotSide_MakesTheSquareLarger_UpToTheWidth()
    {
        RunOnSta(() =>
        {
            var normal = Plot(17.33);
            normal.Measure(new Size(600, double.PositiveInfinity));
            var larger = Plot(17.33);
            larger.PreferredPlotSide = 640;
            larger.Measure(new Size(600, double.PositiveInfinity));
            Assert.True(larger.DesiredSize.Height - normal.DesiredSize.Height >= 600 - 420 - 1,
                $"{larger.DesiredSize.Height:F0} against {normal.DesiredSize.Height:F0}");
            var normalWide = Plot(17.33);
            normalWide.Measure(new Size(2000, double.PositiveInfinity));
            var wide = Plot(17.33);
            wide.PreferredPlotSide = 640;
            wide.Measure(new Size(2000, double.PositiveInfinity));
            Assert.True(wide.DesiredSize.Height - normalWide.DesiredSize.Height >= 640 - 420 - 1, "up to the preferred side when there is room");
        });
    }

    [Theory]
    [InlineData(32, 340)]
    [InlineData(17.33, 640)]
    public void ThePlot_IsASquareNearlyAsWideAsTheControl(double fontSize, double width)
    {
        RunOnSta(() =>
        {
            var plot = Plot(fontSize);
            plot.PreferredPlotSide = 640;
            plot.Measure(new Size(width, double.PositiveInfinity));
            plot.Arrange(new Rect(0, 0, width, plot.DesiredSize.Height));
            var area = plot.PlotArea;
            Assert.True(Math.Abs(area.Width - area.Height) < 1.5, $"{area.Width:F0} by {area.Height:F0}");
            Assert.True(area.Width >= width - 3 * fontSize, $"plot {area.Width:F0} wide in {width}");   // only the y labels beside it
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
