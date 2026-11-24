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
        Show(window, viewModel, services, view);

        if (viewModel.CurrentLab != null) await viewModel.CurrentLab.WorkCompleted;   // e.g. the Tokens lab learning its BPE

        // The window is never shown: its content is laid out and drawn on its own, at exactly the size asked for.
        var content = window.Shell;
        double height = options.Height;
        await SettleAsync(content, options.Width, height);
        if (options.FullLab)
        {
            // The lab pane's width is read from the window as laid out above (stacked or side by side), then the lab is
            // shown by itself and drawn at its full height like the gallery (final review I8).
            window.ShowFullLabPage();
            await SettleAsync(content, options.Width, height);
        }
        if (view == DiagnosticOptions.Gallery || options.FullLab)
        {
            // The gallery (or a full lab) is drawn at its full height so everything is in the picture.
            content.Measure(new Size(options.Width, double.PositiveInfinity));
            height = Math.Min(MaxGalleryHeight, Math.Max(height, Math.Ceiling(content.DesiredSize.Height)));
            await SettleAsync(content, options.Width, height);
        }

        var bitmap = new RenderTargetBitmap(options.Width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        await using var stream = File.Create(file);
        encoder.Save(stream);
        Console.WriteLine($"Saved {file} ({options.Width} x {(int)height}, {ThemeManager.Theme}, {options.TextSize} pt).");
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
