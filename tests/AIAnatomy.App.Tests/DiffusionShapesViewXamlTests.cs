using System.IO;
using System.Xml.Linq;

namespace AIAnatomy.App.Tests;

/// <summary>
/// Lesson 4.9's view, read as XML (Task 16 review fixes): the cloud's axes are fixed for the whole run at −2 to 2, never
/// fitted to each step (that would make the cloud look the same size at step 0 and step 100), and the step slider stretches
/// with the column instead of having a fixed width that a narrow pane at 24 pt clips. The step line is live only when
/// Play is not running.
/// </summary>
public class DiffusionShapesViewXamlTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Controls = "clr-namespace:AIAnatomy.App.Controls";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument View()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AIAnatomy.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return XDocument.Load(Path.Combine(dir!.FullName, "src", "AIAnatomy.App", "Views", "DiffusionShapesView.xaml"));
    }

    [Fact]
    public void TheCloud_HasFixedAxesFromMinus2To2_NeverFittedToEachStep()
    {
        var cloud = Assert.Single(View().Descendants(Controls + "PointCloudPlot"));
        Assert.Equal("2", (string?)cloud.Attribute("AxisLimit"));
        Assert.Null(cloud.Attribute("FitToData"));
    }

    [Fact]
    public void TheStepSlider_StretchesWithTheColumn_WithAMinimumWidth()
    {
        var slider = Assert.Single(View().Descendants(Wpf + "Slider"));
        Assert.Null(slider.Attribute("Width"));
        Assert.Equal("Stretch", (string?)slider.Attribute("HorizontalAlignment"));
        Assert.True(double.Parse((string)slider.Attribute("MinWidth")!) >= 120);
        // Not in a WrapPanel, which would give it unlimited width (so Stretch would do nothing).
        Assert.NotEqual(Wpf + "WrapPanel", slider.Parent!.Name);
    }

    [Fact]
    public void TheStepLine_IsLiveOnlyWhenPlayIsNotRunning()
    {
        var line = Assert.Single(View().Descendants(Wpf + "TextBlock"), t => (string?)t.Attribute("Text") == "{Binding StepText}");
        Assert.Equal("{Binding IsStepLineLive}", (string?)line.Attribute(XName.Get("LiveText.IsLive", Controls.NamespaceName)));
    }
}
