namespace AIAnatomy.Presentation.Tests;

/// <summary>Sets the static ErrorLog.BackgroundFailed, so it runs apart from the other tests.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ErrorLogCollection { public const string Name = "ErrorLog"; }

[Collection(ErrorLogCollection.Name)]
public class ScreenReportingTests
{
    private sealed class TestLab : LabViewModel
    {
        public override string LabId => "test";
        public override void Reset() { }
        public void Report(string screen, object? value) => ReportScreen(screen, value);
        public IDisposable Run() => BeginRun();
    }

    private static (TestLab Lab, List<string> Raised) NewLab()
    {
        var lab = new TestLab();
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => raised.Add(e.Screen);
        return (lab, raised);
    }

    [Fact]
    public void SameValue_DoesNotRaise()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        lab.Report("a", 1);
        Assert.Empty(raised);
    }

    [Fact]
    public void FirstReport_IsBaseline_DoesNotRaise()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        Assert.Empty(raised);
    }

    [Fact]
    public void FirstReportDuringRun_IsBaseline_DoesNotRaise()
    {
        var (lab, raised) = NewLab();
        using (lab.Run()) lab.Report("a", 1);
        Assert.Empty(raised);
        lab.Report("a", 2);
        Assert.Equal(["a"], raised);
    }

    [Fact]
    public void ChangedValue_Raises()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        lab.Report("a", 2);
        Assert.Equal(["a"], raised);
    }

    [Fact]
    public void Collections_AreComparedByTheirContents()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", new[] { 1.5, 2.5 });
        raised.Clear();
        lab.Report("a", new List<double> { 1.5, 2.5 });
        Assert.Empty(raised);
        lab.Report("a", new[] { 1.5, 2.6 });
        Assert.Equal(["a"], raised);
    }

    [Fact]
    public void DuringRun_NothingRaises_ThenOncePerChangedScreenAtEnd()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        lab.Report("b", 1);
        raised.Clear();
        using (lab.Run())
        {
            lab.Report("a", 2);
            lab.Report("a", 3);
            lab.Report("b", 1);
            Assert.Empty(raised);
        }
        Assert.Equal(["a"], raised);
    }

    [Fact]
    public void RunWithNoChange_RaisesNothing()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        raised.Clear();
        using (lab.Run()) lab.Report("a", 1);
        Assert.Empty(raised);
    }

    [Fact]
    public void Fingerprint_DoesNotCollideOnSeparatorCharacters()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", new[] { "a|b" });
        lab.Report("a", new[] { "a", "b" });
        Assert.Equal(["a"], raised);
    }

    [Fact]
    public void ChangeThatIsUndoneInARun_StaysDark()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        using (lab.Run()) { lab.Report("a", 2); lab.Report("a", 1); }
        Assert.Empty(raised);
    }

    [Fact]
    public void NestedRuns_OnlyTheOutermostRaises()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        var outer = lab.Run();
        var inner = lab.Run();
        lab.Report("a", 2);
        inner.Dispose();
        Assert.Empty(raised);
        outer.Dispose();
        Assert.Equal(["a"], raised);
    }

    [Fact]
    public void DisposingARunTwice_RaisesOnceAndDoesNotEndAnOuterRun()
    {
        var (lab, raised) = NewLab();
        lab.Report("a", 1);
        var outer = lab.Run();
        var inner = lab.Run();
        lab.Report("a", 2);
        inner.Dispose();
        inner.Dispose();
        Assert.Empty(raised);   // the outer run is still active
        outer.Dispose();
        outer.Dispose();
        Assert.Equal(["a"], raised);
    }

    [Fact]
    public void AThrowingHandler_DoesNotStopTheOthersOrTheRemainingScreens()
    {
        var lab = new TestLab();
        var raised = new List<string>();
        var logged = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var old = Services.ErrorLog.BackgroundFailed;
        Services.ErrorLog.BackgroundFailed = logged.Enqueue;
        try
        {
            lab.ScreenChanged += (_, _) => throw new InvalidOperationException("boom");
            lab.ScreenChanged += (_, e) => raised.Add(e.Screen);
            lab.Report("a", 1); lab.Report("b", 1);
            using (lab.Run()) { lab.Report("a", 2); lab.Report("b", 2); }
        }
        finally { Services.ErrorLog.BackgroundFailed = old; }
        Assert.Equal(["a", "b"], raised.OrderBy(x => x));
        Assert.Equal(2, logged.Count);
    }

    [Fact]
    public void ReportedScreens_ListsEveryNameReported_InFirstReportOrder()
    {
        // The all-labs self-test compares these with the views' ScreenName values (final review M14).
        var (lab, _) = NewLab();
        Assert.Empty(lab.ReportedScreens);
        lab.Report("b", 1);
        lab.Report("a", 1);
        lab.Report("b", 2);
        Assert.Equal(["b", "a"], lab.ReportedScreens);
    }

    [Theory]
    [InlineData("real-heads", "Real heads screen")]
    [InlineData("crossentropy", "Cross-entropy screen")]
    [InlineData("head-2", "Head 2 screen")]
    [InlineData("lens", "Logit lens screen")]
    [InlineData("backprop", "Backpropagation screen")]
    [InlineData("probabilities", "Probabilities screen")]
    [InlineData("", "Screen")]
    public void ReadableName_TurnsAScreenIdIntoWords(string id, string expected)
    {
        // Final review M11: screen readers hear words, not internal ids.
        Assert.Equal(expected, ScreenNames.Readable(id));
    }
}
