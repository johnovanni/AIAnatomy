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
        Assert.Equal(20, o.TextSize);
        Assert.Equal(1600, o.Width);
        Assert.Equal(1000, o.Height);
    }

    [Fact]
    public void ParsesThemeAndTextSize()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "attention", "a.png", "--theme", "dark", "--text-size", "32", "--width", "1280", "--height", "800"]);

        Assert.Null(o.Error);
        Assert.Equal("attention", o.View);
        Assert.Equal("Dark", o.Theme);
        Assert.Equal(32, o.TextSize);
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
    [InlineData(new[] { "--render-png", "gallery", "g.png", "--text-size", "40" }, "--text-size must be a number from 16 to 32.")]
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
            var o = DiagnosticOptions.Parse(["--render-png", "gallery", "g.png", "--text-size", "24.5"]);
            Assert.Equal(24.5, o.TextSize);
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
    public void EmptyViewInList_GivesError()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "gallery,,sources", "g.png"]);
        Assert.Equal("--render-png needs a view and a .png file.", o.Error);
    }
}
