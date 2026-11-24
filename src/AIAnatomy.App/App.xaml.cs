using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AIAnatomy.App.Diagnostics;
using AIAnatomy.App.Themes;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.App;

public partial class App : Application
{
    private bool _diagnosticRun;
    private bool _showingError;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
        // Background work that nobody awaits: a failure is written to the error log (final review M14), never shown as a crash.
        ErrorLog.BackgroundFailed = error => ErrorLog.Append(AppPaths.ErrorLogFile, error, DateTimeOffset.Now);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLog.ReportBackground(args.Exception);
            args.SetObserved();
        };
        var options = DiagnosticOptions.Parse(e.Args);
        _diagnosticRun = options.Mode != DiagnosticMode.Normal;
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
        ThemeManager.Apply(settings.Current.Theme, settings.Current.BodyFontSize);

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
            ThemeManager.Apply(settings.Current.Theme, settings.Current.BodyFontSize);
        var window = new MainWindow(viewModel) { WindowState = WindowState.Maximized };
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    /// <summary>
    /// Last line of defence: a surprise error is written to the error log and shown in a plain window, and the app
    /// keeps running. A diagnostic run writes it to stderr and exits 1 instead.
    /// </summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        bool logged = ErrorLog.Append(AppPaths.ErrorLogFile, e.Exception, DateTimeOffset.Now);
        if (_diagnosticRun)
        {
            Console.Error.WriteLine(e.Exception.Message);
            Shutdown(1);
            return;
        }
        if (_showingError) return;   // an error while showing the error window: logged, not shown again
        _showingError = true;
        try
        {
            string details = logged ? $"The details were written to {AppPaths.ErrorLogFile}." : "The details could not be written to the error log.";
            ShowMessageWindow("Something went wrong", ErrorLog.UserMessage(e.Exception) + "\n\n" + details, "OK");
        }
        finally { _showingError = false; }
    }

    /// <summary>A plain window (in the theme, at the user's text size) saying why the app cannot start.</summary>
    private static void ShowStartupError(string message) => ShowMessageWindow("AIAnatomy could not start", message, "Close");

    private static void ShowMessageWindow(string title, string message, string buttonText)
    {
        var close = new Button { Content = buttonText, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var heading = new TextBlock { Text = title };
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
        if (Current.MainWindow is { IsVisible: true } owner) window.Owner = owner;
        window.ShowDialog();
    }
}
