using System.Windows;
using System.Windows.Controls;
using AIAnatomy.App.Controls;
using AIAnatomy.App.Views;
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

    /// <summary>Background work stops when the window closes, including a Train lab run left going in the background (R35).</summary>
    protected override void OnClosed(EventArgs e)
    {
        ViewModel.Shutdown();
        base.OnClosed(e);
    }

    /// <summary>
    /// For <c>--render-png --full-lab</c>: the current lab by itself (with its title, in a pane), at the width the lab has in
    /// the window now, so its text wraps as in the app; the caller then draws it at its full height (final review I8).
    /// </summary>
    public void ShowFullLabPage()
    {
        double width = Math.Max(200, LessonPage.LabWidth);
        var title = new TextBlock { Text = ViewModel.SelectedLesson?.Title ?? "" };
        title.SetResourceReference(StyleProperty, "HeadingText");
        var lab = new ContentControl { Content = ViewModel.CurrentLab, Width = width, HorizontalAlignment = HorizontalAlignment.Left, Focusable = false };
        var panel = new StackPanel();
        panel.Children.Add(title);
        panel.Children.Add(lab);
        var pane = new Border { Child = panel, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        pane.SetResourceReference(StyleProperty, "Pane");
        ShowDiagnosticPage(pane);
    }

    /// <summary>Shows a page in place of the lesson (used by --render-png gallery and --full-lab).</summary>
    public void ShowDiagnosticPage(UIElement page)
    {
        DiagnosticPage.Content = page;
        DiagnosticPage.Visibility = Visibility.Visible;
        LessonPage.Visibility = Visibility.Collapsed;
        ContentsPane.Visibility = Visibility.Collapsed;
    }

    /// <summary>The lesson page (for <c>--render-png</c> and the self-test's anchor check).</summary>
    public LessonPageView Page => LessonPage;

    /// <summary>The idea map page, which the window shows when the view-model says so (for <c>--render-png</c>).</summary>
    public IdeaMapView IdeaMap => IdeaMapPage;

    /// <summary>Opens every group of the contents tree, branches included (for the <c>contents</c> picture).</summary>
    public void ExpandContents() => ContentsTree.ExpandAll();
}
