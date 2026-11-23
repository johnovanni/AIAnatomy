using System.Text;

namespace AIAnatomy.Lessons.Markdown;

/// <summary>
/// Parses the small Markdown subset used by the lessons. It never throws: anything it does not
/// recognise (including unclosed markers) stays as literal text.
/// </summary>
public static class MarkdownParser
{
    public static MarkdownDocument Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(markdown)) return new MarkdownDocument(blocks);

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraph = new List<string>();
        var bullets = new List<IReadOnlyList<MarkdownInline>>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            blocks.Add(new Paragraph(ParseInlines(string.Join(" ", paragraph))));
            paragraph.Clear();
        }
        void FlushBullets()
        {
            if (bullets.Count == 0) return;
            blocks.Add(new BulletList(bullets.ToList()));
            bullets.Clear();
        }
        void FlushAll() { FlushParagraph(); FlushBullets(); }

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0) { FlushAll(); continue; }

            if (trimmed.StartsWith("$$", StringComparison.Ordinal) && TryReadFormula(lines, ref i, out var latex))
            {
                FlushAll();
                blocks.Add(new FormulaBlock(latex));
                continue;
            }

            var level = HeadingLevel(trimmed);
            if (level > 0)
            {
                FlushAll();
                blocks.Add(new Heading(level, ParseInlines(trimmed[level..].Trim())));
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushParagraph();
                bullets.Add(ParseInlines(trimmed[2..].Trim()));
                continue;
            }

            FlushBullets();
            paragraph.Add(trimmed);
        }
        FlushAll();
        return new MarkdownDocument(blocks);
    }

    private static int HeadingLevel(string trimmed)
    {
        int n = 0;
        while (n < trimmed.Length && trimmed[n] == '#') n++;
        return n is >= 1 and <= 3 && n < trimmed.Length && trimmed[n] == ' ' ? n : 0;
    }

    // Reads a $$ ... $$ block starting at lines[i]. On success i is left on the closing line.
    private static bool TryReadFormula(string[] lines, ref int i, out string latex)
    {
        latex = "";
        var first = lines[i].Trim()[2..];
        if (first.EndsWith("$$", StringComparison.Ordinal))
        {
            latex = first[..^2].Trim();
            return true;
        }
        var body = new List<string>();
        if (first.Trim().Length > 0) body.Add(first.Trim());
        for (int j = i + 1; j < lines.Length; j++)
        {
            var t = lines[j].Trim();
            if (t.EndsWith("$$", StringComparison.Ordinal))
            {
                var last = t[..^2].Trim();
                if (last.Length > 0) body.Add(last);
                latex = string.Join("\n", body);
                i = j;
                return true;
            }
            body.Add(lines[j].TrimEnd());
        }
        return false;
    }

    private static IReadOnlyList<MarkdownInline> ParseInlines(string text)
    {
        var result = new List<MarkdownInline>();
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0) return;
            result.Add(new TextRun(literal.ToString()));
            literal.Clear();
        }

        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '*' && i + 1 < text.Length && text[i + 1] == '*' && TryEmphasis(text, i, 2, out var bold, out var afterBold))
            {
                FlushLiteral();
                result.Add(new BoldRun(bold));
                i = afterBold;
            }
            else if (c == '*' && TryEmphasis(text, i, 1, out var italic, out var afterItalic))
            {
                FlushLiteral();
                result.Add(new ItalicRun(italic));
                i = afterItalic;
            }
            else if (c == '[' && TryLink(text, i, out var linkText, out var url, out var afterLink))
            {
                FlushLiteral();
                const string prefix = "lesson:";
                if (url.StartsWith(prefix, StringComparison.Ordinal) && url.Length > prefix.Length)
                    result.Add(new LessonLink(url[prefix.Length..].Trim(), linkText));
                else
                    result.Add(new TextRun(linkText));
                i = afterLink;
            }
            else
            {
                literal.Append(c);
                i++;
            }
        }
        FlushLiteral();
        return result;
    }

    // Marker of the given width (1 or 2 asterisks) at start; the content must not touch whitespace at either end.
    private static bool TryEmphasis(string text, int start, int width, out string content, out int next)
    {
        content = "";
        next = start;
        var marker = new string('*', width);
        int from = start + width;
        int close = text.IndexOf(marker, from, StringComparison.Ordinal);
        if (close <= from) return false;
        var inner = text[from..close];
        if (char.IsWhiteSpace(inner[0]) || char.IsWhiteSpace(inner[^1])) return false;
        content = inner;
        next = close + width;
        return true;
    }

    private static bool TryLink(string text, int start, out string linkText, out string url, out int next)
    {
        linkText = url = "";
        next = start;
        int closeBracket = text.IndexOf(']', start + 1);
        if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(') return false;
        int closeParen = text.IndexOf(')', closeBracket + 2);
        if (closeParen < 0) return false;
        linkText = text[(start + 1)..closeBracket];
        url = text[(closeBracket + 2)..closeParen].Trim();
        next = closeParen + 1;
        return true;
    }
}
