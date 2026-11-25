using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AIAnatomy.App.Tests;

/// <summary>Reads the view files from the source tree as plain text and XML (they are not copied beside the tests).</summary>
public class LiveTextXamlTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static string AppFolder() => Path.Combine(RepoRoot(), "src", "AIAnatomy.App");

    /// <summary>Every XAML file of the App (views, controls, themes, the main window), not the build output.</summary>
    private static string[] AllXaml()
    {
        string sep = Path.DirectorySeparatorChar.ToString();
        var files = Directory.GetFiles(AppFolder(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(sep + "bin" + sep) && !f.Contains(sep + "obj" + sep)).ToArray();
        Assert.True(files.Length > 20, "The App's XAML files were not found.");
        Assert.Contains(files, f => f.EndsWith("MainWindow.xaml"));
        return files;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AIAnatomy.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

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
        foreach (var file in AllXaml())
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

    /// <summary>The result lines converted to the shared behaviour (so a renamed copy of the old handler cannot pass): the number of
    /// <c>c:LiveText.IsLive="True"</c> each file must carry.</summary>
    [Theory]
    [InlineData("MainWindow.xaml", 1)]
    [InlineData("Views/CheckYourselfView.xaml", 1)]
    [InlineData("Views/ClassicMlView.xaml", 2)]
    [InlineData("Views/ExpertSystemView.xaml", 1)]
    [InlineData("Views/RewardView.xaml", 2)]
    [InlineData("Views/SearchView.xaml", 2)]
    [InlineData("Views/SamplingView.xaml", 1)]
    [InlineData("Views/LearningView.xaml", 1)]
    [InlineData("Views/TrainView.xaml", 3)]
    [InlineData("Views/TokensView.xaml", 1)]
    public void ConvertedResultLinesCarryLiveText(string file, int expected)
    {
        string text = File.ReadAllText(Path.Combine(AppFolder(), file));
        Assert.True(Regex.Matches(text, @"c:LiveText\.IsLive=""True""").Count >= expected,
            $"{file} should have {expected} c:LiveText.IsLive=\"True\" result lines.");
    }

    /// <summary>Lesson 4.7: with an empty pad or a dot, the lab's Message and PredictionText hold the same words, so only the
    /// prediction line may be live; the copy next to the pad is shown but not announced, so a screen reader says it once.</summary>
    [Fact]
    public void DigitNetworkView_AnnouncesTheNotADigitMessageOnce()
    {
        var doc = XDocument.Load(Path.Combine(ViewsFolder(), "DigitNetworkView.xaml"));
        XName live = XName.Get("LiveText.IsLive", "clr-namespace:AIAnatomy.App.Controls");
        var bound = doc.Descendants(Wpf + "TextBlock")
            .Where(t => (string?)t.Attribute("Text") is "{Binding Message}" or "{Binding PredictionText}")
            .ToDictionary(t => (string)t.Attribute("Text")!, t => (string?)t.Attribute(live) == "True");
        Assert.Equal(2, bound.Count);
        Assert.True(bound["{Binding PredictionText}"], "The prediction line should be live.");
        Assert.False(bound["{Binding Message}"], "The message beside the pad repeats the prediction line, so it should not be live.");
    }

    /// <summary>Lesson 4.10: the lines that follow a slider (the forward step, its prediction error, the frame of a run)
    /// change at every tick, so they are not live; the result of a run, which changes only when the learner picks a number
    /// of steps or a seed, is.</summary>
    [Fact]
    public void DiffusionStepsView_AnnouncesResults_NotSliderTicks()
    {
        var doc = XDocument.Load(Path.Combine(ViewsFolder(), "DiffusionStepsView.xaml"));
        XName live = XName.Get("LiveText.IsLive", "clr-namespace:AIAnatomy.App.Controls");
        var bound = doc.Descendants(Wpf + "TextBlock")
            .Where(t => ((string?)t.Attribute("Text"))?.StartsWith("{Binding", StringComparison.Ordinal) == true)
            .ToDictionary(t => (string)t.Attribute("Text")!, t => (string?)t.Attribute(live) == "True");
        Assert.True(bound["{Binding ResultText}"], "The result line should be live.");
        Assert.False(bound["{Binding ForwardText}"], "The forward step line follows a slider.");
        Assert.False(bound["{Binding PredictionText}"], "The prediction error follows the forward slider.");
        Assert.False(bound["{Binding FrameText}"], "The frame line follows a slider.");
        Assert.False(bound["{Binding MeasuredText}"], "The list of runs repeats the result line's numbers.");
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
