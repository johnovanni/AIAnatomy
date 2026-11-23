using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AIAnatomy.App.Themes;
using AIAnatomy.App.Views;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App.Diagnostics;

/// <summary>
/// <c>--render-png</c>: draws the window's content into a PNG without showing it on screen, so screens can be checked
/// without a screen capture. The theme and text size apply to this render only; nothing is saved.
/// </summary>
public static class PngRenderer
{
    private const double MaxGalleryHeight = 12000;

    public static async Task RenderAsync(DiagnosticOptions options)
    {
        double heading = Math.Round(options.TextSize * 1.4, MidpointRounding.AwayFromZero);
        ThemeManager.Apply(options.Theme, options.TextSize, heading);
        var services = AppServices.Create(new InMemorySettings(new AppSettings(options.TextSize, heading, options.Theme)));
        var viewModel = services.NewMainViewModel();
        var window = new MainWindow(viewModel);
        Show(window, viewModel, services, options.View!);

        // The window is never shown: its content is laid out and drawn on its own, at exactly the size asked for.
        var content = window.Shell;
        double height = options.Height;
        await SettleAsync(content, options.Width, height);
        if (options.View == DiagnosticOptions.Gallery)
        {
            // The gallery is drawn at its full height so every control is in the picture.
            content.Measure(new Size(options.Width, double.PositiveInfinity));
            height = Math.Min(MaxGalleryHeight, Math.Max(height, Math.Ceiling(content.DesiredSize.Height)));
            await SettleAsync(content, options.Width, height);
        }

        var bitmap = new RenderTargetBitmap(options.Width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputFile!))!);
        await using var stream = File.Create(options.OutputFile!);
        encoder.Save(stream);
        Console.WriteLine($"Saved {options.OutputFile} ({options.Width} x {(int)height}, {ThemeManager.Theme}, {options.TextSize} pt).");
    }

    private static void Show(MainWindow window, MainViewModel viewModel, AppServices services, string view)
    {
        switch (view)
        {
            case DiagnosticOptions.Gallery: window.ShowDiagnosticPage(new ControlGallery()); break;
            case DiagnosticOptions.SourcesView: viewModel.ShowSourcesCommand.Execute(null); break;
            case DiagnosticOptions.SettingsView: viewModel.ShowSettingsCommand.Execute(null); break;
            default:
                if (services.Lessons.All.All(l => l.Id != view))
                    throw new ArgumentException($"There is no lesson or view called '{view}'. Use a lesson id, gallery, sources or settings.");
                viewModel.NavigateToLesson(view);
                break;
        }
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
