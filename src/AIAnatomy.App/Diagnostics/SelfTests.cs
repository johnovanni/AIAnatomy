using System.Windows;
using AIAnatomy.App.Themes;
using AIAnatomy.App.Views;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.App.Diagnostics;

/// <summary>
/// <c>--selftest</c>: scripted checks that run without showing a window. Each returns the exit code. They use in-memory
/// settings and an empty temporary model folder, so the user's settings and models are never read or changed.
/// </summary>
public static class SelfTests
{
    public const int TrainSteps = 5;
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(5);

    public static Task<int> RunAsync(DiagnosticOptions options) => options.SelfTest switch
    {
        DiagnosticOptions.AllLabsTest => AllLabsAsync(),
        DiagnosticOptions.TrainTest => TrainAsync(),
        _ => Task.FromResult(Fail($"Unknown self-test '{options.SelfTest}'.")),
    };

    /// <summary>
    /// Opens every lesson, creates its lab, checks a view exists for it, that every branch anchor is on the page and scrolls
    /// to the top, and that the view's screen names are the ones the lab reports, then calls Reset (waiting for any work
    /// that starts) and Deactivate. Fails if any of that throws or does not hold, or if a lesson names a lab that is not
    /// registered.
    /// </summary>
    private static async Task<int> AllLabsAsync()
    {
        ThemeManager.Apply("Light", 13);
        using var folder = new TemporaryFolder();
        var services = AppServices.Create(new InMemorySettings(new AppSettings()), folder.Path);
        var viewModel = services.NewMainViewModel();
        var window = new MainWindow(viewModel);
        var missing = new List<string>();
        int checkedLabs = 0;
        foreach (var lesson in services.Lessons.All)
        {
            if (lesson.LabId != ComingLaterLabViewModel.Id && !viewModel.Labs.IsRegistered(lesson.LabId))
            {
                missing.Add($"{lesson.Id} ({lesson.LabId})");
                Console.WriteLine($"{lesson.Id}: {lesson.LabId} is not registered.");
                continue;
            }
            try
            {
                await viewModel.NavigateToLesson(lesson.Id);
                var lab = viewModel.CurrentLab ?? throw new InvalidOperationException("it has no lab.");
                if (window.TryFindResource(new DataTemplateKey(lab.GetType())) == null)
                    return Fail($"Lesson '{lesson.Id}': there is no view for {lab.GetType().Name}.");
                if (await MissingAnchorAsync(window, lesson) is { } anchor)
                    return Fail($"Lesson '{lesson.Id}': the branch anchor '{anchor}' is not in the {lab.GetType().Name} view.");
                if (await MisplacedAnchorAsync(window, lesson) is { } misplaced)
                    return Fail($"Lesson '{lesson.Id}': {misplaced}");
                await lab.WorkCompleted.WaitAsync(Patience);
                if (await ScreenNameMismatchAsync(window, lab) is { } mismatch)
                    return Fail($"Lesson '{lesson.Id}' ({lab.GetType().Name}): {mismatch}");
                lab.Reset();
                await lab.WorkCompleted.WaitAsync(Patience);
                if (lab is RulesVsLearningLabViewModel rules)
                {
                    rules.ChunksPerRun = 2;
                    await rules.LearnCommand.ExecuteAsync(null).WaitAsync(Patience);
                    if (rules.History.Count != 2)
                        return Fail($"Lesson '{lesson.Id}': after one press of Learn the history has {rules.History.Count} entries, not 2.");
                    rules.Reset();
                }
                lab.Deactivate();
                checkedLabs++;
                Console.WriteLine($"{lesson.Id}: {lab.LabId} created, reset and deactivated.");
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return Fail($"Lesson '{lesson.Id}' ({lesson.LabId}): {e.GetType().Name}: {e.Message}");
            }
        }
        if (missing.Count > 0)
            return Fail($"{checkedLabs} labs passed, but these lessons name a lab that is not registered: {string.Join(", ", missing)}.");
        Console.WriteLine($"All {checkedLabs} labs created, reset and deactivated.");
        return 0;
    }

    /// <summary>
    /// The first branch anchor of <paramref name="lesson"/> that is not on its page (the lab view is laid out first, in a
    /// window that is never shown), or null when every one is there. Generic: it covers every lesson's branches.
    /// </summary>
    private static async Task<string?> MissingAnchorAsync(MainWindow window, AIAnatomy.Lessons.Lesson lesson)
    {
        if (lesson.Branches.Count == 0) return null;
        await LayOutAsync(window);
        return lesson.Branches.Select(b => b.Anchor).FirstOrDefault(a => AIAnatomy.App.Controls.LessonAnchor.Find(window.Shell, a) == null);
    }

    /// <summary>
    /// Scrolls the page to each branch anchor, as a click on the branch does, and says which one did not end up with its top
    /// near the top of the page (final review I3, AA-011). A place near the page's end may stay lower, with the page
    /// scrolled to its end. Null when every one is in place.
    /// </summary>
    private static async Task<string?> MisplacedAnchorAsync(MainWindow window, AIAnatomy.Lessons.Lesson lesson)
    {
        foreach (var branch in lesson.Branches)
        {
            window.Page.ScrollTo(branch.Anchor);
            await LayOutAsync(window);
            if (window.Page.AnchorPosition(branch.Anchor) is not { } at)
                return $"the branch anchor '{branch.Anchor}' could not be scrolled to.";
            bool atTop = Math.Abs(at.Top - LessonPageView.AnchorGap) <= 2;
            if (!atTop && !(at.AtEnd && at.Top >= 0))
                return $"after scrolling to '{branch.Anchor}' its top is {Math.Round(at.Top)} px below the top of the page, not {LessonPageView.AnchorGap}.";
            Console.WriteLine($"  {branch.Anchor}: top at {Math.Round(at.Top)} px{(atTop ? "" : " (page at its end)")}");
        }
        return null;
    }

    /// <summary>
    /// Compares the <c>ScreenName</c>s of the screens on the lesson page with the screen names the lab has reported: a name
    /// in the view that the lab never reports is a screen that can never light (a typo), and a name the lab reports that no
    /// screen shows lights nothing (final review M14). Null when they agree.
    /// </summary>
    private static async Task<string?> ScreenNameMismatchAsync(MainWindow window, LabViewModel lab)
    {
        await LayOutAsync(window);
        var shown = new SortedSet<string>(StringComparer.Ordinal);
        Collect(window.Page, shown);
        var reported = new SortedSet<string>(lab.ReportedScreens, StringComparer.Ordinal);
        Console.WriteLine($"  screens: {(shown.Count == 0 ? "none" : string.Join(", ", shown))}");
        var neverLit = shown.Except(reported).ToList();
        var unseen = reported.Except(shown).ToList();
        if (neverLit.Count > 0) return $"the view has screens the lab never reports: {string.Join(", ", neverLit)}.";
        if (unseen.Count > 0) return $"the lab reports screens the view does not show: {string.Join(", ", unseen)}.";
        return null;

        static void Collect(DependencyObject parent, ISet<string> into)
        {
            if (parent is AIAnatomy.App.Controls.Screen { ScreenName.Length: > 0 } screen) into.Add(screen.ScreenName);
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++) Collect(System.Windows.Media.VisualTreeHelper.GetChild(parent, i), into);
        }
    }

    // The window is never shown: its content is laid out at 1600 × 1000 and the dispatcher finishes bindings, three times.
    private static async Task LayOutAsync(MainWindow window)
    {
        var content = window.Shell;
        for (int pass = 0; pass < 3; pass++)
        {
            content.Measure(new Size(1600, 1000));
            content.Arrange(new Rect(0, 0, 1600, 1000));
            content.UpdateLayout();
            await content.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>Opens the Train lab with its default settings and trains for 5 steps through its view-model.</summary>
    private static async Task<int> TrainAsync()
    {
        using var folder = new TemporaryFolder();
        var services = AppServices.Create(new InMemorySettings(new AppSettings()), folder.Path);
        var viewModel = services.NewMainViewModel();
        var lesson = services.Lessons.All.FirstOrDefault(l => l.LabId == TrainLabViewModel.Id);
        if (lesson == null) return Fail($"No lesson uses the {TrainLabViewModel.Id}.");
        await viewModel.NavigateToLesson(lesson.Id);
        if (viewModel.CurrentLab is not TrainLabViewModel lab) return Fail($"Lesson '{lesson.Id}' did not open the Train lab.");

        lab.Settings.StepsText = TrainSteps.ToString();
        if (lab.Settings.StartProblem != null) return Fail("The Train lab will not start: " + lab.Settings.StartProblem);
        await lab.StartCommand.ExecuteAsync(null).WaitAsync(Patience);
        lab.Deactivate();

        foreach (var r in lab.Reports)
            Console.WriteLine($"Step {r.Step}: training loss {NumberText.Exact(r.TrainLoss)}" +
                              (r.ValidationLoss is double v ? $", validation loss {NumberText.Exact(v)}" : ""));
        if (lab.Message != null) return Fail("Training stopped: " + lab.Message);
        if (lab.Reports.Count != TrainSteps)
            return Fail($"Expected {TrainSteps} training reports, but {lab.Reports.Count} arrived.");
        if (lab.Reports.Any(r => !double.IsFinite(r.TrainLoss) || r.ValidationLoss is double v && !double.IsFinite(v)))
            return Fail("A training report had a loss that is not a finite number.");
        Console.WriteLine($"Train self-test passed: {TrainSteps} steps with finite losses, {lab.SpeedText}.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
