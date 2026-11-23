using System.Windows;
using AIAnatomy.App.Themes;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App.Diagnostics;

/// <summary><c>--selftest</c>: scripted checks that run without showing a window. Each returns the exit code.</summary>
public static class SelfTests
{
    public static int Run(DiagnosticOptions options) => options.SelfTest switch
    {
        DiagnosticOptions.AllLabsTest => AllLabs(),
        DiagnosticOptions.TrainTest => Fail("The train self-test needs the Train lab, which is not built yet."),
        _ => Fail($"Unknown self-test '{options.SelfTest}'."),
    };

    /// <summary>Opens every lesson, creates its lab, checks a view exists for it, and calls Reset.</summary>
    private static int AllLabs()
    {
        ThemeManager.Apply("Light", 20, 28);
        var services = AppServices.Create(new InMemorySettings(new AppSettings()));
        var viewModel = services.NewMainViewModel();
        var window = new MainWindow(viewModel);
        foreach (var lesson in services.Lessons.All)
        {
            viewModel.NavigateToLesson(lesson.Id);
            var lab = viewModel.CurrentLab ?? throw new InvalidOperationException($"Lesson '{lesson.Id}' has no lab.");
            if (window.TryFindResource(new DataTemplateKey(lab.GetType())) == null)
                return Fail($"Lesson '{lesson.Id}': there is no view for {lab.GetType().Name}.");
            lab.Reset();
            Console.WriteLine($"{lesson.Id}: {lab.LabId} created and reset.");
        }
        Console.WriteLine($"All {services.Lessons.All.Count} labs created and reset.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
