using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AIAnatomy.Lessons.Markdown;
using WpfMath.Controls;
using WpfMath.Parsers;
using MdBlock = AIAnatomy.Lessons.Markdown.MarkdownBlock;
using MdInline = AIAnatomy.Lessons.Markdown.MarkdownInline;
using MdParagraph = AIAnatomy.Lessons.Markdown.Paragraph;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Shows a lesson's <see cref="MarkdownDocument"/> as a read-only flow document: headings, paragraphs, bullet lists,
/// formulas (drawn by WpfMath) and lesson links. A formula that cannot be read is shown as written, in monospace,
/// with a short note. Clicking a lesson link raises <see cref="NavigateRequested"/>. Sizes and colours follow the
/// settings. By default it grows to its full height and leaves scrolling to the pane around it.
/// </summary>
public sealed class MarkdownView : Decorator
{
    public const string FormulaProblemNote = "This formula could not be drawn, so it is shown as written.";

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(nameof(Document), typeof(MarkdownDocument),
        typeof(MarkdownView), new FrameworkPropertyMetadata(null, (d, _) => ((MarkdownView)d).Rebuild()));

    public static readonly RoutedEvent NavigateRequestedEvent = LessonNavigation.NavigateRequestedEvent.AddOwner(typeof(MarkdownView));

    private readonly FlowDocumentScrollViewer _viewer;

    public MarkdownView()
    {
        _viewer = new FlowDocumentScrollViewer
        {
            IsToolBarVisible = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Padding = new Thickness(0),
        };
        // The pane around us scrolls: pass the mouse wheel up instead of swallowing it.
        _viewer.PreviewMouseWheel += (_, e) =>
        {
            if (e.Handled) return;
            e.Handled = true;
            RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
        };
        Child = _viewer;
        Rebuild();
    }

    public MarkdownDocument? Document { get => (MarkdownDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

    public event NavigateRequestedEventHandler NavigateRequested
    {
        add => AddHandler(NavigateRequestedEvent, value);
        remove => RemoveHandler(NavigateRequestedEvent, value);
    }

    /// <summary>The flow document now showing (for checks and diagnostics).</summary>
    public FlowDocument FlowDocument => _viewer.Document;

    private void Rebuild()
    {
        var flow = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Left,
            Background = Brushes.Transparent,
            IsHyphenationEnabled = false,
        };
        flow.SetResourceReference(FlowDocument.FontSizeProperty, "BodyFontSize");
        flow.SetResourceReference(FlowDocument.ForegroundProperty, "TextBrush");
        foreach (var block in Document?.Blocks ?? []) flow.Blocks.Add(Build(block));
        _viewer.Document = flow;
    }

    private Block Build(MdBlock block) => block switch
    {
        Heading h => Heading(h),
        MdParagraph p => WithInlines(new System.Windows.Documents.Paragraph { Margin = new Thickness(0, 0, 0, 12) }, p.Inlines),
        BulletList list => Bullets(list),
        FormulaBlock f => Formula(f.Latex),
        _ => new System.Windows.Documents.Paragraph(new Run(block.ToString())),
    };

    private System.Windows.Documents.Paragraph Heading(Heading h)
    {
        var paragraph = WithInlines(new System.Windows.Documents.Paragraph { Margin = new Thickness(0, 12, 0, 8), FontWeight = FontWeights.SemiBold }, h.Inlines);
        paragraph.SetResourceReference(TextElement.FontSizeProperty, h.Level <= 1 ? "HeadingFontSize" : "SubheadingFontSize");
        return paragraph;
    }

    // Bullets are text ("•" and a tab-like gap with a hanging indent), so they take the text colour in both themes;
    // WPF's own list markers stayed black in the Dark theme.
    private Section Bullets(BulletList list)
    {
        const double indent = 30;
        var result = new Section { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var item in list.Items)
        {
            var paragraph = new System.Windows.Documents.Paragraph { Margin = new Thickness(indent, 0, 0, 4), TextIndent = -indent };
            paragraph.Inlines.Add(new Run("•") { FontWeight = FontWeights.Bold });
            paragraph.Inlines.Add(new InlineUIContainer(new FrameworkElement { Width = indent * 0.6 }));
            result.Blocks.Add(WithInlines(paragraph, item));
        }
        return result;
    }

    /// <summary>Draws the formula, or shows the LaTeX as written with a note if WpfMath cannot read it.</summary>
    private static Block Formula(string latex)
    {
        try
        {
            WpfTeXFormulaParser.Instance.Parse(latex);
        }
        catch (Exception)   // WpfMath reports unreadable LaTeX with several exception types; any of them means "show it as written"
        {
            var fallback = new System.Windows.Documents.Paragraph { Margin = new Thickness(0, 4, 0, 12) };
            fallback.Inlines.Add(new Run(latex) { FontFamily = new FontFamily("Cascadia Mono, Consolas") });
            fallback.Inlines.Add(new LineBreak());
            var note = new Run(FormulaProblemNote) { FontStyle = FontStyles.Italic };
            note.SetResourceReference(TextElement.FontSizeProperty, "SmallFontSize");
            note.SetResourceReference(TextElement.ForegroundProperty, "MutedTextBrush");
            fallback.Inlines.Add(note);
            return fallback;
        }

        var formula = new FormulaControl { Formula = latex, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) };
        formula.SetResourceReference(FormulaControl.ScaleProperty, "FormulaScale");
        formula.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        // Wide formulas scroll sideways instead of being cut off.
        var scroller = new ScrollViewer
        {
            Content = formula,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };
        return new BlockUIContainer(scroller) { Margin = new Thickness(0, 4, 0, 12) };
    }

    private System.Windows.Documents.Paragraph WithInlines(System.Windows.Documents.Paragraph paragraph, IReadOnlyList<MdInline> inlines)
    {
        foreach (var inline in inlines) paragraph.Inlines.Add(Build(inline));
        return paragraph;
    }

    private Inline Build(MdInline inline)
    {
        switch (inline)
        {
            case TextRun t: return new Run(t.Text);
            case BoldRun b: return new Bold(new Run(b.Text));
            case ItalicRun i: return new Italic(new Run(i.Text));
            case LessonLink link:
                var hyperlink = new Hyperlink(new Run(link.Text)) { ToolTip = "Opens that lesson" };
                hyperlink.SetResourceReference(TextElement.ForegroundProperty, "LinkBrush");
                hyperlink.Click += (_, _) => RaiseEvent(new NavigateRequestedEventArgs(NavigateRequestedEvent, this, link.LessonId));
                return hyperlink;
            default: return new Run(inline.ToString());
        }
    }
}
