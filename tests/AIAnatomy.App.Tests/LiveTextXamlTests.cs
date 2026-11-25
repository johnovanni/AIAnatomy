using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AIAnatomy.App.Tests;

/// <summary>Reads the view files from the source tree as plain text and XML (they are not copied beside the tests).</summary>
public class LiveTextXamlTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static string ViewsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AIAnatomy.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "AIAnatomy.App", "Views");
    }

    /// <summary>A text block with an accessible name is read as that name, not as its own text: a bound result line would be hidden.</summary>
    [Fact]
    public void NoTextBlockHidesItsBoundText()
    {
        var bad = new List<string>();
        foreach (var file in Directory.GetFiles(ViewsFolder(), "*.xaml"))
        {
            var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var tb in doc.Descendants(Wpf + "TextBlock"))
            {
                string? text = (string?)tb.Attribute("Text");
                if (text == null || !text.TrimStart().StartsWith("{Binding", StringComparison.Ordinal)) continue;
                if (tb.Attribute("AutomationProperties.Name") == null) continue;
                bad.Add($"{Path.GetFileName(file)} line {((IXmlLineInfo)tb).LineNumber}");
            }
        }
        Assert.True(bad.Count == 0, "TextBlocks with bound Text and AutomationProperties.Name: " + string.Join("; ", bad));
    }

    /// <summary>The shared behaviour replaces the hand-written handlers (a view may still keep a conditional announcement of its own, under another name).</summary>
    [Fact]
    public void ResultLinesUseLiveText()
    {
        var bad = Directory.GetFiles(ViewsFolder(), "*.xaml*")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"OnLiveTextUpdated|OnFeedbackUpdated"))
            .Select(Path.GetFileName);
        Assert.True(!bad.Any(), "Hand-written live text handlers in: " + string.Join(", ", bad));
    }

    [Fact]
    public void LiveTextRaisesOnlyWhenTheTextChanges()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var t = new System.Windows.Controls.TextBlock();
                AIAnatomy.App.Controls.LiveText.SetIsLive(t, true);
                Assert.Equal(System.Windows.Automation.AutomationLiveSetting.Polite,
                    System.Windows.Automation.AutomationProperties.GetLiveSetting(t));
                Assert.True(AIAnatomy.App.Controls.LiveText.ShouldAnnounce("old", "new"));
                Assert.False(AIAnatomy.App.Controls.LiveText.ShouldAnnounce("same", "same"));
                Assert.False(AIAnatomy.App.Controls.LiveText.ShouldAnnounce("old", ""));
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);   // WPF elements need a single-threaded apartment
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("The STA work failed.", failure);
    }
}
