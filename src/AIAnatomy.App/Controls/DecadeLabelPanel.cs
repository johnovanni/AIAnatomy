using System.Windows;
using System.Windows.Controls;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Lays out one short label per equal cell, each starting at its cell's left edge (where the gridline is), and never
/// wraps them: if the widest label is wider than a cell, only every other label is shown (then every third, and so on).
/// </summary>
public sealed class DecadeLabelPanel : Panel
{
    private int Step(double cell)
    {
        double widest = InternalChildren.Cast<UIElement>().Select(c => c.DesiredSize.Width).DefaultIfEmpty(0).Max() + 6;
        return cell <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(widest / cell));
    }

    protected override Size MeasureOverride(Size available)
    {
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(available.Width) ? 0 : available.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int n = InternalChildren.Count;
        double cell = n == 0 ? 0 : finalSize.Width / n;
        int step = Step(cell);
        for (int i = 0; i < n; i++)
        {
            var child = InternalChildren[i];
            bool shown = i % step == 0;
            child.Visibility = shown ? Visibility.Visible : Visibility.Hidden;
            child.Arrange(new Rect(i * cell, 0, child.DesiredSize.Width, finalSize.Height));
        }
        return finalSize;
    }
}
