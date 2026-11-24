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

    // Machado et al. 2009, severity 1.0, applied to linear RGB.
    private static readonly double[][] Protan =
        [[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]];
    private static readonly double[][] Deutan =
        [[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]];

    private static double[] LinearRgb(string hex)
    {
        var h = hex.TrimStart('#');
        double Ch(int i)
        {
            var c = int.Parse(h.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return [Ch(0), Ch(2), Ch(4)];
    }

    private static double[] Lab(double[] rgb)
    {
        double[] xyz =
        [
            (0.4124564 * rgb[0] + 0.3575761 * rgb[1] + 0.1804375 * rgb[2]) / 0.95047,
            0.2126729 * rgb[0] + 0.7151522 * rgb[1] + 0.0721750 * rgb[2],
            (0.0193339 * rgb[0] + 0.1191920 * rgb[1] + 0.9503041 * rgb[2]) / 1.08883,
        ];
        double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
        var (fx, fy, fz) = (F(xyz[0]), F(xyz[1]), F(xyz[2]));
        return [116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)];
    }

    private static double[] Simulate(double[][] m, double[] rgb) =>
        m.Select(r => Math.Clamp(r[0] * rgb[0] + r[1] * rgb[1] + r[2] * rgb[2], 0, 1)).ToArray();

    private static double DeltaE(double[] a, double[] b) =>
        Math.Sqrt(a.Zip(b, (x, y) => (x - y) * (x - y)).Sum());

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void PartBrushes_AreDistinguishable_ForColourBlindViewers(string theme)
    {
        var map = Brushes(theme);
        var keys = Parts.All.ToArray();
        (string Name, double Min, Func<double[], double[]> View)[] views =
        [
            ("normal vision", 15, c => c),
            ("protanopia", 8, c => Simulate(Protan, c)),
            ("deuteranopia", 8, c => Simulate(Deutan, c)),
        ];
        for (var i = 0; i < keys.Length; i++)
            for (var j = 0; j < i; j++)
            {
                var a = LinearRgb(map[PartBrush(keys[i])]);
                var b = LinearRgb(map[PartBrush(keys[j])]);
                foreach (var (name, min, view) in views)
                {
                    var de = DeltaE(Lab(view(a)), Lab(view(b)));
                    Assert.True(de >= min, $"{theme} {keys[j]} vs {keys[i]} under {name}: DeltaE {de:F1} < {min}");
                }
            }
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
