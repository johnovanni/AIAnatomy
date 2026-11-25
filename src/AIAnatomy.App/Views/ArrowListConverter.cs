using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AIAnatomy.App.Views;

/// <summary>A list of arrows from a view-model ((X, Y) pairs for each end), as the <see cref="Point"/> pairs a plot draws;
/// null for anything else.</summary>
public sealed class ArrowListConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IReadOnlyList<((double X, double Y) From, (double X, double Y) To)> arrows
            ? arrows.Select(a => (new Point(a.From.X, a.From.Y), new Point(a.To.X, a.To.Y))).ToList()
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
