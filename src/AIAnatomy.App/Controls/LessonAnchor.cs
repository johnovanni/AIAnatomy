using System.Windows;
using System.Windows.Media;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Names a place on a lesson page (or inside a lab view) so the page can scroll to it when a lesson branch or a Check
/// yourself question points there. Set <c>c:LessonAnchor.Name="..."</c> on any element. The page itself names
/// <see cref="Explanation"/>, <see cref="Formula"/>, <see cref="Brains"/> and <see cref="Lab"/>.
/// </summary>
public static class LessonAnchor
{
    public const string Explanation = "explanation", Formula = "formula", Brains = "brains", Lab = "lab";

    public static readonly DependencyProperty NameProperty = DependencyProperty.RegisterAttached(
        "Name", typeof(string), typeof(LessonAnchor), new PropertyMetadata(null));

    public static string? GetName(DependencyObject element) => (string?)element.GetValue(NameProperty);
    public static void SetName(DependencyObject element, string? value) => element.SetValue(NameProperty, value);

    /// <summary>The first element at or below <paramref name="root"/> (in the visual tree) named <paramref name="name"/>, or null.</summary>
    public static FrameworkElement? Find(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe && string.Equals(GetName(fe), name, StringComparison.Ordinal)) return fe;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
            if (Find(VisualTreeHelper.GetChild(root, i), name) is { } found) return found;
        return null;
    }
}
