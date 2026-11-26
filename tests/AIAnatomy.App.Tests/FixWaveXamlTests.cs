using System.IO;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Xml.Linq;
using AIAnatomy.App.Controls;

namespace AIAnatomy.App.Tests;

/// <summary>Stage 2 Part B final fix wave: the views' heat-map formats, the connection sentence's live setting, and
/// <see cref="LiveText.Announce"/>.</summary>
public class FixWaveXamlTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Controls = "clr-namespace:AIAnatomy.App.Controls";
    private static readonly XName Live = Controls + "LiveText.IsLive";
    private static readonly XName Assertive = Controls + "LiveText.IsAssertive";

    private static string ViewsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AIAnatomy.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "AIAnatomy.App", "Views");
    }

    private static XDocument View(string name) => XDocument.Load(Path.Combine(ViewsFolder(), name));

    // Ruling 2: lesson 2.4's two attention maps write whole percentages, with the note that says how to read them.
    [Fact]
    public void AttentionMaps_WriteWholePercentages_WithTheNote()
    {
        var doc = View("AttentionView.xaml");
        var maps = doc.Descendants(Controls + "HeatMap").ToList();
        Assert.Equal(2, maps.Count);
        Assert.All(maps, m => Assert.Equal("Percent", (string?)m.Attribute("CellFormat")));
        Assert.All(maps, m => Assert.Null(m.Attribute("ShowCellValues")));
        Assert.Equal(2, doc.Descendants(Wpf + "TextBlock").Count(t =>
            (string?)t.Attribute("Text") == "{x:Static labs:AttentionLabViewModel.PercentNote}"));
    }

    // Ruling 2: the maps that are too wide for numbers keep them off (by design), and stay as they were.
    [Theory]
    [InlineData("MultiHeadView.xaml")]
    [InlineData("PositionView.xaml")]
    [InlineData("HiddenStateBlockView.xaml")]
    public void WideMaps_KeepNoNumbersInTheirSquares(string file)
    {
        var maps = View(file).Descendants(Controls + "HeatMap").ToList();
        Assert.NotEmpty(maps);
        Assert.All(maps, m => Assert.Equal("False", (string?)m.Attribute("ShowCellValues")));
    }

    // Final review M8: the digit pictures of lessons 4.6 and 4.7 are drawn as paper and ink (white to black), so blue
    // means "below zero" only, as on the feature maps beside them.
    [Theory]
    [InlineData("FiltersView.xaml")]
    [InlineData("DigitNetworkView.xaml")]
    public void DigitPictures_UseTheInkRamp(string file)
    {
        XName name = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        var map = View(file).Descendants(Controls + "HeatMap").Single(m => (string?)m.Attribute(name) == "ImageMap");
        Assert.Equal("{DynamicResource InkLowColor}", (string?)map.Attribute("RampLowColor"));
        Assert.Equal("{DynamicResource InkMidColor}", (string?)map.Attribute("RampMidColor"));
        Assert.Equal("{DynamicResource InkHighColor}", (string?)map.Attribute("RampHighColor"));
    }

    // Final review M9: the feature map's key is body text, and it does not promise numbers the squares may have no room for.
    [Fact]
    public void FeatureMapKey_IsBodyText_AndHonestAboutNumbers()
    {
        var key = View("FiltersView.xaml").Descendants(Wpf + "TextBlock")
            .Single(t => ((string?)t.Attribute("Text"))?.StartsWith("Red is above zero", StringComparison.Ordinal) == true);
        Assert.Equal("{StaticResource BodyText}", (string?)key.Attribute("Style"));
        Assert.Contains("when there is room", (string)key.Attribute("Text")!);
        Assert.Contains("point at a square", (string)key.Attribute("Text")!);
    }

    // Ruling 4 (B5): the sentence under the connection chips is a polite live region. When the mouse moves to a chip a
    // screen reader is not on, nothing else would say it; a double reading with the chip's HelpText is the smaller harm.
    [Fact]
    public void ConnectionNote_IsPoliteLiveText()
    {
        XName name = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        var note = View("LessonPageView.xaml").Descendants(Wpf + "TextBlock").Single(t => (string?)t.Attribute(name) == "ConnectionNote");
        Assert.Equal("True", (string?)note.Attribute(Live));
        Assert.Null(note.Attribute(Assertive));
    }

    // Triage (Task 12a): each map's range line ("−0.42 to +1.30") is read like body text, never smaller than 12 DIPs.
    [Theory]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(24)]
    public void FeatureMapRangeLines_AreAtLeastTheBodySize(double points)
    {
        RunOnSta(() =>
        {
            double body = points * 96 / 72;
            var grid = new FeatureMapGrid { FontSize = body };
            Assert.True(grid.RangeTextSize >= body && grid.RangeTextSize >= 12, $"{grid.RangeTextSize} at {body}");
        });
    }

    // Found in this wave's render: with the range lines at body size, two neighbours ("0 to +1.35 0 to +1.57") were only
    // 14 px apart, under one character at 24 pt. The space from the end of one range line to the next map is at least 1 em.
    [Theory]
    [InlineData(13)]
    [InlineData(24)]
    public void FeatureMapRangeLines_KeepAnEmBetweenNeighbours(double points)
    {
        RunOnSta(() =>
        {
            var maps = Enumerable.Range(0, 16).Select(k =>
            {
                var m = new double[8, 8];
                m[3, 3] = 1.35 + k * 0.1;   // "0 to +1.35" and so on: the width the lab shows
                return m;
            }).ToList();
            var grid = new FeatureMapGrid { FontSize = points * 96 / 72, Maps = maps };
            foreach (double width in new[] { 500.0, 800.0, 1200.0 })
                Assert.True(grid.RangeLineSpacing(width) >= grid.RangeTextSize, $"{grid.RangeLineSpacing(width)} at {width}, {points} pt");
        });
    }

    // Triage (Task 12a): the faint target rings of the point-cloud plots are the only view of the shape on the plot, so
    // their colour (the muted text colour) must reach 3:1 against the pane and the screen in both themes.
    [Fact]
    public void TargetRings_UseTheMutedBrush()
    {
        string source = File.ReadAllText(Path.Combine(ViewsFolder(), "..", "Controls", "PointCloudPlot.cs"));
        Assert.Contains("new Pen(MutedBrush, 1), Markers(target.", source);
    }

    // Final review M15: lesson 4.10's captions say that arrows past the ±2 plot are cut off, and why β hugs the axis.
    [Fact]
    public void DiffusionStepsCaptions_ExplainCutOffArrowsAndTheFlatBeta()
    {
        var texts = View("DiffusionStepsView.xaml").Descendants(Wpf + "TextBlock").Select(t => (string?)t.Attribute("Text") ?? "").ToList();
        Assert.Contains(texts, t => t.Contains("not scaled", StringComparison.Ordinal)
                                    && t.Contains("runs past the edge of the plot is cut off there, without its head", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("β (beta)", StringComparison.Ordinal)
                                    && t.Contains("at most 0.1, so on this 0 to 1 scale its line stays close to the axis", StringComparison.Ordinal));
    }

    // Triage (Task 2): icon glyphs are written as escapes (\uE722 in C#, &#xE722; in XAML), never as the invisible
    // private-use characters themselves, which are easy to damage without seeing it.
    [Fact]
    public void NoSourceFile_HoldsALiteralPrivateUseCharacter()
    {
        string root = Path.GetFullPath(Path.Combine(ViewsFolder(), "..", "..", ".."));
        string sep = Path.DirectorySeparatorChar.ToString();
        var bad = new List<string>();
        foreach (string folder in new[] { "src", "tests" })
            foreach (string file in Directory.GetFiles(Path.Combine(root, folder), "*.*", SearchOption.AllDirectories))
            {
                if (!(file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal))) continue;
                if (file.Contains(sep + "bin" + sep) || file.Contains(sep + "obj" + sep)) continue;
                if (File.ReadAllText(file).Any(ch => ch >= '\uE000' && ch <= '\uF8FF')) bad.Add(Path.GetFileName(file));
            }
        Assert.True(bad.Count == 0, "Literal private-use characters in: " + string.Join(", ", bad));
    }

    // Task 18 review minor: the family tree's two hint lines sit together without a negative margin pulling one up.
    [Fact]
    public void FamilyTreeHints_HaveNoNegativeMargin()
    {
        var margins = View("FamilyTreeView.xaml").Descendants().Select(e => (string?)e.Attribute("Margin")).OfType<string>();
        Assert.DoesNotContain(margins, m => m.Contains('-'));
    }

    // Task 18 review minor: LiveText.Announce says a block's text only when there is text and a screen reader is listening.
    [Fact]
    public void Announce_RaisesOnlyWithTextAndAListener()
    {
        RunOnSta(() =>
        {
            var block = new TextBlock { Text = "Reset everything?" };
            AutomationProperties.SetLiveSetting(block, AutomationLiveSetting.Polite);
            Assert.True(LiveText.Announce(block, listening: true));
            Assert.False(LiveText.Announce(block, listening: false));
            Assert.False(LiveText.Announce(new TextBlock { Text = "" }, listening: true));
            Assert.Throws<ArgumentNullException>(() => LiveText.Announce(null!, listening: true));
            // With no screen reader running (as in a test), the public form says nothing and does not throw.
            LiveText.Announce(block);
        });
    }

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
