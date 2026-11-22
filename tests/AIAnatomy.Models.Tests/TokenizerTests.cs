using AIAnatomy.Models.Tokenization;

namespace AIAnatomy.Models.Tests;

public class TokenizerTests
{
    // About 2 KB of original fable-style prose (the bundled fables arrive in a later task).
    private const string Fable =
        "A fox once lived at the edge of a wide green meadow. Every morning he walked along the old stone wall, " +
        "looking for something good to eat. One day he saw a crow sitting on a branch above him, and in her beak " +
        "was a large piece of yellow cheese.\n" +
        "\"What a fine bird you are,\" said the fox. \"Your feathers shine like polished coal, and I am sure your " +
        "voice must be the finest in the whole forest. Would you not sing for me, just one song?\"\n" +
        "The crow, who loved to hear herself praised, opened her beak to sing. Down fell the cheese, and the fox " +
        "caught it before it touched the ground. \"Thank you,\" he said with a grin. \"Here is a piece of advice " +
        "in return: never trust a person who praises you too much.\"\n" +
        "Meanwhile, in the village below, a slow old tortoise was setting out to visit his cousin on the far side " +
        "of the hill. A hare laughed at him and said, \"You will be there by next winter, if you are lucky.\" " +
        "The tortoise did not answer. He simply kept walking, one step after another, through the heat of the day " +
        "and the cool of the evening.\n" +
        "The hare, certain that he had plenty of time, lay down under a tree and fell fast asleep. When he woke, " +
        "the sun was low, and the tortoise was already sitting at his cousin's door, drinking tea. The hare ran " +
        "as fast as he could, but it was too late. Slow and steady wins the race, the tortoise said, and the " +
        "hare had nothing to say in reply.\n" +
        "A little ant, working hard all summer, carried grain to her nest while a cricket sang in the sun. When " +
        "the cold days came, the cricket had no food and no home, and he knocked at the ant's door. The ant " +
        "gave him a seat by the fire, but she also gave him a lesson: it is wise to prepare for tomorrow today.\n" +
        "An old farmer had three sons who quarrelled from morning until night. One evening he asked each of them " +
        "to break a single stick, and each did so with ease. Then he tied three sticks together and asked them to " +
        "try again, but not one of the sons could bend the bundle. \"Alone you are weak,\" said the farmer, " +
        "\"but together you are strong.\"\n";

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
