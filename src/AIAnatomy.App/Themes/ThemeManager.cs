using System.Windows;

namespace AIAnatomy.App.Themes;

/// <summary>Swaps the Light or Dark colour dictionary and sets the text-size resources the whole app uses.</summary>
public static class ThemeManager
{
    // Settings are in points; WPF sizes are device-independent pixels (1 pt = 96/72 px).
    private const double PxPerPt = 96.0 / 72.0;

    private static ResourceDictionary? _current;

    /// <summary>The theme now showing ("Light" or "Dark").</summary>
    public static string Theme { get; private set; } = "Dark";

    /// <summary>
    /// Applies a theme and sizes, all from the body size in points: BodyFontSize, TitleFontSize (body x 2), HeadingFontSize
    /// and SubheadingFontSize (body x 1.15), SmallFontSize (body x 0.85, never under 9 pt), FormulaScale and ScrollBarSize.
    /// </summary>
    public static void Apply(string theme, double bodyPt)
    {
        var resources = Application.Current.Resources;
        Theme = theme == "Dark" ? "Dark" : "Light";
        var next = new ResourceDictionary { Source = new Uri($"/AIAnatomy.App;component/Themes/{Theme}.xaml", UriKind.Relative) };
        if (_current != null) resources.MergedDictionaries.Remove(_current);
        resources.MergedDictionaries.Add(next);
        _current = next;

        resources["BodyFontSize"] = bodyPt * PxPerPt;
        resources["TitleFontSize"] = bodyPt * 2 * PxPerPt;
        resources["HeadingFontSize"] = bodyPt * 1.15 * PxPerPt;
        resources["SubheadingFontSize"] = bodyPt * 1.15 * PxPerPt;
        resources["SmallFontSize"] = Math.Max(9, bodyPt * 0.85) * PxPerPt;
        resources["FormulaScale"] = bodyPt * 1.1 * PxPerPt;
        resources["LessonsColumnWidth"] = bodyPt * 11 * PxPerPt;   // whole words of lesson titles fit at every text size
        resources["GraphDiagramHeight"] = bodyPt * 30 * PxPerPt;    // the expression graph has six boxes with two lines of numbers each
        resources["DiagramHeight"] = bodyPt * 21 * PxPerPt;        // the map keeps its text legible at large sizes
        resources["ScrollBarSize"] = Math.Max(14, 24 * bodyPt / 20);
    }
}
