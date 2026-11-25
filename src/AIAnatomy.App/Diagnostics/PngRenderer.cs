using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AIAnatomy.App.Themes;
using AIAnatomy.App.Views;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App.Diagnostics;

/// <summary>
/// <c>--render-png</c>: draws the window's content into a PNG without showing it on screen, so screens can be checked
/// without a screen capture. The theme and text size apply to this render only; nothing is saved. With <c>--full-lab</c>
/// the lab pane is drawn by itself at its full height (up to 12,000 px), so the bottom of a long lab is in the picture.
/// </summary>
public static class PngRenderer
{
    private const double MaxGalleryHeight = 12000;

    /// <summary>Renders every view named in the options (one or several) with the same theme, text size and window size.</summary>
    public static async Task RenderAsync(DiagnosticOptions options)
    {
        ThemeManager.Apply(options.Theme, options.TextSize);
        // Fresh services (in-memory settings, an empty temporary model folder) for each view, so one picture cannot change
        // the next and none depends on the user's own models.
        foreach (string view in options.Views)
        {
            using var folder = new TemporaryFolder();
            var services = AppServices.Create(new InMemorySettings(new AppSettings(options.TextSize, options.Theme, ScaleVersion: SettingsService.CurrentScaleVersion)), folder.Path);
            await RenderViewAsync(options, services, view, options.OutputFileFor(view));
        }
    }

    private static async Task RenderViewAsync(DiagnosticOptions options, AppServices services, string view, string file)
    {
        var viewModel = services.NewMainViewModel();
        var window = new MainWindow(viewModel);
        if (view == DiagnosticOptions.RulesVsLearningTrained)
        {
            await viewModel.NavigateToLesson("rules-vs-learning");
            if (viewModel.CurrentLab is not RulesVsLearningLabViewModel rules) throw new InvalidOperationException("The rules-vs-learning lesson did not open its lab.");
            rules.ChunksPerRun = 2;
            await rules.LearnCommand.ExecuteAsync(null);
            await rules.LearnCommand.ExecuteAsync(null);
        }
        if (view == DiagnosticOptions.FamilyTreeChosen)
        {
            await viewModel.NavigateToLesson("family-tree");
            if (viewModel.CurrentLab is not FamilyTreeLabViewModel tree) throw new InvalidOperationException("The family-tree lesson did not open its lab.");
            tree.Choose("rules");
        }
        if (view == DiagnosticOptions.ExpertSystemEdited)
        {
            await viewModel.NavigateToLesson("expert-system");
            if (viewModel.CurrentLab is not ExpertSystemLabViewModel rules) throw new InvalidOperationException("The expert-system lesson did not open its lab.");
            foreach (var text in new[] { "if flies then bat", "if has wings bat", "if not mammal then fish", "" })
            {
                rules.AddRule();
                rules.CommitRule(rules.Rules[^1], text);
            }
            rules.Describe("albatross");
        }
        if (view == DiagnosticOptions.SearchMidgame)
        {
            await viewModel.NavigateToLesson("search");
            if (viewModel.CurrentLab is not SearchLabViewModel search) throw new InvalidOperationException("The search lesson did not open its lab.");
            await search.PlayAsync(4);   // you (X) in the centre; the computer replies
            search.LookAhead[0].IsExpanded = true;   // its first option opened: your replies beneath it
        }
        if (view == DiagnosticOptions.SearchWon)
        {
            await viewModel.NavigateToLesson("search");
            if (viewModel.CurrentLab is not SearchLabViewModel search) throw new InvalidOperationException("The search lesson did not open its lab.");
            while (!search.Board.IsOver) await search.PlayAsync(search.Board.EmptyCells[0]);   // you take the lowest free square
        }
        if (view == DiagnosticOptions.ClassicMlNeighbours)
        {
            await viewModel.NavigateToLesson("classic-ml");
            if (viewModel.CurrentLab is not ClassicMlLabViewModel classic) throw new InvalidOperationException("The classic-ml lesson did not open its lab.");
            classic.Method = ClassicMethod.NearestNeighbours;   // on the default set
            classic.K = 1;
        }
        if (view == DiagnosticOptions.RewardTrained)
        {
            await viewModel.NavigateToLesson("learning-by-reward");
            if (viewModel.CurrentLab is not RewardLabViewModel reward) throw new InvalidOperationException("The learning-by-reward lesson did not open its lab.");
            reward.UpdateInterval = TimeSpan.Zero;   // no need to pace a run nobody watches
            await reward.RunCommand.ExecuteAsync(null);   // 300 episodes on the default maze
        }
        var map = view is DiagnosticOptions.RulesVsLearningTrained or DiagnosticOptions.FamilyTreeChosen or DiagnosticOptions.ExpertSystemEdited
            or DiagnosticOptions.SearchMidgame or DiagnosticOptions.SearchWon or DiagnosticOptions.ClassicMlNeighbours
            or DiagnosticOptions.RewardTrained ? null : Show(window, viewModel, services, view);

        if (viewModel.CurrentLab != null) await viewModel.CurrentLab.WorkCompleted;   // e.g. the Tokens lab learning its BPE

        // The window is never shown: its content is laid out and drawn on its own, at exactly the size asked for.
        var content = window.Shell;
        double width = options.Width, height = options.Height;
        await SettleAsync(content, options.Width, height);
        if (view == DiagnosticOptions.ContentsView)
        {
            window.ExpandContents();
            await SettleAsync(content, options.Width, height);
        }
        if (view is DiagnosticOptions.CheckAnswered or DiagnosticOptions.CheckSkipped or DiagnosticOptions.BranchAnchor)
            await ShowLessonPageViewAsync(window, viewModel, services, view, content, options.Width, height);
        if (options.FullLab)
        {
            // The lab pane's width is read from the window as laid out above (stacked or side by side), then the lab is
            // shown by itself and drawn at its full height like the gallery (final review I8).
            window.ShowFullLabPage();
            await SettleAsync(content, options.Width, height);
        }
        if (map != null && view == DiagnosticOptions.IdeaMapRadialProgress)
        {
            // Laid out above; now marks change as they do when lessons are opened and checked. The map must keep its layout
            // (no new measure or Arrange) and show the new marks in place (final review I1, I2).
            await SettleAsync(content, options.Width, height);
            var before = viewModel.IdeaMap.Layout;
            string placeholder = services.Lessons.All.First(l => l.IsPlaceholder).Id;
            services.Progress.MarkOpened("attention");
            services.Progress.MarkChecked("tokens");
            services.Progress.MarkOpened(placeholder);
            await SettleAsync(content, options.Width, height);
            Console.WriteLine($"Marks changed: layout kept = {before != null && ReferenceEquals(before, viewModel.IdeaMap.Layout)}; " +
                string.Join("; ", new[] { "rules-vs-learning", "tokens", "attention", "embeddings", placeholder }
                    .Select(id => viewModel.IdeaMap.Bubbles.Single(b => b.Id == id).AutomationName)));
        }
        if (map != null)
        {
            // The idea map is drawn whole (it scrolls in the window), so the picture shows all of it.
            map.ShowWholeMap();
            await SettleAsync(content, options.Width, height);
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width = Math.Min(MaxGalleryHeight, Math.Max(width, Math.Ceiling(content.DesiredSize.Width)));
            height = Math.Min(MaxGalleryHeight, Math.Max(height, Math.Ceiling(content.DesiredSize.Height)));
            await SettleAsync(content, width, height);
        }
        else if (view == DiagnosticOptions.Gallery || options.FullLab)
        {
            // The gallery (or a full lab) is drawn at its full height so everything is in the picture.
            content.Measure(new Size(options.Width, double.PositiveInfinity));
            height = Math.Min(MaxGalleryHeight, Math.Max(height, Math.Ceiling(content.DesiredSize.Height)));
            await SettleAsync(content, options.Width, height);
        }

        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        await using var stream = File.Create(file);
        encoder.Save(stream);
        Console.WriteLine($"Saved {file} ({(int)width} x {(int)height}, {ThemeManager.Theme}, {options.TextSize} pt){MapSize(map)}.");
    }

    private static string MapSize(IdeaMapView? map) =>
        map?.DataContext is AIAnatomy.Presentation.IdeaMap.IdeaMapViewModel { Layout: { } l }
            ? $"; map layout {Math.Ceiling(l.Width)} x {Math.Ceiling(l.Height)} px" : "";

    /// <summary>
    /// The lesson-page views (final review I3, I4). Check yourself: the Tokens lesson with its first question answered
    /// rightly and its second wrongly (or skipped), scrolled so Check yourself is at the top. Branch: the Learning lesson
    /// opened from its "backprop" branch the way a click in the contents opens it, from the first lesson already on show.
    /// Each prints where the place's top ended up.
    /// </summary>
    private static async Task ShowLessonPageViewAsync(MainWindow window, MainViewModel viewModel, AppServices services, string view,
        FrameworkElement content, double width, double height)
    {
        string anchor;
        if (view == DiagnosticOptions.BranchAnchor)
        {
            var branch = services.Lessons.Get("learning").Branches.Single(b => b.Anchor == "backprop");
            viewModel.OpenBranchCommand.Execute(branch);   // the page scrolls itself when the request arrives
            if (viewModel.CurrentLab != null) await viewModel.CurrentLab.WorkCompleted;
            anchor = branch.Anchor;
            await SettleAsync(content, width, height);
        }
        else
        {
            await viewModel.NavigateToLesson("tokens");
            if (viewModel.CurrentLab != null) await viewModel.CurrentLab.WorkCompleted;   // the BPE, so the page has its final height
            var check = viewModel.CurrentPage?.Check ?? throw new InvalidOperationException("The Tokens lesson has no Check yourself.");
            var questions = services.Lessons.Get("tokens").Questions;
            if (view == DiagnosticOptions.CheckSkipped) check.SkipCommand.Execute(null);
            else
            {
                check.Questions[0].ChooseCommand.Execute(questions[0].CorrectIndex);                          // right
                check.Questions[1].ChooseCommand.Execute((questions[1].CorrectIndex + 1) % questions[1].Answers.Count);   // not quite
            }
            anchor = AIAnatomy.App.Controls.LessonAnchor.Check;
            await SettleAsync(content, width, height);
            window.Page.ScrollTo(anchor);
            await SettleAsync(content, width, height);
        }
        Console.WriteLine(window.Page.AnchorPosition(anchor) is { } at
            ? $"'{anchor}' top is {Math.Round(at.Top)} px below the top of the page{(at.AtEnd ? " (page scrolled to its end)" : "")}."
            : $"'{anchor}' is not on the page.");
    }

    /// <summary>Shows the view; returns the idea map view when that is what was asked for.</summary>
    private static IdeaMapView? Show(MainWindow window, MainViewModel viewModel, AppServices services, string view)
    {
        switch (view)
        {
            case var v when DiagnosticOptions.IsIdeaMapView(v):
                // The window's own idea map page (its progress is in the scratch folder, like the models, so the picture never shows the user's own marks).
                var ideaMap = viewModel.IdeaMap;
                ideaMap.Mode = view.StartsWith("ideamap-loop", StringComparison.Ordinal) ? AIAnatomy.Presentation.IdeaMap.MapMode.Loop : AIAnatomy.Presentation.IdeaMap.MapMode.Radial;
                string? chosen = view switch
                {
                    DiagnosticOptions.IdeaMapRadialChosen => "attention",
                    DiagnosticOptions.IdeaMapLoopChosen => "residual-stream",
                    DiagnosticOptions.IdeaMapLoopLearning => "learning",
                    _ => null,
                };
                if (chosen != null) ideaMap.Choose(chosen);
                viewModel.ShowIdeaMapCommand.Execute(null);
                return window.IdeaMap;
            case DiagnosticOptions.ContentsView: break;   // the first lesson, with the tree opened in RenderViewAsync
            case DiagnosticOptions.CheckAnswered or DiagnosticOptions.CheckSkipped or DiagnosticOptions.BranchAnchor: break;   // after the first layout
            case DiagnosticOptions.Gallery: window.ShowDiagnosticPage(new ControlGallery()); break;
            case DiagnosticOptions.SourcesView: viewModel.ShowSourcesCommand.Execute(null); break;
            case DiagnosticOptions.SettingsView: viewModel.ShowSettingsCommand.Execute(null); break;
            case DiagnosticOptions.SettingsResetQuestion:   // one press of Reset progress: the question shows (re-review N1)
                viewModel.ShowSettingsCommand.Execute(null);
                viewModel.SettingsPage.ResetProgressCommand.Execute(null);
                break;
            default:
                if (services.Lessons.All.All(l => l.Id != view))
                    throw new ArgumentException($"There is no lesson or view called '{view}'. Use a lesson id, contents, gallery, sources, settings, ideamap-radial or ideamap-loop.");
                viewModel.NavigateToLesson(view);
                break;
        }
        return null;
    }

    // Layout, then let the dispatcher finish bindings, flow documents and formulas (Background priority), twice over.
    private static async Task SettleAsync(FrameworkElement content, double width, double height)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();
            await content.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }
}
