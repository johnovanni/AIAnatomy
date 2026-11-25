using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AIAnatomy.App.Views;

/// <summary>A list of (X, Y) pairs from a view-model, as the <see cref="Point"/>s a plot draws; null for anything else.</summary>
public sealed class PointListConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IReadOnlyList<(double X, double Y)> points ? points.Select(p => new Point(p.X, p.Y)).ToList() : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
