using System.Windows;
using System.Windows.Media;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Controls;

/// <summary>Where a chart's plot area is and how data maps into it.</summary>
public sealed record AxisFrame(Rect Plot, double XMin, double XMax, double YMin, double YMax,
    IReadOnlyList<double> XTicks, IReadOnlyList<double> YTicks, string? XTitle, string? YTitle, double TitleHeight)
{
    public Point Map(double x, double y) =>
        new(Plot.Left + (x - XMin) / (XMax - XMin) * Plot.Width, Plot.Bottom - (y - YMin) / (YMax - YMin) * Plot.Height);
}

public abstract partial class ChartBase
{
    /// <summary>Lays out axes inside <paramref name="bounds"/>: the y title above the plot, the x title under the tick labels.</summary>
    protected AxisFrame LayoutAxes(Rect bounds, double xMin, double xMax, double yMin, double yMax, string? xTitle, string? yTitle)
    {
        double label = LabelSize;
        double tickCount = Math.Clamp(bounds.Width / (label * 6), 2, 8);
        var yTicks = AxisTicks.Ticks(yMin, yMax, Math.Clamp((int)(bounds.Height / (label * 3)), 2, 8));
        var xTicks = AxisTicks.Ticks(xMin, xMax, (int)tickCount);
        double yLabelWidth = yTicks.Select(t => Number(NumberText.Tick(t), label, Foreground).Width).DefaultIfEmpty(0).Max();
        double lineHeight = Number("0", label, Foreground).Height;
        double titleHeight = string.IsNullOrWhiteSpace(yTitle) ? 0 : Text(yTitle, label, MutedBrush, maxWidth: bounds.Width).Height + 6;
        double xTitleHeight = string.IsNullOrWhiteSpace(xTitle) ? 0 : Text(xTitle, label, MutedBrush, maxWidth: bounds.Width).Height + 4;
        double lastXLabel = xTicks.Count == 0 ? 0 : Number(NumberText.Tick(xTicks[^1]), label, Foreground).Width / 2;
        var plot = new Rect(bounds.Left + yLabelWidth + 12, bounds.Top + titleHeight + lineHeight / 2,
            Math.Max(10, bounds.Width - yLabelWidth - 12 - lastXLabel - 4),
            Math.Max(10, bounds.Height - titleHeight - lineHeight / 2 - lineHeight - 6 - xTitleHeight));
        return new AxisFrame(plot, xMin, xMax, yMin, yMax, xTicks, yTicks, xTitle, yTitle, titleHeight);
    }

    protected void DrawAxes(DrawingContext dc, AxisFrame f, Rect bounds)
    {
        double label = LabelSize;
        var grid = new Pen(GridBrush, 1);
        var axis = new Pen(AxisBrush, 1.5);
        foreach (double t in f.YTicks)
        {
            double y = f.Map(f.XMin, t).Y;
            dc.DrawLine(grid, new Point(f.Plot.Left, y), new Point(f.Plot.Right, y));
            var text = Number(NumberText.Tick(t), label, Foreground);
            dc.DrawText(text, new Point(f.Plot.Left - 8 - text.Width, y - text.Height / 2));
        }
        foreach (double t in f.XTicks)
        {
            double x = f.Map(t, f.YMin).X;
            dc.DrawLine(grid, new Point(x, f.Plot.Top), new Point(x, f.Plot.Bottom));
            var text = Number(NumberText.Tick(t), label, Foreground);
            dc.DrawText(text, new Point(x - text.Width / 2, f.Plot.Bottom + 4));
        }
        dc.DrawLine(axis, f.Plot.BottomLeft, f.Plot.BottomRight);
        dc.DrawLine(axis, f.Plot.BottomLeft, f.Plot.TopLeft);
        if (!string.IsNullOrWhiteSpace(f.YTitle))
            dc.DrawText(Text("\u2191 " + f.YTitle, label, MutedBrush, maxWidth: bounds.Width), new Point(bounds.Left, bounds.Top));
        if (!string.IsNullOrWhiteSpace(f.XTitle))
        {
            var title = Text(f.XTitle + " \u2192", label, MutedBrush, maxWidth: bounds.Width);
            dc.DrawText(title, new Point(Math.Max(bounds.Left, f.Plot.Right - title.Width), f.Plot.Bottom + 6 + Number("0", label, Foreground).Height));
        }
    }
}
