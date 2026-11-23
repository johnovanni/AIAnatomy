using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIAnatomy.App.Controls;
using AIAnatomy.Lessons;
using AIAnatomy.Presentation;

namespace AIAnatomy.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
        // Lesson links in the explanation and linked boxes in diagrams all arrive here.
        AddHandler(LessonNavigation.NavigateRequestedEvent, new NavigateRequestedEventHandler((_, e) =>
        {
            ViewModel.NavigateToLessonCommand.Execute(e.LessonId);
            e.Handled = true;
        }));
    }

    public MainViewModel ViewModel { get; }

    /// <summary>Shows a page in place of the lesson (used by --render-png gallery).</summary>
    public void ShowDiagnosticPage(UIElement page)
    {
        DiagnosticPage.Content = page;
        DiagnosticPage.Visibility = Visibility.Visible;
        LessonPage.Visibility = Visibility.Collapsed;
    }

    // A click on a lesson, even the one already selected, opens it (and leaves Sources or Settings).
    private void OnLessonClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: Lesson lesson }) ViewModel.OpenLessonCommand.Execute(lesson);
    }

    private void OnLessonKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && sender is ListBoxItem { DataContext: Lesson lesson })
        {
            ViewModel.OpenLessonCommand.Execute(lesson);
            e.Handled = true;
        }
    }

    private void OnSectionClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: Section section }) ViewModel.ShowSectionCommand.Execute(section);
    }

    private void OnSectionKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && sender is ListBoxItem { DataContext: Section section })
        {
            ViewModel.ShowSectionCommand.Execute(section);
            e.Handled = true;
        }
    }
}
