using System.IO;
using System.Globalization;
using System.Xml.Linq;
using AIAnatomy.Lessons;

namespace AIAnatomy.App.Tests;

/// <summary>Reads the theme files as plain XML (no WPF resource loading) and checks WCAG 2.x contrast.</summary>
public class ThemeContrastTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly (string Fg, string Bg)[] TextPairs =
    [
        ("TextBrush", "WindowBrush"), ("TextBrush", "SurfaceBrush"), ("TextBrush", "ScreenBrush"),
        ("MutedTextBrush", "WindowBrush"), ("MutedTextBrush", "SurfaceBrush"), ("MutedTextBrush", "ScreenBrush"),
        ("ScreenTextBrush", "ScreenBrush"),
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> Cache = new();

    private static Dictionary<string, string> Brushes(string theme)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(theme, out var found)) return found;
            var path = Path.Combine(AppContext.BaseDirectory, "Themes", theme + ".xaml");
            var doc = XDocument.Load(path);
            var map = new Dictionary<string, string>();
            foreach (var e in doc.Descendants().Where(e => e.Name.LocalName == "SolidColorBrush"))
            {
                var key = (string?)e.Attribute(X + "Key");
                var color = (string?)e.Attribute("Color");
                if (key is null || color is null) continue;
                Assert.False(color.StartsWith('{'), $"{theme} {key}: Color must be a literal for the contrast tests");
                map[key] = color;
            }
            return Cache[theme] = map;
        }
    }

    private static double Luminance(string hex)
    {
        var h = hex.TrimStart('#');
        Assert.True(h.Length == 6, $"Expected #RRGGBB, got {hex}");
        double Ch(int i)
        {
            var c = int.Parse(h.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Ch(0) + 0.7152 * Ch(2) + 0.0722 * Ch(4);
    }

    private static double Contrast(string theme, string fg, string bg)
    {
        var map = Brushes(theme);
        Assert.True(map.ContainsKey(fg), $"{theme} is missing {fg}");
        Assert.True(map.ContainsKey(bg), $"{theme} is missing {bg}");
        var a = Luminance(map[fg]);
        var b = Luminance(map[bg]);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static string PartBrush(string key) => "Part" + char.ToUpperInvariant(key[0]) + key[1..] + "Brush";

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void TextBrushes_MeetFourPointFive(string theme)
    {
        foreach (var (fg, bg) in TextPairs)
            Assert.True(Contrast(theme, fg, bg) >= 4.5, $"{theme} {fg} on {bg}: {Contrast(theme, fg, bg):F2}");
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void PartAndConnectionBrushes_MeetThree(string theme)
    {
        var names = Parts.All.Select(PartBrush).Append("ConnectionBrush");
        foreach (var name in names)
            foreach (var bg in new[] { "SurfaceBrush", "ScreenBrush" })
                Assert.True(Contrast(theme, name, bg) >= 3.0, $"{theme} {name} on {bg}: {Contrast(theme, name, bg):F2}");
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void EveryPartKeyHasABrushInBothThemes(string theme)
    {
        var map = Brushes(theme);
        foreach (var key in Parts.All) Assert.True(map.ContainsKey(PartBrush(key)), $"{theme} lacks {PartBrush(key)}");
    }

    [Theory]
    [InlineData("Dark", false)]
    [InlineData("Light", true)]
    public void ScreenGlowOpacity_IsDefined_AndZeroInLight(string theme, bool zero)
    {
        var doc = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Themes", theme + ".xaml"));
        var e = doc.Descendants().Single(d => (string?)d.Attribute(X + "Key") == "ScreenGlowOpacity");
        var v = double.Parse(e.Value.Trim(), CultureInfo.InvariantCulture);
        if (zero) Assert.Equal(0, v); else Assert.InRange(v, 0.01, 1);
    }

    [Fact]
    public void PartsIsKnown_AcceptsOnlyListedKeys()
    {
        Assert.Equal(11, Parts.All.Count);
        Assert.All(Parts.All, k => Assert.True(Parts.IsKnown(k)));
        Assert.False(Parts.IsKnown("Attention"));
        Assert.False(Parts.IsKnown(""));
        Assert.False(Parts.IsKnown(null));
    }
}
