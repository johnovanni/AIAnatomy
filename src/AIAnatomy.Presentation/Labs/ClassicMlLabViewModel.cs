using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AIAnatomy.Models.ClassicMl;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>The two classic methods the lab can show.</summary>
public enum ClassicMethod { DecisionTree, NearestNeighbours }

/// <summary>
/// Lesson 4.4's lab: coloured points of up to three classes on a 0 to 10 plot, and either a decision tree's splits
/// (CART by Gini, grown to the depth slider) or k-nearest-neighbour regions (the k slider). Everything is worked out
/// again at once after each change, on the calling thread: at most 200 points, so a tree takes well under a
/// millisecond and the neighbour grid (60 by 60 squares against 200 points) is about 0.7 million distances.
/// Screens: "regions" (the plot and the accuracy line) and "questions" (the tree's questions).
/// </summary>
public sealed partial class ClassicMlLabViewModel : LabViewModel
{
    public const string Id = "ClassicMlLab";
    public const double PlotMin = 0, PlotMax = 10;
    public const int MaxPoints = 200, GridCells = 60, DefaultDepth = 3, MaxDepth = 10, DefaultK = 3, MaxK = 15;
    public const int ClassCount = 3;

    /// <summary>A right-click (or Delete) removes the nearest point only if it is at most this far away, in plot units.</summary>
    public const double RemoveRadius = 0.3;

    public const string DefaultStarter = "Two groups with odd points";
    public const string EmptyMessage = "Add points of at least one class to see regions.";
    public const string FullMessage = "The plot holds at most 200 points.";
    public const string TreeNote = "The tree asks these questions about any place on the plot, starting at the top:";
    public const string NeighboursNote =
        "Nearest neighbours asks no questions: it compares distances. A place gets the class most common among the k stored points nearest to it.";

    private const string RegionsScreen = "regions", QuestionsScreen = "questions";

    private bool _quiet;
    private string? _refusal;

    public ClassicMlLabViewModel()
    {
        StarterNames = StarterSets.All.Select(s => s.Name).ToList();
        Points.CollectionChanged += OnPointsChanged;
        LoadDefaults();
        Recompute();   // also reports the baselines
    }

    public override string LabId => Id;

    /// <summary>The training points, in the order they were added.</summary>
    public ObservableCollection<LabeledPoint> Points { get; } = new();

    /// <summary>The starter sets' names, in <see cref="StarterSets.All"/> order.</summary>
    public IReadOnlyList<string> StarterNames { get; }

    /// <summary>The starter set loaded last (the points may have been changed since); null after Clear.</summary>
    [ObservableProperty] private string? starterName;

    /// <summary>The class (0 to 2, shown as A to C) the next added point gets.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsClassA), nameof(IsClassB), nameof(IsClassC))] private int newPointLabel;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDecisionTree), nameof(IsNearestNeighbours))]
    private ClassicMethod method = ClassicMethod.DecisionTree;

    /// <summary>The tree's maximum depth: 1 to <see cref="MaxDepth"/>.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DepthText))] private int depth = DefaultDepth;

    /// <summary>How many nearest points vote: 1 to <see cref="MaxK"/>.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(KText))] private int k = DefaultK;

    /// <summary>Tree: the leaf rectangles, which tile the plot. Neighbours: <see cref="GridCells"/> × <see cref="GridCells"/>
    /// squares, each with the vote at its centre. Empty when there are no points.</summary>
    [ObservableProperty] private IReadOnlyList<Region> regions = [];

    /// <summary>The tree's splits as lines on the plot, in the order the questions list them: a split on x is a line at
    /// x = At from y = From to y = To; a split on y runs from x = From to x = To. Empty for nearest neighbours.</summary>
    [ObservableProperty] private IReadOnlyList<(Axis Axis, double At, double From, double To)> splitLines = [];

    /// <summary>The tree's questions, one line each, indented two spaces per level: "Is x < 3?", "  yes → class A",
    /// "  no: Is y < 6.500?". Empty for nearest neighbours and for an empty plot.</summary>
    [ObservableProperty] private IReadOnlyList<string> questions = [];

    /// <summary>The line above the questions: what the tree does, or that nearest neighbours asks none.</summary>
    [ObservableProperty] private string questionsNote = "";

    /// <summary>"Right on 18 of 20 training points (90%)."; empty when there are no points.</summary>
    [ObservableProperty] private string accuracyText = "";

    /// <summary><see cref="EmptyMessage"/> on an empty plot, <see cref="FullMessage"/> after a point is refused, else null.</summary>
    [ObservableProperty] private string? message;

    /// <summary>What the last add or remove did, in words ("Added class A at (2.5, 3)."), for the eyes and screen readers.</summary>
    [ObservableProperty] private string actionText = "";

    public bool IsDecisionTree
    {
        get => Method == ClassicMethod.DecisionTree;
        set { if (value) Method = ClassicMethod.DecisionTree; }
    }

    public bool IsNearestNeighbours
    {
        get => Method == ClassicMethod.NearestNeighbours;
        set { if (value) Method = ClassicMethod.NearestNeighbours; }
    }

    /// <summary>For the class chooser's three radio buttons.</summary>
    public bool IsClassA { get => NewPointLabel == 0; set { if (value) NewPointLabel = 0; } }
    public bool IsClassB { get => NewPointLabel == 1; set { if (value) NewPointLabel = 1; } }
    public bool IsClassC { get => NewPointLabel == 2; set { if (value) NewPointLabel = 2; } }

    public string DepthText => $"Depth: {Depth}";
    public string KText => $"k = {K}";

    /// <summary>"A", "B" or "C".</summary>
    public static string ClassName(int label) => ((char)('A' + label)).ToString();

    /// <summary>The marker of each class, so colour is never the only sign: circle, square, triangle.</summary>
    public static string ShapeName(int label) => label switch { 0 => "circle", 1 => "square", _ => "triangle" };

    /// <summary>"class A at (2.5, 3)".</summary>
    public static string PointText(LabeledPoint p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return $"class {ClassName(p.Label)} at ({NumberText.Tick(p.X)}, {NumberText.Tick(p.Y)})";
    }

    /// <summary>Replaces the points with a starter set.</summary>
    [RelayCommand]
    public void LoadStarter(string name)
    {
        var set = StarterSets.All.FirstOrDefault(s => s.Name == name);
        if (set.Points == null) throw new ArgumentException($"There is no starter set called '{name}'.", nameof(name));
        ReplacePoints(set.Points);
        StarterName = name;
        ActionText = "";
        Recompute();
    }

    /// <summary>Adds a point of <see cref="NewPointLabel"/>'s class, clamped to the plot and rounded to one decimal (like
    /// the starter sets). False, with <see cref="FullMessage"/>, when the plot already holds <see cref="MaxPoints"/>.</summary>
    public bool AddPoint(double x, double y)
    {
        if (Points.Count >= MaxPoints)
        {
            _refusal = FullMessage;
            Message = FullMessage;
            return false;
        }
        var point = new LabeledPoint(OnPlot(x), OnPlot(y), NewPointLabel);
        Quietly(() => Points.Add(point));
        ActionText = $"Added {PointText(point)}.";
        Recompute();
        return true;
    }

    /// <summary>Removes the point nearest (x, y) if one is within <see cref="RemoveRadius"/>; equally near points go in
    /// list order. False if none is that near.</summary>
    public bool RemoveNearest(double x, double y)
    {
        int best = -1;
        double bestSquared = RemoveRadius * RemoveRadius + 1e-12;   // a point exactly 0.3 away counts
        for (int i = 0; i < Points.Count; i++)
        {
            double dx = Points[i].X - x, dy = Points[i].Y - y, d = dx * dx + dy * dy;
            if (d < bestSquared) { best = i; bestSquared = d; }
        }
        if (best < 0)
        {
            ActionText = $"No point within {NumberText.Tick(RemoveRadius)} of ({NumberText.Tick(Math.Round(x, 2))}, {NumberText.Tick(Math.Round(y, 2))}) to remove.";
            return false;
        }
        var removed = Points[best];
        Quietly(() => Points.RemoveAt(best));
        ActionText = $"Removed {PointText(removed)}.";
        Recompute();
        return true;
    }

    [RelayCommand]
    public void Clear()
    {
        Quietly(Points.Clear);
        StarterName = null;
        ActionText = "";
        Recompute();
    }

    public override void Reset()
    {
        LoadDefaults();
        Recompute();
    }

    private void LoadDefaults()
    {
        _quiet = true;
        try
        {
            ReplacePoints(StarterSets.All.Single(s => s.Name == DefaultStarter).Points);
            StarterName = DefaultStarter;
            Method = ClassicMethod.DecisionTree;
            Depth = DefaultDepth;
            K = DefaultK;
            NewPointLabel = 0;
            ActionText = "";
        }
        finally { _quiet = false; }
    }

    private void ReplacePoints(IEnumerable<LabeledPoint> points) => Quietly(() =>
    {
        Points.Clear();
        foreach (var p in points) Points.Add(p);
    });

    private void Quietly(Action change)
    {
        bool was = _quiet;
        _quiet = true;
        try { change(); }
        finally { _quiet = was; }
    }

    private static double OnPlot(double v) =>
        double.IsNaN(v) ? PlotMin : Math.Round(Math.Clamp(v, PlotMin, PlotMax), 1, MidpointRounding.AwayFromZero);

    // A change made straight to the collection (not through the methods above) is worked out too.
    private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_quiet) Recompute();
    }

    partial void OnNewPointLabelChanged(int value)
    {
        if (value is < 0 or >= ClassCount) NewPointLabel = Math.Clamp(value, 0, ClassCount - 1);
    }

    partial void OnMethodChanged(ClassicMethod value) => Recompute();

    partial void OnDepthChanged(int value)
    {
        if (value is < 1 or > MaxDepth) { Depth = Math.Clamp(value, 1, MaxDepth); return; }
        Recompute();
    }

    partial void OnKChanged(int value)
    {
        if (value is < 1 or > MaxK) { K = Math.Clamp(value, 1, MaxK); return; }
        Recompute();
    }

    private void Recompute()
    {
        if (_quiet) return;
        var points = Points.ToArray();
        if (points.Length < MaxPoints) _refusal = null;
        if (points.Length == 0)
        {
            Regions = [];
            SplitLines = [];
            Questions = [];
            QuestionsNote = EmptyMessage;
            AccuracyText = "";
            Message = EmptyMessage;
        }
        else
        {
            int right;
            if (Method == ClassicMethod.DecisionTree)
            {
                var tree = DecisionTree.Grow(points, Depth);
                Regions = DecisionTree.Regions(tree, PlotMin, PlotMax, PlotMin, PlotMax);
                var lines = new List<(Axis, double, double, double)>();
                var questions = new List<string>();
                Describe(tree, PlotMin, PlotMax, PlotMin, PlotMax, 0, "", lines, questions);
                SplitLines = lines;
                Questions = questions;
                QuestionsNote = TreeNote;
                right = points.Count(p => DecisionTree.Predict(tree, p.X, p.Y) == p.Label);
            }
            else
            {
                Regions = NeighbourGrid(points, K);
                SplitLines = [];
                Questions = [];
                QuestionsNote = NeighboursNote;
                right = points.Count(p => NearestNeighbours.Classify(points, p.X, p.Y, K) == p.Label);
            }
            int n = points.Length;
            // Rounded down, so 100% means every point and 199 of 200 shows as 99%.
            AccuracyText = $"Right on {right} of {n} training {(n == 1 ? "point" : "points")} ({100 * right / n}%).";
            Message = _refusal;
        }

        ReportScreen(RegionsScreen, new[] { Method.ToString(), AccuracyText }
            .Concat(points.Select(p => $"{p.X},{p.Y},{p.Label}"))
            .Concat(SplitLines.Select(l => $"{l.Axis},{l.At},{l.From},{l.To}"))
            .Append(string.Concat(Regions.Select(r => r.Label)))
            .ToArray());
        ReportScreen(QuestionsScreen, new[] { QuestionsNote }.Concat(Questions).ToArray());
    }

    private static IReadOnlyList<Region> NeighbourGrid(LabeledPoint[] points, int k)
    {
        double side = (PlotMax - PlotMin) / GridCells;
        var cells = new Region[GridCells * GridCells];
        for (int row = 0; row < GridCells; row++)
        {
            double y0 = PlotMin + row * side, y1 = row == GridCells - 1 ? PlotMax : PlotMin + (row + 1) * side;
            for (int col = 0; col < GridCells; col++)
            {
                double x0 = PlotMin + col * side, x1 = col == GridCells - 1 ? PlotMax : PlotMin + (col + 1) * side;
                cells[row * GridCells + col] = new Region(x0, x1, y0, y1, NearestNeighbours.Classify(points, (x0 + x1) / 2, (y0 + y1) / 2, k));
            }
        }
        return cells;
    }

    /// <summary>
    /// Walks the tree as <see cref="DecisionTree.Regions"/> does (a split outside the box it reaches is skipped, since
    /// only one side of it can happen there), writing each question and leaf and the line each split draws.
    /// </summary>
    private static void Describe(TreeNode node, double x0, double x1, double y0, double y1, int level, string answer,
        List<(Axis, double, double, double)> lines, List<string> questions)
    {
        string indent = new(' ', 2 * level);
        if (node is Leaf leaf)
        {
            questions.Add(level == 0 && answer.Length == 0
                ? $"No questions: everywhere → class {ClassName(leaf.Label)}"
                : $"{indent}{answer} → class {ClassName(leaf.Label)}");
            return;
        }
        var s = (SplitNode)node;
        double lo = s.Axis == Axis.X ? x0 : y0, hi = s.Axis == Axis.X ? x1 : y1, t = s.Threshold;
        if (t <= lo) { Describe(s.AtOrAbove, x0, x1, y0, y1, level, answer, lines, questions); return; }
        if (t >= hi) { Describe(s.Below, x0, x1, y0, y1, level, answer, lines, questions); return; }

        string axis = s.Axis == Axis.X ? "x" : "y";
        string question = $"Is {axis} < {NumberText.Short(t)}?";
        questions.Add(answer.Length == 0 ? indent + question : $"{indent}{answer}: {question}");
        if (s.Axis == Axis.X)
        {
            lines.Add((Axis.X, t, y0, y1));
            Describe(s.Below, x0, t, y0, y1, level + 1, "yes", lines, questions);
            Describe(s.AtOrAbove, t, x1, y0, y1, level + 1, "no", lines, questions);
        }
        else
        {
            lines.Add((Axis.Y, t, x0, x1));
            Describe(s.Below, x0, x1, y0, t, level + 1, "yes", lines, questions);
            Describe(s.AtOrAbove, x0, x1, t, y1, level + 1, "no", lines, questions);
        }
    }
}
