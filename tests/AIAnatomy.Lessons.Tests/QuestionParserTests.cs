namespace AIAnatomy.Lessons.Tests;

public class QuestionParserTests
{
    private static readonly IReadOnlySet<string> Anchors =
        new HashSet<string> { "explanation", "formula", "brains", "lab", "heads" };

    private static IReadOnlyList<Question> Parse(string text) => QuestionParser.Parse(text.Split('\n'), Anchors);

    private const string Good = "? Which token can position 3 attend to?\n- Only position 4\n* Positions 1, 2 and 3\n- Every position\nwhy: The mask.\nsee: heads";

    [Fact]
    public void ValidQuestion_IsRead()
    {
        var q = Assert.Single(Parse(Good));
        Assert.Equal("Which token can position 3 attend to?", q.Prompt);
        Assert.Equal(["Only position 4", "Positions 1, 2 and 3", "Every position"], q.Answers);
        Assert.Equal(1, q.CorrectIndex);
        Assert.Equal("The mask.", q.Why);
        Assert.Equal("heads", q.See);
        Assert.Null(q.CheckedBy);
    }

    [Fact]
    public void Questions_TwoCorrectAnswers_Fail()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Parse(Good.Replace("- Every", "* Every")));
        Assert.Contains("question 1", ex.Message);
    }

    [Fact]
    public void Questions_NoCorrectAnswer_Fail()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Parse(Good.Replace("* Positions", "- Positions")));
        Assert.Contains("question 1", ex.Message);
    }

    [Theory]
    [InlineData("why: The mask.\n")]
    [InlineData("\nsee: heads")]
    public void Questions_MissingWhyOrSee_Fail(string removed)
    {
        var text = Good.Replace(removed.Trim('\n'), "");
        var ex = Assert.Throws<InvalidDataException>(() => Parse("? First?\n- a\n* b\nwhy: x\nsee: lab\n\n" + text));
        Assert.Contains("question 2", ex.Message);
    }

    [Fact]
    public void Questions_SeeMustBeAKnownAnchor()
    {
        Assert.Throws<InvalidDataException>(() => Parse(Good.Replace("see: heads", "see: nowhere")));
        Parse(Good.Replace("see: heads", "see: explanation"));
        Parse(Good);
    }

    [Fact]
    public void Questions_CheckedByIsOptional()
    {
        var q = Assert.Single(Parse(Good + "\nchecked-by-code: Attention_MaskBlocksLaterPositions"));
        Assert.Equal("Attention_MaskBlocksLaterPositions", q.CheckedBy);
    }

    [Fact]
    public void SeveralQuestions_AreSplitOnQuestionMarks()
    {
        Assert.Equal(2, Parse(Good + "\n\n" + Good).Count);
    }
}
