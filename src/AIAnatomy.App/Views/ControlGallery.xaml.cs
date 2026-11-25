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

        Maps.Maps = Enumerable.Range(0, 12).Select(SampleMap).ToList();

        // The spiral preset, doubled to fill the −3..3 axes; a fixed, made-up amount of noise on each point (not model output).
        var spiral = AIAnatomy.Models.Diffusion.Shapes.Presets[0].Points;
        var target = spiral.Select(p => new System.Windows.Point(p.X * 2, p.Y * 2)).ToList();
        var noisy = target.Select((p, i) => new System.Windows.Point(p.X + 0.45 * Math.Sin(i * 12.9898), p.Y + 0.45 * Math.Cos(i * 78.233))).ToList();
        Cloud.TargetPoints = target;
        Cloud.Points = noisy.Where((_, i) => i % 4 == 0).ToList();
        Cloud.Arrows = Enumerable.Range(0, target.Count).Where(i => i % 40 == 0).Select(i => (target[i], noisy[i])).ToList();
        // A made-up "prediction": the true noise turned a little and shortened, from the same start.
        Cloud.SecondaryArrows = Enumerable.Range(0, target.Count).Where(i => i % 40 == 0).Select(i =>
        {
            var (dx, dy) = (noisy[i].X - target[i].X, noisy[i].Y - target[i].Y);
            return (target[i], new System.Windows.Point(target[i].X + 0.8 * (dx * 0.94 - dy * 0.34), target[i].Y + 0.8 * (dx * 0.34 + dy * 0.94)));
        }).ToList();
        Cloud.HighlightIndices = Enumerable.Range(0, 20).Select(k => k * 12).ToList();

        var few = Enumerable.Range(0, 6).Select(k => SampleMap(k * 2 + 1)).ToList();
        few.Insert(2, new double[0, 0]);   // an empty map is skipped; the others keep their numbers
        MapsUnlimited.Maps = few;
        MapSingle.Maps = [RingMap28()];

        // A hand-made "2" in pad units: a hook running down into a diagonal, then the base as a second stroke.
        var hook = Enumerable.Range(0, 15).Select(k =>
        {
            double a = Math.PI * (1.05 + 1.2 * k / 14);
            return new System.Windows.Point(0.5 + 0.2 * Math.Cos(a), 0.33 + 0.17 * Math.Sin(a));
        }).Append(new System.Windows.Point(0.3, 0.8)).ToArray();
        Pad.Strokes = [hook, [new System.Windows.Point(0.28, 0.8), new System.Windows.Point(0.72, 0.79)]];
        var image = AIAnatomy.Models.Vision.DigitImage.FromStrokes(Controls.DrawingPad.ToDigitStrokes(Pad.Strokes), 8)!;
        var map = new double[8, 8];
        for (int i = 0; i < 64; i++) map[i / 8, i % 8] = image[i];
        PadImage.Maps = [map];
    }

    /// <summary>The page's content, measured by --render-png gallery to make the picture tall enough.</summary>
    public System.Windows.FrameworkElement Page => Panel;

    // An 8 by 8 map of waves at a different angle and strength for each k: values from about −1.5 to 1.5.
    private static double[,] SampleMap(int k)
    {
        var m = new double[8, 8];
        double angle = k * Math.PI / 6, strength = 0.5 + (k % 4) * 0.33;
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
            m[r, c] = strength * Math.Sin((r * Math.Cos(angle) + c * Math.Sin(angle)) * 0.9);
        return m;
    }

    // A 28 by 28 map in −1..1: a ring that is positive inside and negative just outside, like an edge detector's output.
    private static double[,] RingMap28()
    {
        var m = new double[28, 28];
        for (int r = 0; r < 28; r++)
        for (int c = 0; c < 28; c++)
        {
            double d = Math.Sqrt((r - 13.5) * (r - 13.5) + (c - 13.5) * (c - 13.5));
            m[r, c] = Math.Tanh(1.5 * Math.Cos(d * 0.45)) * Math.Exp(-d / 30);
        }
        return m;
    }

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
