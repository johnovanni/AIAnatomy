namespace AIAnatomy.Lessons.Markdown;

public sealed record MarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks);

public abstract record MarkdownBlock;

public sealed record Heading(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record Paragraph(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record BulletList(IReadOnlyList<IReadOnlyList<MarkdownInline>> Items) : MarkdownBlock;

public sealed record FormulaBlock(string Latex) : MarkdownBlock;

public abstract record MarkdownInline;

public sealed record TextRun(string Text) : MarkdownInline;

public sealed record BoldRun(string Text) : MarkdownInline;

public sealed record ItalicRun(string Text) : MarkdownInline;

/// <summary>A link to another lesson, written <c>[text](lesson:id)</c>.</summary>
public sealed record LessonLink(string LessonId, string Text) : MarkdownInline;
