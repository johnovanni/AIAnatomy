using System.Windows;
using AIAnatomy.App.Themes;
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
    /// Opens every lesson, creates its lab, checks a view exists for it, calls Reset (waiting for any work that starts) and
    /// Deactivate. Fails if any of that throws, or if a lesson names a lab that is not registered.
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
