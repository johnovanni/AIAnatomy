using System.Globalization;

namespace AIAnatomy.Presentation;

public enum DiagnosticMode { Normal, RenderPng, SelfTest }

/// <summary>
/// The app's command line, for checks and diagnostics only:
/// <c>--render-png &lt;lessonId|gallery|sources|settings|ideamap-radial|ideamap-loop|ideamap-radial-chosen|ideamap-loop-chosen|ideamap-loop-learning&gt; &lt;file.png&gt; [--theme Light|Dark] [--text-size N] [--width 1600] [--height 1000] [--full-lab]</c>
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
    public const string SourcesView = "sources";
    public const string SettingsView = "settings";
    public const string IdeaMapRadial = "ideamap-radial";
    public const string IdeaMapLoop = "ideamap-loop";
    public const string IdeaMapRadialChosen = "ideamap-radial-chosen";   // Attention chosen
    public const string IdeaMapLoopChosen = "ideamap-loop-chosen";       // Residual stream chosen
    public const string IdeaMapLoopLearning = "ideamap-loop-learning";   // Learning chosen
    public static bool IsIdeaMapView(string v) =>
        v is IdeaMapRadial or IdeaMapLoop or IdeaMapRadialChosen or IdeaMapLoopChosen or IdeaMapLoopLearning;
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
            error = "Idea map views are ideamap-radial, ideamap-loop, ideamap-radial-chosen, ideamap-loop-chosen and ideamap-loop-learning.";
        if (error == null && fullLab && (!render || view!.Split(',').Any(v => v is Gallery or SourcesView or SettingsView || IsIdeaMapView(v))))
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
