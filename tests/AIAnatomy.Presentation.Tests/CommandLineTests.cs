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

        Assert.Null(DiagnosticOptions.Parse(["--render-png", "ideamap-radial-progress", "m.png"]).Error);

        var bad = DiagnosticOptions.Parse(["--render-png", "ideamap-sideways", "m.png"]);
        Assert.Equal("Idea map views are ideamap-radial, ideamap-loop, ideamap-radial-chosen, ideamap-loop-chosen, ideamap-loop-learning and ideamap-radial-progress.", bad.Error);
        Assert.NotNull(DiagnosticOptions.Parse(["--render-png", "ideamap-radial", "m.png", "--full-lab"]).Error);
    }

    [Fact]
    public void CheckYourselfAndBranchViews_AreRenderViews_NotForFullLab()
    {
        // Final review I3 and I4: Check yourself answered (one right, one wrong) and skipped, and a page opened at a branch.
        Assert.Null(DiagnosticOptions.Parse(["--render-png", DiagnosticOptions.SettingsResetQuestion, "s.png"]).Error);   // re-review N1
        Assert.NotNull(DiagnosticOptions.Parse(["--render-png", DiagnosticOptions.SettingsResetQuestion, "s.png", "--full-lab"]).Error);
        var o = DiagnosticOptions.Parse(["--render-png", "check-answered,check-skipped,branch-anchor", "c.png"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { DiagnosticOptions.CheckAnswered, DiagnosticOptions.CheckSkipped, DiagnosticOptions.BranchAnchor }, o.Views);
        Assert.NotNull(DiagnosticOptions.Parse(["--render-png", "check-answered", "c.png", "--full-lab"]).Error);
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
    public void FamilyTreeChosen_IsARenderViewThatTakesFullLab()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "family-tree," + DiagnosticOptions.FamilyTreeChosen, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "family-tree", "family-tree-chosen" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-family-tree-chosen.png"), o.OutputFileFor(DiagnosticOptions.FamilyTreeChosen));
    }

    [Fact]
    public void ExpertSystemEdited_IsARenderViewThatTakesFullLab()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "expert-system," + DiagnosticOptions.ExpertSystemEdited, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "expert-system", "expert-system-edited" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-expert-system-edited.png"), o.OutputFileFor(DiagnosticOptions.ExpertSystemEdited));
    }

    [Fact]
    public void SearchMidgame_IsARenderViewThatTakesFullLab()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "search," + DiagnosticOptions.SearchMidgame, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "search", "search-midgame" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-search-midgame.png"), o.OutputFileFor(DiagnosticOptions.SearchMidgame));
    }

    [Fact]
    public void SearchWon_IsARenderViewThatTakesFullLab()
    {
        var o = DiagnosticOptions.Parse(["--render-png", "search-midgame," + DiagnosticOptions.SearchWon, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "search-midgame", "search-won" }, o.Views);
        Assert.True(o.FullLab);
    }

    [Fact]
    public void ClassicMlNeighbours_IsARenderViewThatTakesFullLab()
    {
        Assert.Equal("classic-ml-neighbours", DiagnosticOptions.ClassicMlNeighbours);
        var o = DiagnosticOptions.Parse(["--render-png", "classic-ml," + DiagnosticOptions.ClassicMlNeighbours, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "classic-ml", "classic-ml-neighbours" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-classic-ml-neighbours.png"), o.OutputFileFor(DiagnosticOptions.ClassicMlNeighbours));
    }

    [Fact]
    public void RewardTrained_IsARenderViewThatTakesFullLab()
    {
        Assert.Equal("reward-trained", DiagnosticOptions.RewardTrained);
        var o = DiagnosticOptions.Parse(["--render-png", "learning-by-reward," + DiagnosticOptions.RewardTrained, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "learning-by-reward", "reward-trained" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-reward-trained.png"), o.OutputFileFor(DiagnosticOptions.RewardTrained));
    }

    [Fact]
    public void MemoryNetworkMeasured_IsARenderViewThatTakesFullLab()
    {
        Assert.Equal("memory-network-measured", DiagnosticOptions.MemoryNetworkMeasured);
        var o = DiagnosticOptions.Parse(["--render-png", "memory-networks," + DiagnosticOptions.MemoryNetworkMeasured, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "memory-networks", "memory-network-measured" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-memory-network-measured.png"), o.OutputFileFor(DiagnosticOptions.MemoryNetworkMeasured));
    }

    [Fact]
    public void FiltersLearned_IsARenderViewThatTakesFullLab()
    {
        Assert.Equal("filters-learned", DiagnosticOptions.FiltersLearned);
        var o = DiagnosticOptions.Parse(["--render-png", "image-filters," + DiagnosticOptions.FiltersLearned, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "image-filters", "filters-learned" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-filters-learned.png"), o.OutputFileFor(DiagnosticOptions.FiltersLearned));
    }

    [Fact]
    public void DigitNetworkSample_IsARenderViewThatTakesFullLab()
    {
        Assert.Equal("digit-network-sample", DiagnosticOptions.DigitNetworkSample);
        var o = DiagnosticOptions.Parse(["--render-png", "image-network," + DiagnosticOptions.DigitNetworkSample, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "image-network", "digit-network-sample" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-digit-network-sample.png"), o.OutputFileFor(DiagnosticOptions.DigitNetworkSample));
    }

    [Fact]
    public void DiffusionShapesDone_IsARenderViewThatTakesFullLab()
    {
        Assert.Equal("diffusion-shapes-done", DiagnosticOptions.DiffusionShapesDone);
        var o = DiagnosticOptions.Parse(["--render-png", "diffusion-shapes," + DiagnosticOptions.DiffusionShapesDone, "t.png", "--full-lab"]);
        Assert.Null(o.Error);
        Assert.Equal(new[] { "diffusion-shapes", "diffusion-shapes-done" }, o.Views);
        Assert.True(o.FullLab);
        Assert.Equal(Path.Combine("", "t-diffusion-shapes-done.png"), o.OutputFileFor(DiagnosticOptions.DiffusionShapesDone));
    }

    [Fact]
    public void IsLabRenderView_IsTrueForTheLabRenderViewsOnly()
    {
        foreach (var v in new[] { "rules-vs-learning-trained", "family-tree-chosen", "expert-system-edited", "search-midgame",
                                  "search-won", "classic-ml-neighbours", "reward-trained", "memory-network-measured", "filters-learned", "digit-network-sample",
                                  "diffusion-shapes-done" })
            Assert.True(DiagnosticOptions.IsLabRenderView(v), v);
        foreach (var v in new[] { "learning-by-reward", "memory-networks", "image-filters", "image-network", "diffusion-shapes", "classic-ml", "gallery", "contents", "ideamap-radial", "check-answered", "" })
            Assert.False(DiagnosticOptions.IsLabRenderView(v), v);
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
