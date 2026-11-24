using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class TokensTests
{
    private static string Fables() => File.ReadAllText(Path.Combine(TestEnvironment.Content, "texts", "aesop-fables.txt"));

    private static TokensLabViewModel NewLab() => new(Fables, TimeSpan.Zero);

    [Fact]
    public async Task BothTokenizationsShown()
    {
        var lab = NewLab();
        await lab.BpeReady;
        lab.Text = "the crow";

        Assert.Equal(8, lab.CharacterChips.Count);
        Assert.Equal(new[] { "t", "h", "e", "␣", "c", "r", "o", "w" }, lab.CharacterChips.Select(c => c.Text));
        Assert.All(lab.CharacterChips, c => Assert.True(c.Id > 0));
        Assert.InRange(lab.BpeChips.Count, 1, 7);
        Assert.Equal("the␣crow", string.Concat(lab.BpeChips.Select(c => c.Text)));
        Assert.Contains("8 tokens", lab.CharacterCountText);
        Assert.Contains($"{lab.BpeChips.Count} token", lab.BpeCountText);
        Assert.Contains("200 merges", lab.BpeCountText);
    }

    [Fact]
    public async Task MergesSlider_ChangesBpeTokenCount()
    {
        var lab = NewLab();
        lab.Text = "the fox and the crow";
        await lab.BpeReady;
        int at200 = lab.BpeChips.Count;

        lab.Merges = 0;
        await lab.BpeReady;
        Assert.Equal(20, lab.BpeChips.Count);   // no merges: one piece per character

        lab.Merges = 500;
        await lab.BpeReady;
        Assert.True(lab.BpeChips.Count < at200, $"500 merges gave {lab.BpeChips.Count}, 200 gave {at200}");
        Assert.Equal("the␣fox␣and␣the␣crow", string.Concat(lab.BpeChips.Select(c => c.Text)));
    }

    [Fact]
    public async Task Merges_AreLearnedOncePerValue_AndFastWhenRepeated()
    {
        var lab = NewLab();
        await lab.BpeReady;
        lab.Merges = 300;
        await lab.BpeReady;
        lab.Merges = 200;      // already learned: shown at once, no background work
        Assert.True(lab.BpeReady.IsCompleted);
        Assert.False(lab.IsBpeBusy);
        Assert.Null(lab.BpeStatus);
    }

    [Fact]
    public async Task ChangingMergesQuickly_KeepsOnlyTheLastValue()
    {
        var lab = new TokensLabViewModel(Fables, TimeSpan.FromMilliseconds(100));
        await lab.BpeReady;
        lab.Merges = 50;
        lab.Merges = 120;
        lab.Merges = 80;
        await lab.BpeReady;
        Assert.Contains("80 merges", lab.BpeCountText);
        Assert.False(lab.IsBpeBusy);
    }

    [Fact]
    public async Task EmojiInput_ShowsUnknownChip()
    {
        var lab = NewLab();
        await lab.BpeReady;
        lab.Text = "a\U0001F642b";

        Assert.Equal(3, lab.CharacterChips.Count);
        Assert.True(lab.CharacterChips[1].IsUnknown);
        Assert.Equal("�", lab.CharacterChips[1].Text);
        Assert.Contains(lab.BpeChips, c => c.IsUnknown && c.Text == "�");
        Assert.Null(lab.Message);
    }

    [Fact]
    public async Task EmptyText_ShowsPrompt_NotAnError()
    {
        var lab = NewLab();
        await lab.BpeReady;
        lab.Text = "";
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Empty(lab.CharacterChips);
        Assert.Empty(lab.BpeChips);
    }

    [Fact]
    public async Task CountTheLetters_ShowsThePiecesTheBpeSees()
    {
        var lab = NewLab();
        await lab.BpeReady;
        lab.Word = "strawberry";
        lab.Letter = "r";

        Assert.Equal("strawberry", string.Concat(lab.WordChips.Select(c => c.Text)));
        Assert.True(lab.WordChips.Count < 10);
        Assert.Equal(3, lab.WordChips.Sum(c => int.Parse(c.Detail![..1])));   // three r's, spread over the pieces
        Assert.Contains("10 letters", lab.WordSummary);
        Assert.Contains("3 times", lab.WordSummary);

        lab.Merges = 0;
        await lab.BpeReady;
        Assert.Equal(10, lab.WordChips.Count);
        Assert.Contains("the pieces are the letters", lab.WordSummary);
    }

    [Fact]
    public async Task EnterTypesCrLf_ButIsCountedAsOneToken()
    {
        // Final review I11: the text box types CR LF for Enter; the tokenizer reads it as one new line (D10).
        var lab = NewLab();
        await lab.BpeReady;
        lab.Text = "ab\r\ncd";
        Assert.Equal(5, lab.CharacterChips.Count);
        Assert.StartsWith("5 tokens", lab.CharacterCountText);
        lab.Merges = 0;
        await lab.BpeReady;
        Assert.StartsWith("5 tokens with 0 merges, about 1 character per token", lab.BpeCountText);   // 5 characters in 5 pieces, not 6 in 5
    }

    [Fact]
    public async Task CountTheLetters_CountsOnlyLetters()
    {
        // Final review M9: spaces and digits are characters, not letters.
        var lab = NewLab();
        await lab.BpeReady;
        lab.Word = "a b1";
        Assert.Contains("“a b1” has 4 characters, 2 of them letters.", lab.WordSummary);
        lab.Word = "crow";
        Assert.Contains("“crow” has 4 letters.", lab.WordSummary);
    }

    [Fact]
    public async Task Reset_RestoresDefaults()
    {
        var lab = NewLab();
        await lab.BpeReady;
        lab.Text = "x";
        lab.Word = "y";
        lab.Merges = 10;
        await lab.BpeReady;
        lab.Reset();
        await lab.BpeReady;
        Assert.Equal(TokensLabViewModel.DefaultText, lab.Text);
        Assert.Equal(TokensLabViewModel.DefaultWord, lab.Word);
        Assert.Equal(200, lab.Merges);
        Assert.Contains("200 merges", lab.BpeCountText);
    }

    [Fact]
    public async Task ScreensLightAfterDirectChanges_NotWhenUnchanged()
    {
        var lab = NewLab();
        await lab.BpeReady;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.Text = "the crow";
        Assert.Contains("characters", raised);
        Assert.Contains("pieces", raised);
        raised.Clear();
        lab.Text = "the crow";
        Assert.Empty(raised);

        lab.Merges = 20;
        await lab.BpeReady;
        Assert.Contains("pieces", raised);
        Assert.DoesNotContain("characters", raised);
        raised.Clear();

        lab.Word = "banana";
        Assert.Equal(["word"], raised);
        raised.Clear();
        lab.Word = "banana";
        Assert.Empty(raised);
    }

    [Fact]
    public async Task TheFirstFillOfThePieces_IsABaseline_NotALightUp()
    {
        var lab = NewLab();
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        await lab.BpeReady;
        Assert.Empty(raised);
    }
}
