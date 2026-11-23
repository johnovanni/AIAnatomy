using AIAnatomy.Models.Tokenization;

namespace AIAnatomy.Models.Tests;

public class TokenizerTests
{
    private const string Fable = TestTexts.Fable;

    [Fact]
    public void Fable_IsAboutTwoKilobytes()
    {
        Assert.InRange(Fable.Length, 1800, 2600);
    }

    [Fact]
    public void Char_RoundTrip()
    {
        var tokenizer = CharTokenizer.FromText(Fable);
        var ids = tokenizer.Encode(Fable);
        Assert.Equal(Fable.Length, ids.Count);
        Assert.DoesNotContain(0, ids);
        Assert.Equal(Fable, tokenizer.Decode(ids));
    }

    [Fact]
    public void Char_VocabularyIsUnknownThenDistinctCharsInOrdinalOrder()
    {
        var tokenizer = CharTokenizer.FromText("ba ab");
        Assert.Equal(new[] { ' ', 'a', 'b' }, tokenizer.Vocabulary);
        Assert.Equal(4, tokenizer.VocabularySize);
        Assert.Equal(0, tokenizer.UnknownId);
        Assert.Equal(new[] { 3, 2, 1, 2, 3 }, tokenizer.Encode("ba ab"));
    }

    [Fact]
    public void Char_FromVocabulary_GivesSameIdsAsFromText()
    {
        var fromText = CharTokenizer.FromText("hello world");
        var loaded = CharTokenizer.FromVocabulary(fromText.Vocabulary);
        Assert.Equal(fromText.Encode("hello"), loaded.Encode("hello"));
        Assert.Equal(fromText.VocabularySize, loaded.VocabularySize);
    }

    [Fact]
    public void Char_UnknownCharacter_MapsToZeroAndDecodesAsReplacement()
    {
        var tokenizer = CharTokenizer.FromText("hello ");
        var ids = tokenizer.Encode("héllo 🙂");
        Assert.Contains(0, ids);
        Assert.Contains("�", tokenizer.Decode(ids));
        Assert.Equal("�", tokenizer.TokenText(0));
    }

    [Fact]
    public void Char_EmojiCountsAsOneUnknownToken()
    {
        var tokenizer = CharTokenizer.FromText("hello ");
        var ids = tokenizer.Encode("🙂");
        Assert.Equal(new[] { 0 }, ids);
    }

    [Fact]
    public void Char_EmojiInTrainingText_IsStillUnknown()
    {
        var tokenizer = CharTokenizer.FromText("a🙂b");
        Assert.Equal(new[] { 'a', 'b' }, tokenizer.Vocabulary);
        Assert.Equal(new[] { 1, 0, 2 }, tokenizer.Encode("a🙂b"));
    }

    [Fact]
    public void Normalizer_ConvertsCarriageReturns()
    {
        Assert.Equal("a\nb\nc\n", TextNormalizer.Normalize("a\r\nb\rc\n"));
    }

    [Fact]
    public void Char_NormalisesLineEndings()
    {
        var tokenizer = CharTokenizer.FromText("a\nb");
        Assert.Equal(tokenizer.Encode("a\nb"), tokenizer.Encode("a\r\nb"));
    }

    [Fact]
    public void Bpe_FirstMergeOfAaabdaaabac_IsAA()
    {
        var tokenizer = BpeTokenizer.Train("aaabdaaabac", 1);
        Assert.Equal(("a", "a"), tokenizer.Merges[0]);
    }

    [Fact]
    public void Bpe_TieBreak_FirstOccurrenceInTextWins()
    {
        // "xy" and "zw" both occur twice; xy appears first in the text.
        var tokenizer = BpeTokenizer.Train("zwxy xyzw", 1);
        Assert.Equal(("z", "w"), tokenizer.Merges[0]);
        var other = BpeTokenizer.Train("xyzw zwxy", 1);
        Assert.Equal(("x", "y"), other.Merges[0]);
    }

    [Fact]
    public void Bpe_MergesNeverCrossWordBoundary()
    {
        // Words are "a", " b", " a", " b", ... so (a, space) can never merge; only (space, b) / (space, a) can.
        var tokenizer = BpeTokenizer.Train("a b a b a b", 5);
        foreach (var (left, right) in tokenizer.Merges)
            Assert.False((left + right).IndexOf(' ') > 0, $"merge {left}|{right} crosses a word boundary");
        Assert.Equal(new[] { "a", " b", " a", " b" }, tokenizer.Encode("a b a b").Select(tokenizer.TokenText));
    }

    [Fact]
    public void Bpe_NewlineIsItsOwnPiece()
    {
        var tokenizer = BpeTokenizer.Train("ab\nab\nab\n", 10);
        foreach (var (left, right) in tokenizer.Merges)
            Assert.DoesNotContain("\n", left + right);
        Assert.Equal(new[] { "ab", "\n" }, tokenizer.Encode("ab\n").Select(tokenizer.TokenText));
    }

    [Fact]
    public void Bpe_IdsAreUnknownThenBaseCharsThenMerges()
    {
        var tokenizer = BpeTokenizer.Train("aaabdaaabac", 2);
        // base chars a b c d -> ids 1..4; merge 1 = (a,a) -> 5; then (aa,a) ties (a,b) and comes first -> 6
        Assert.Equal("\uFFFD", tokenizer.TokenText(0));
        Assert.Equal("a", tokenizer.TokenText(1));
        Assert.Equal("d", tokenizer.TokenText(4));
        Assert.Equal("aa", tokenizer.TokenText(5));
        Assert.Equal("aaa", tokenizer.TokenText(6));
        Assert.Equal(7, tokenizer.VocabularySize);
        Assert.Equal(new[] { 6, 2, 4, 6, 2, 1, 3 }, tokenizer.Encode("aaabdaaabac"));
    }

    [Fact]
    public void Bpe_UnknownCharacter_EncodesToZero()
    {
        var tokenizer = BpeTokenizer.Train("hello world", 3);
        var ids = tokenizer.Encode("hello 🙂 world");
        Assert.Contains(0, ids);
        Assert.Contains("�", tokenizer.Decode(ids));
    }

    [Fact]
    public void Bpe_RoundTrip()
    {
        var tokenizer = BpeTokenizer.Train(Fable, 50);
        Assert.Equal(50, tokenizer.Merges.Count);
        Assert.Equal(Fable, tokenizer.Decode(tokenizer.Encode(Fable)));
    }

    [Fact]
    public void Bpe_RoundTrip_OfUnseenTextWithKnownCharacters()
    {
        var tokenizer = BpeTokenizer.Train(Fable, 50);
        const string other = "the fox and the hare\nwere in the tea";
        Assert.Equal(other, tokenizer.Decode(tokenizer.Encode(other)));
    }

    [Fact]
    public void Bpe_ReducesTokenCount()
    {
        var tokenizer = BpeTokenizer.Train(Fable, 50);
        Assert.True(tokenizer.Encode(Fable).Count < Fable.Length);
    }

    [Fact]
    public void Bpe_IsDeterministic()
    {
        var a = BpeTokenizer.Train(Fable, 50);
        var b = BpeTokenizer.Train(Fable, 50);
        Assert.Equal(a.Merges, b.Merges);
        Assert.Equal(a.Encode(Fable), b.Encode(Fable));
    }

    [Fact]
    public void Bpe_StopsEarlyWhenNoPairsRemain()
    {
        var tokenizer = BpeTokenizer.Train("ab", 10);
        Assert.Single(tokenizer.Merges);
    }
}
