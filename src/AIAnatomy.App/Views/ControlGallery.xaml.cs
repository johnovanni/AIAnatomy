using AIAnatomy.Lessons.Markdown;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Views;

/// <summary>Every drawn control with fixed sample data (not model output), for visual checks only.</summary>
public partial class ControlGallery : System.Windows.Controls.UserControl
{
    public ControlGallery()
    {
        InitializeComponent();

        string[] tokens = ["The", " fox", " saw", " the", " crow", "."];
        Heat.Values = CausalWeights(tokens.Length);
        Heat.RowLabels = tokens;
        Heat.ColumnLabels = tokens;
        Heat.SelectedCell = new GridCell(3, 1);

        Bars.Items =
        [
            new LabeledValue("cheese", 0.0815), new LabeledValue("crow", 0.4127), new LabeledValue("tree", 0.1904),
            new LabeledValue("fox", 0.2236), new LabeledValue("song", 0.0518), new LabeledValue("grapes", 0.04),
        ];
        Bars.HighlightedLabel = "fox";

        Vector.Values = [0.412, -0.873, 0.051, 1.204, -0.337, 0.689, -1.12, 0.004, 0.275, -0.548, 0.931, -0.066];
        Vector.SelectedIndex = 3;

        Scatter.Points =
        [
            new PlotPoint(-1.8, 1.2, "fox", "animals"), new PlotPoint(-1.5, 1.6, "wolf", "animals"), new PlotPoint(-2.1, 0.7, "crow", "animals"),
            new PlotPoint(-1.1, 1.1, "lion", "animals"), new PlotPoint(1.4, 1.5, "forest", "places"), new PlotPoint(1.9, 0.9, "river", "places"),
            new PlotPoint(1.1, 2.0, "field", "places"), new PlotPoint(0.2, -1.6, "ran", "actions"), new PlotPoint(-0.4, -1.9, "ate", "actions"),
            new PlotPoint(0.7, -1.2, "sang", "actions"), new PlotPoint(0.1, -0.8, "saw", "actions"),
        ];
        Scatter.SelectedIndex = 4;

        var train = Enumerable.Range(0, 31).Select(i => (X: i * 100.0, Y: 1.5 + 2.7 * Math.Exp(-i / 6.0))).ToList();
        var validation = Enumerable.Range(0, 7).Select(i => (X: i * 500.0, Y: 1.7 + 2.6 * Math.Exp(-i * 5 / 6.0))).ToList();
        Lines.Series = [new LineSeries("training loss", train), new LineSeries("validation loss", validation)];

        Network.Nodes =
        [
            new NetworkNode("text", "Text", 0.08, 0.5, "\"The fox\"", null),
            new NetworkNode("tokens", "Tokens", 0.3, 0.5, "7 ids", "tokens"),
            new NetworkNode("embed", "Embeddings", 0.55, 0.2, "64 numbers each", "embeddings"),
            new NetworkNode("attn", "Attention", 0.55, 0.8, "4 heads", "attention"),
            new NetworkNode("out", "Next letter", 0.88, 0.5, "\" \" 41.27%", "output-sampling"),
        ];
        Network.Edges =
        [
            new NetworkEdge("text", "tokens", "split"), new NetworkEdge("tokens", "embed", "look up"),
            new NetworkEdge("embed", "attn", "mix"), new NetworkEdge("attn", "out", "score"),
        ];

        Pipeline.Stages =
        [
            new PipelineStage("Tokens", "The text is split into pieces, each with an id.", null,
                [new LabeledValue("\"T\"", 21), new LabeledValue("\"h\"", 37), new LabeledValue("\"e\"", 34)]),
            new PipelineStage("Token embedding", "Each id is looked up in a table of numbers.", [0.412, -0.873, 0.051, 1.204, -0.337, 0.689, -1.12, 0.004], null),
            new PipelineStage("Probabilities", "The scores are turned into chances that add up to 1.", null,
                [new LabeledValue("\" \"", 0.4127), new LabeledValue("\"r\"", 0.2236)]),
        ];
        Pipeline.SelectedIndex = 1;

        Markdown.Document = new MarkdownDocument(
        [
            new Heading(2, [new TextRun("Attention in one line")]),
            new Paragraph([new TextRun("Each token "), new BoldRun("looks back"), new TextRun(" at earlier tokens and takes a "),
                new ItalicRun("weighted average"), new TextRun(" of what it finds. See "), new LessonLink("attention", "the attention lesson"), new TextRun(".")]),
            new BulletList([[new TextRun("Weights in a row add up to 1.")], [new TextRun("Future positions are blocked.")]]),
            new FormulaBlock(@"\mathrm{softmax}\left(\frac{Q K^{\top}}{\sqrt{d_k}}\right) V"),
            new FormulaBlock(@"\frac{1}{"),
        ]);
    }

    /// <summary>The page's content, measured by --render-png gallery to make the picture tall enough.</summary>
    public System.Windows.FrameworkElement Page => Panel;

    // Rows that add up to 1 over the past and present only; the future is −∞ (blocked), as after a causal mask.
    private static double[,] CausalWeights(int n)
    {
        var w = new double[n, n];
        for (int r = 0; r < n; r++)
        {
            double total = 0;
            for (int c = 0; c <= r; c++) total += 1.0 + ((r * 7 + c * 3) % 5);
            for (int c = 0; c < n; c++) w[r, c] = c <= r ? (1.0 + ((r * 7 + c * 3) % 5)) / total : double.NegativeInfinity;
        }
        return w;
    }
}
