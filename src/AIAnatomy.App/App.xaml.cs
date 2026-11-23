using System.IO;
using System.Windows;
using System.Windows.Controls;
using AIAnatomy.App.Diagnostics;
using AIAnatomy.App.Themes;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var options = DiagnosticOptions.Parse(e.Args);
        if (options.Error != null)
        {
            Console.Error.WriteLine(options.Error);
            Shutdown(1);
            return;
        }
        if (options.Mode != DiagnosticMode.Normal)
        {
            Shutdown(await CommandLine.RunAsync(options));
            return;
        }
        RunNormally();
    }

    private void RunNormally()
    {
        var settings = new SettingsService(AppPaths.SettingsFile);
        ThemeManager.Apply(settings.Current.Theme, settings.Current.BodyFontSize, settings.Current.HeadingFontSize);

        AppServices services;
        try
        {
            services = AppServices.Create(settings);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            ShowStartupError(ex.Message);
            Shutdown(1);
            return;
        }

        var viewModel = services.NewMainViewModel();
        viewModel.SettingsPage.Applied += (_, _) =>
            ThemeManager.Apply(settings.Current.Theme, settings.Current.BodyFontSize, settings.Current.HeadingFontSize);
        var window = new MainWindow(viewModel) { WindowState = WindowState.Maximized };
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    /// <summary>A plain window (in the theme, at the user's text size) saying why the app cannot start.</summary>
    private static void ShowStartupError(string message)
    {
        var close = new Button { Content = "Close", IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var heading = new TextBlock { Text = "AIAnatomy could not start" };
        heading.SetResourceReference(FrameworkElement.StyleProperty, "HeadingText");
        var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(heading);
        panel.Children.Add(body);
        panel.Children.Add(close);
        var window = new Window
        {
            Title = "AIAnatomy",
            Content = panel,
            Width = 760,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
        };
        window.SetResourceReference(Control.BackgroundProperty, "WindowBrush");
        window.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        window.SetResourceReference(Control.FontSizeProperty, "BodyFontSize");
        close.Click += (_, _) => window.Close();
        window.ShowDialog();
    }
}
