using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Gives an element a transformer part's colour as a theme resource (so a change of theme recolours it at once). Set
/// <c>c:PartResource.Background</c>, <c>BorderBrush</c> or <c>Foreground</c> to a part key such as "attention"; a null or
/// unknown key (a lesson with no part yet) gives a neutral colour. Also the part's icon glyph.
/// </summary>
public static class PartResource
{
    public static readonly DependencyProperty BackgroundProperty = Register("Background", "BorderBrush");
    public static readonly DependencyProperty BorderBrushProperty = Register("BorderBrush", "BorderBrush");
    public static readonly DependencyProperty ForegroundProperty = Register("Foreground", "MutedTextBrush");

    public static string? GetBackground(DependencyObject o) => (string?)o.GetValue(BackgroundProperty);
    public static void SetBackground(DependencyObject o, string? v) => o.SetValue(BackgroundProperty, v);
    public static string? GetBorderBrush(DependencyObject o) => (string?)o.GetValue(BorderBrushProperty);
    public static void SetBorderBrush(DependencyObject o, string? v) => o.SetValue(BorderBrushProperty, v);
    public static string? GetForeground(DependencyObject o) => (string?)o.GetValue(ForegroundProperty);
    public static void SetForeground(DependencyObject o, string? v) => o.SetValue(ForegroundProperty, v);

    /// <summary>The resource key of a part's brush, for example "PartAttentionBrush"; <paramref name="neutral"/> when there is no known part.</summary>
    public static string BrushKey(string? part, string neutral) =>
        AIAnatomy.Lessons.Parts.IsKnown(part) ? $"Part{char.ToUpperInvariant(part![0])}{part[1..]}Brush" : neutral;

    // One glyph per part key, from Segoe Fluent Icons (Segoe MDL2 Assets on Windows 10 has the same code points).
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        ["intro"] = "\uE946",        // Info: where to start
        ["tokens"] = "\uE8D2",       // Font: text cut into pieces
        ["embeddings"] = "\uE707",   // MapPin: a place in meaning-space
        ["position"] = "\uE8FD",     // Bullets: things in order
        ["attention"] = "\uE7B3",    // View: looking back at other tokens
        ["feedforward"] = "\uE8EF",  // Calculator: sums on each token
        ["residual"] = "\uE72C",     // Refresh: the stream that is added to, round and round
        ["output"] = "\uE724",       // Send: the answer going out
        ["learning"] = "\uE8F1",     // Library: what has been learned
        ["whole"] = "\uE71D",        // AllApps: every part together
        ["later"] = "\uE81C",        // History: not yet
        ["family"] = "\uE716",       // People: the family of AI
        ["rules"] = "\uE8A5",        // Document: a rule book, written by hand
        ["search"] = "\uE721",       // Search: looking through the options
        ["classic"] = "\uE713",      // Settings: classic machine learning, a tuned machine
        ["reward"] = "\uE734",       // FavoriteStar: the reward that learning chases
    };

    /// <summary>The icon glyph for a part key; a neutral dot for no part.</summary>
    public static string Glyph(string? part) => part != null && Glyphs.TryGetValue(part, out var g) ? g : "\uE91F";   // E91F: a small circle

    public static readonly IValueConverter GlyphConverter = new GlyphValueConverter();

    // An element with no part key yet keeps the colour its XAML gives it; one whose key becomes null or unknown gets the neutral colour.
    private static DependencyProperty Register(string name, string neutral) =>
        DependencyProperty.RegisterAttached(name, typeof(string), typeof(PartResource),
            new PropertyMetadata(null, (o, e) => Apply(o, name, (string?)e.NewValue, neutral)));

    private static void Apply(DependencyObject o, string which, string? part, string neutral)
    {
        if (o is not FrameworkElement fe) return;
        string key = BrushKey(part, neutral);
        DependencyProperty? target = (which, o) switch
        {
            ("Background", Border) => Border.BackgroundProperty,
            ("Background", Control) => Control.BackgroundProperty,
            ("Background", Panel) => Panel.BackgroundProperty,
            ("Background", Shape) => Shape.FillProperty,
            ("BorderBrush", Border) => Border.BorderBrushProperty,
            ("BorderBrush", Control) => Control.BorderBrushProperty,
            ("Foreground", TextBlock) => TextBlock.ForegroundProperty,
            ("Foreground", Control) => Control.ForegroundProperty,
            _ => null,
        };
        if (target != null) fe.SetResourceReference(target, key);
    }

    private sealed class GlyphValueConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Glyph(value as string);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
