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
        var map = Show(window, viewModel, services, view);

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
        if (options.FullLab)
        {
            // The lab pane's width is read from the window as laid out above (stacked or side by side), then the lab is
            // shown by itself and drawn at its full height like the gallery (final review I8).
            window.ShowFullLabPage();
            await SettleAsync(content, options.Width, height);
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
            case DiagnosticOptions.Gallery: window.ShowDiagnosticPage(new ControlGallery()); break;
            case DiagnosticOptions.SourcesView: viewModel.ShowSourcesCommand.Execute(null); break;
            case DiagnosticOptions.SettingsView: viewModel.ShowSettingsCommand.Execute(null); break;
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
