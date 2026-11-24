namespace AIAnatomy.Presentation.Tests;

public class CommandLineTests
{
    [Fact]
    public void ParsesRenderPngWithDefaults()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "gallery", @"C:\temp\g.png"]);

        Assert.Null(o.Error);
        Assert.Equal(DiagnosticMode.RenderPng, o.Mode);
        Assert.Equal("gallery", o.View);
        Assert.Equal(@"C:\temp\g.png", o.OutputFile);
        Assert.Equal("Light", o.Theme);
        Assert.Equal(13, o.TextSize);
        Assert.Equal(1600, o.Width);
        Assert.Equal(1000, o.Height);
    }

    [Fact]
    public void ParsesThemeAndTextSize()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "attention", "a.png", "--theme", "dark", "--text-size", "24", "--width", "1280", "--height", "800"]);

        Assert.Null(o.Error);
        Assert.Equal("attention", o.View);
        Assert.Equal("Dark", o.Theme);
        Assert.Equal(24, o.TextSize);
        Assert.Equal(1280, o.Width);
        Assert.Equal(800, o.Height);
    }

    [Fact]
    public void UnknownOption_GivesError()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "gallery", "g.png", "--colour", "red"]);
        Assert.Equal("Unknown option '--colour'.", o.Error);
    }

    [Fact]
    public void NoArguments_MeansNormalStart()
    {
        var o = DiagnosticOptions.Parse([]);
        Assert.Equal(DiagnosticMode.Normal, o.Mode);
        Assert.Null(o.Error);
    }

    [Theory]
    [InlineData(new[] { "--render-png", "gallery" }, "--render-png needs a view and a .png file.")]
    [InlineData(new[] { "--render-png", "gallery", "g.bmp" }, "--render-png needs a view and a .png file.")]
    [InlineData(new[] { "--render-png", "gallery", "g.png", "--theme", "Blue" }, "--theme must be Light or Dark.")]
    [InlineData(new[] { "--render-png", "gallery", "g.png", "--text-size", "25" }, "--text-size must be a number from 10 to 24.")]
    [InlineData(new[] { "--render-png", "gallery", "g.png", "--text-size", "9" }, "--text-size must be a number from 10 to 24.")]
    [InlineData(new[] { "--render-png", "gallery", "g.png", "--text-size" }, "--text-size needs a value.")]
    [InlineData(new[] { "--render-png", "gallery", "g.png", "--width", "wide" }, "--width must be a whole number from 400 to 8000.")]
    [InlineData(new[] { "--theme", "Dark" }, "--theme, --text-size, --width and --height only go with --render-png.")]
    [InlineData(new[] { "--selftest", "everything" }, "--selftest must be all-labs or train.")]
    [InlineData(new[] { "--selftest", "train", "--render-png", "gallery", "g.png" }, "Use either --render-png or --selftest, not both.")]
    public void BadInput_GivesPlainError(string[] args, string error)
    {
        var o = DiagnosticOptions.Parse(args);
        Assert.Equal(error, o.Error);
    }

    [Fact]
    public void ParsesSelfTest()
    {
        var o = DiagnosticOptions.Parse(["--selftest", "all-labs"]);
        Assert.Null(o.Error);
        Assert.Equal(DiagnosticMode.SelfTest, o.Mode);
        Assert.Equal("all-labs", o.SelfTest);
    }

    [Fact]
    public void TextSize_IsReadWithInvariantCulture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var o = DiagnosticOptions.Parse(["--render-png", "gallery", "g.png", "--text-size", "23.5"]);
            Assert.Equal(23.5, o.TextSize);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }

    [Fact]
    public void SeveralViews_InOneLaunch()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "gallery,sources,settings", @"C:\temp\dark.png", "--theme", "Dark"]);

        Assert.Null(o.Error);
        Assert.Equal(new[] { "gallery", "sources", "settings" }, o.Views);
        Assert.Equal(@"C:\temp\dark-gallery.png", o.OutputFileFor("gallery"));
        Assert.Equal(@"C:\temp\dark-settings.png", o.OutputFileFor("settings"));
    }

    [Fact]
    public void OneView_KeepsTheFileName()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "gallery", "g.png"]);
        Assert.Equal(new[] { "gallery" }, o.Views);
        Assert.Equal("g.png", o.OutputFileFor("gallery"));
    }

    [Fact]
    public void ParsesFullLab()
    {
        // Final review I8: the lab pane by itself at its full height, so the bottom of a long lab can be checked at 24 pt.
        var o = DiagnosticOptions.Parse(["--render-png", "output-sampling,learning", "lab.png", "--full-lab", "--theme", "Dark", "--text-size", "24"]);
        Assert.Null(o.Error);
        Assert.True(o.FullLab);
        Assert.Equal(new[] { "output-sampling", "learning" }, o.Views);
        Assert.Equal(24, o.TextSize);
        Assert.False(DiagnosticOptions.Parse(["--render-png", "learning", "lab.png"]).FullLab);
    }

    [Theory]
    [InlineData(new[] { "--full-lab" }, "--full-lab only goes with --render-png and a lesson id.")]
    [InlineData(new[] { "--render-png", "gallery", "g.png", "--full-lab" }, "--full-lab only goes with --render-png and a lesson id.")]
    [InlineData(new[] { "--render-png", "learning,sources", "g.png", "--full-lab" }, "--full-lab only goes with --render-png and a lesson id.")]
    public void FullLab_WithoutALesson_GivesPlainError(string[] args, string error)
    {
        Assert.Equal(error, DiagnosticOptions.Parse(args).Error);
    }

    [Fact]
    public void EmptyViewInList_GivesError()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "gallery,,sources", "g.png"]);
        Assert.Equal("--render-png needs a view and a .png file.", o.Error);
    }

    [Fact]
    public void IdeaMapViews_AreRadialAndLoopOnly()
    {
        var ok = DiagnosticOptions.Parse(["--render-png", "ideamap-loop", "m.png"]);
        Assert.Null(ok.Error);
        Assert.Equal(new[] { "ideamap-loop" }, ok.Views);
        Assert.Null(DiagnosticOptions.Parse(["--render-png", "ideamap-radial,ideamap-loop", "m.png"]).Error);

        Assert.Null(DiagnosticOptions.Parse(["--render-png", "ideamap-radial-chosen,ideamap-loop-chosen,ideamap-loop-learning", "m.png"]).Error);

        var bad = DiagnosticOptions.Parse(["--render-png", "ideamap-sideways", "m.png"]);
        Assert.Equal("Idea map views are ideamap-radial, ideamap-loop, ideamap-radial-chosen, ideamap-loop-chosen and ideamap-loop-learning.", bad.Error);
        Assert.NotNull(DiagnosticOptions.Parse(["--render-png", "ideamap-radial", "m.png", "--full-lab"]).Error);
    }

    [Fact]
    public void RulesVsLearningTrained_IsARenderViewThatTakesFullLab()
    {
        var o = DiagnosticOptions.Parse(["--render-png", DiagnosticOptions.RulesVsLearningTrained, "t.png", "--full-lab", "--width", "700"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "rules-vs-learning-trained" }, o.Views);
        Assert.True(o.FullLab);
    }

    [Fact]
    public void ContentsView_IsAKnownView_ButNotForFullLab()
    {
        var ok = DiagnosticOptions.Parse(["--render-png", "contents,tokens", "c.png"]);
        Assert.Null(ok.Error);
        Assert.Equal(new[] { "contents", "tokens" }, ok.Views);
        Assert.NotNull(DiagnosticOptions.Parse(["--render-png", "contents", "c.png", "--full-lab"]).Error);
    }
}
