using System.Globalization;

namespace AIAnatomy.Presentation;

public enum DiagnosticMode { Normal, RenderPng, SelfTest }

/// <summary>
/// The app's command line, for checks and diagnostics only:
/// <c>--render-png &lt;lessonId|contents|gallery|sources|settings|ideamap-radial|ideamap-loop|ideamap-radial-chosen|ideamap-loop-chosen|ideamap-loop-learning|ideamap-radial-progress|rules-vs-learning-trained|family-tree-chosen|expert-system-edited|search-midgame|search-won|classic-ml-neighbours|reward-trained|memory-network-measured|check-answered|check-skipped|branch-anchor|settings-reset-question&gt; &lt;file.png&gt; [--theme Light|Dark] [--text-size N] [--width 1600] [--height 1000] [--full-lab]</c>
/// or <c>--selftest &lt;all-labs|train&gt;</c>. No arguments means a normal start. A render uses Light at 13 pt unless
/// told otherwise, so pictures do not depend on the user's own settings. Several views may be given as
/// <c>gallery,sources,settings</c>; each then goes to "file-view.png". <c>--full-lab</c> (lesson ids only) draws the lab
/// pane by itself at its full height and at the width it has in the window, so the bottom of a long lab can be checked.
/// Bad input sets <see cref="Error"/>.
/// </summary>
public sealed record DiagnosticOptions(
    DiagnosticMode Mode,
    string? View,
    string? OutputFile,
    string Theme,
    double TextSize,
    int Width,
    int Height,
    string? SelfTest,
    string? Error,
    bool FullLab = false)
{
    public const string Gallery = "gallery";
    public const string ContentsView = "contents";   // the first lesson with every group of the contents tree open
    public const string SourcesView = "sources";
    public const string SettingsView = "settings";
    public const string IdeaMapRadial = "ideamap-radial";
    public const string IdeaMapLoop = "ideamap-loop";
    public const string IdeaMapRadialChosen = "ideamap-radial-chosen";   // Attention chosen
    public const string IdeaMapLoopChosen = "ideamap-loop-chosen";       // Residual stream chosen
    public const string IdeaMapLoopLearning = "ideamap-loop-learning";   // Learning chosen
    /// <summary>Radial, laid out first, then marks changed (one lesson opened, one checked, a placeholder opened) without
    /// a new layout: the marks as the tree shows them, and the layout kept (final review I1, I2).</summary>
    public const string IdeaMapRadialProgress = "ideamap-radial-progress";
    public static bool IsIdeaMapView(string v) =>
        v is IdeaMapRadial or IdeaMapLoop or IdeaMapRadialChosen or IdeaMapLoopChosen or IdeaMapLoopLearning or IdeaMapRadialProgress;
    /// <summary>The Tokens lesson's Check yourself with the first question answered rightly and the second wrongly,
    /// scrolled to the top of the page (final review I4).</summary>
    public const string CheckAnswered = "check-answered";
    /// <summary>The Tokens lesson's Check yourself after Skip (final review I4).</summary>
    public const string CheckSkipped = "check-skipped";
    /// <summary>The Learning lesson opened from its "Backpropagation on a small graph" branch, as a click in the contents
    /// does: the branch's heading should be near the top of the page (final review I3, AA-011).</summary>
    public const string BranchAnchor = "branch-anchor";
    /// <summary>The Settings page after one press of Reset progress: the question and its two buttons (re-review N1).</summary>
    public const string SettingsResetQuestion = "settings-reset-question";
    private static bool IsLessonPageView(string v) => v is CheckAnswered or CheckSkipped or BranchAnchor or SettingsResetQuestion;
    /// <summary>The Rules vs learning lesson after two presses of Learn, so its history shows (a render cannot press Learn).</summary>
    public const string RulesVsLearningTrained = "rules-vs-learning-trained";
    /// <summary>The family tree lesson with the Rules bubble chosen, so its card shows (a render cannot click).</summary>
    public const string FamilyTreeChosen = "family-tree-chosen";
    /// <summary>The expert system with rules added (one breaks the albatross, one has a parse error, one a problem, one is
    /// empty) and the albatross described, so errors and "not used" show (a render cannot type).</summary>
    public const string ExpertSystemEdited = "expert-system-edited";
    /// <summary>The search lesson in mid-game: you (X) in the centre, the computer's reply made, and the first option of its
    /// look-ahead opened, so the replies beneath it show (a render cannot click).</summary>
    public const string SearchMidgame = "search-midgame";
    /// <summary>The search lesson after a finished game: you (X) always took the lowest free square, and the computer (O)
    /// completed the diagonal from top right to bottom left, so the winning line and the status naming it show.</summary>
    public const string SearchWon = "search-won";
    /// <summary>The classic machine learning lesson in nearest-neighbours mode with k = 1 on the default set, so the
    /// neighbour regions carving round the odd points show (a render cannot click).</summary>
    public const string ClassicMlNeighbours = "classic-ml-neighbours";
    /// <summary>The learning-by-reward lesson after one full Run (300 episodes) on the default maze, so values, arrows and
    /// the steps chart show (a render cannot press Run).</summary>
    public const string RewardTrained = "reward-trained";
    /// <summary>The memory networks lesson after its live loss-by-position measurement, with 200 characters written by each
    /// network from the default start text and seed (a render cannot press the button).</summary>
    public const string MemoryNetworkMeasured = "memory-network-measured";
    /// <summary>Whether the view is one of the lab render views above, each a lesson whose lab is set up first.</summary>
    public static bool IsLabRenderView(string v) =>
        v is RulesVsLearningTrained or FamilyTreeChosen or ExpertSystemEdited or SearchMidgame or SearchWon
            or ClassicMlNeighbours or RewardTrained or MemoryNetworkMeasured;
    public const string AllLabsTest = "all-labs";
    public const string TrainTest = "train";

    /// <summary>The views to render: <see cref="View"/> may list several, separated by commas, in one launch.</summary>
    public IReadOnlyList<string> Views => View?.Split(',') ?? [];

    /// <summary>The file for one view: the given file when there is one view, otherwise "name-view.png" beside it.</summary>
    public string OutputFileFor(string view)
    {
        ArgumentNullException.ThrowIfNull(OutputFile);
        if (Views.Count <= 1) return OutputFile;
        string folder = Path.GetDirectoryName(OutputFile) ?? "";
        return Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(OutputFile)}-{view}{Path.GetExtension(OutputFile)}");
    }

    private const double MinTextSize = 10, MaxTextSize = 24;
    private const int MinPixels = 400, MaxPixels = 8000;

    public static DiagnosticOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? view = null, file = null, selfTest = null, error = null;
        string theme = "Light";
        double textSize = 13;
        int width = 1600, height = 1000;
        bool render = false, renderOnlyOption = false, fullLab = false;

        for (int i = 0; i < args.Length && error == null; i++)
        {
            string? Next()
            {
                if (i + 1 < args.Length) return args[++i];
                error = $"{args[i]} needs a value.";
                return null;
            }

            switch (args[i].ToLowerInvariant())
            {
                case "--render-png":
                    render = true;
                    if (i + 2 < args.Length && args[i + 2].EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                        && args[i + 1].Split(',').All(v => v.Trim().Length > 0))
                    {
                        view = args[i + 1];
                        file = args[i + 2];
                        i += 2;
                    }
                    else error = "--render-png needs a view and a .png file.";
                    break;
                case "--selftest":
                    if (Next() is not { } name) break;
                    if (name is AllLabsTest or TrainTest) selfTest = name;
                    else error = $"--selftest must be {AllLabsTest} or {TrainTest}.";
                    break;
                case "--theme":
                    renderOnlyOption = true;
                    if (Next() is not { } t) break;
                    if (t.Equals("Light", StringComparison.OrdinalIgnoreCase)) theme = "Light";
                    else if (t.Equals("Dark", StringComparison.OrdinalIgnoreCase)) theme = "Dark";
                    else error = "--theme must be Light or Dark.";
                    break;
                case "--text-size":
                    renderOnlyOption = true;
                    if (Next() is not { } s) break;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double size)
                        && size >= MinTextSize && size <= MaxTextSize) textSize = size;
                    else error = $"--text-size must be a number from {MinTextSize} to {MaxTextSize}.";
                    break;
                case "--width":
                    renderOnlyOption = true;
                    if (Next() is { } w) width = Pixels("--width", w, ref error);
                    break;
                case "--height":
                    renderOnlyOption = true;
                    if (Next() is { } h) height = Pixels("--height", h, ref error);
                    break;
                case "--full-lab":
                    fullLab = true;
                    break;
                default:
                    error = $"Unknown option '{args[i]}'.";
                    break;
            }
        }

        if (error == null && render && selfTest != null) error = "Use either --render-png or --selftest, not both.";
        if (error == null && renderOnlyOption && !render)
            error = "--theme, --text-size, --width and --height only go with --render-png.";
        if (error == null && render && view!.Split(',').Any(v => v.StartsWith("ideamap-", StringComparison.Ordinal) && !IsIdeaMapView(v)))
            error = "Idea map views are ideamap-radial, ideamap-loop, ideamap-radial-chosen, ideamap-loop-chosen, ideamap-loop-learning and ideamap-radial-progress.";
        if (error == null && fullLab && (!render || view!.Split(',').Any(v => v is Gallery or SourcesView or SettingsView or ContentsView || IsIdeaMapView(v) || IsLessonPageView(v))))
            error = "--full-lab only goes with --render-png and a lesson id.";

        var mode = render ? DiagnosticMode.RenderPng : selfTest != null ? DiagnosticMode.SelfTest : DiagnosticMode.Normal;
        return new DiagnosticOptions(mode, view, file, theme, textSize, width, height, selfTest, error, fullLab);
    }

    private static int Pixels(string option, string text, ref string? error)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= MinPixels && n <= MaxPixels) return n;
        error = $"{option} must be a whole number from {MinPixels} to {MaxPixels}.";
        return 0;
    }
}
