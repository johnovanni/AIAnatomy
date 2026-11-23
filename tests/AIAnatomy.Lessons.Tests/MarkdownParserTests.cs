using AIAnatomy.Lessons.Markdown;

namespace AIAnatomy.Lessons.Tests;

public class MarkdownParserTests
{
    [Fact]
    public void Parses_BoldItalicLink()
    {
        var doc = MarkdownParser.Parse("Some **bold** and *italic* and [attention](lesson:attention) and [web](https://example.com).");
        var para = Assert.IsType<Paragraph>(Assert.Single(doc.Blocks));
        Assert.Equal(new MarkdownInline[]
        {
            new TextRun("Some "), new BoldRun("bold"), new TextRun(" and "), new ItalicRun("italic"),
            new TextRun(" and "), new LessonLink("attention", "attention"), new TextRun(" and "),
            new TextRun("web"), new TextRun("."),
        }, para.Inlines.ToArray());
    }

    [Fact]
    public void Parses_FormulaBlock()
    {
        var doc = MarkdownParser.Parse("Before\n\n$$\na = b\n+ c\n$$\n\nAfter");
        Assert.Equal(3, doc.Blocks.Count);
        var f = Assert.IsType<FormulaBlock>(doc.Blocks[1]);
        Assert.Equal("a = b\n+ c", f.Latex);
    }

    [Fact]
    public void Parses_SingleLineFormulaBlock()
    {
        var f = Assert.IsType<FormulaBlock>(Assert.Single(MarkdownParser.Parse("$$x^2$$").Blocks));
        Assert.Equal("x^2", f.Latex);
    }

    [Fact]
    public void Parses_BulletList()
    {
        var doc = MarkdownParser.Parse("- one\n- **two**\n- three");
        var list = Assert.IsType<BulletList>(Assert.Single(doc.Blocks));
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(new MarkdownInline[] { new BoldRun("two") }, list.Items[1].ToArray());
    }

    [Fact]
    public void Parses_Headings()
    {
        var doc = MarkdownParser.Parse("# One\n\n## Two\n\n### Three\n\n#### Four is text");
        Assert.Equal(1, Assert.IsType<Heading>(doc.Blocks[0]).Level);
        Assert.Equal(2, Assert.IsType<Heading>(doc.Blocks[1]).Level);
        Assert.Equal(3, Assert.IsType<Heading>(doc.Blocks[2]).Level);
        Assert.IsType<Paragraph>(doc.Blocks[3]);
    }

    [Theory]
    [InlineData("an **unclosed bold")]
    [InlineData("an *unclosed italic")]
    [InlineData("a [broken link](lesson:x")]
    [InlineData("a [no target] here")]
    [InlineData("2 * 3 * 4")]
    public void UnclosedMarkers_AreLiteralText(string input)
    {
        var para = Assert.IsType<Paragraph>(Assert.Single(MarkdownParser.Parse(input).Blocks));
        Assert.Equal(input, string.Concat(para.Inlines.Cast<TextRun>().Select(t => t.Text)));
    }

    [Fact]
    public void UnclosedFormula_IsParagraphText()
    {
        var doc = MarkdownParser.Parse("$$\nx = 1");
        Assert.IsType<Paragraph>(Assert.Single(doc.Blocks));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n\n")]
    [InlineData("****")]
    [InlineData("**")]
    [InlineData("*")]
    [InlineData("[](lesson:)")]
    [InlineData("[")]
    [InlineData("](")]
    [InlineData("$$")]
    [InlineData("- ")]
    [InlineData("#")]
    [InlineData("# ")]
    public void OddInput_DoesNotThrow(string input) => Assert.NotNull(MarkdownParser.Parse(input));

    [Fact]
    public void NullInput_GivesEmptyDocument() => Assert.Empty(MarkdownParser.Parse(null!).Blocks);

    [Fact]
    public void WindowsLineEndings_AreHandled()
    {
        var doc = MarkdownParser.Parse("# T\r\n\r\ntext\r\nmore\r\n");
        Assert.Equal(2, doc.Blocks.Count);
    }
}
