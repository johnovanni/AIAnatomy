using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AIAnatomy.Lessons;
using AIAnatomy.Presentation;
using AIAnatomy.Presentation.Contents;

namespace AIAnatomy.App.Views;

/// <summary>
/// The numbered contents tree. The arrow keys move through it (and open or close groups); Enter or Space, or a click, opens
/// the lesson or branch (a section heading opens or closes instead). Moving with the arrows only moves the outline, so
/// looking down the list does not open every lab on the way. Tab leaves the tree for the lesson page.
/// </summary>
public partial class ContentsTreeView : UserControl
{
    public ContentsTreeView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old) old.PropertyChanged -= OnMainChanged;
            if (e.NewValue is MainViewModel now) now.PropertyChanged += OnMainChanged;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, RevealSelected);
        };
        Tree.PreviewGotKeyboardFocus += OnFocusArriving;
    }

    /// <summary>
    /// Tab (or Shift+Tab) into the tree lands on the open lesson's row. WPF would otherwise return to the row that last had
    /// the keyboard, which may be a lesson clicked several lessons ago (final review I5). A click still goes where it is aimed,
    /// and moving inside the tree is not touched.
    /// </summary>
    private void OnFocusArriving(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OldFocus is Visual old && Tree.IsAncestorOf(old)) return;
        if (InputManager.Current.MostRecentInputDevice is not KeyboardDevice) return;
        if (_openItem is not { IsVisible: true } item || ReferenceEquals(e.NewFocus, item) || !Tree.IsAncestorOf(item)) return;
        e.Handled = true;   // cancels this move; the open lesson's row takes the focus instead
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => item.Focus());
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedLesson)) Dispatcher.BeginInvoke(DispatcherPriority.Background, RevealSelected);
    }

    // The open lesson's row, once RevealSelected has found it.
    private TreeViewItem? _openItem;

    /// <summary>
    /// Scrolls the open lesson's row into view, opening its section first if it is closed (a "coming later" one starts
    /// closed), and makes it the tree's own selected row, so the keyboard starts at the lesson being read rather than at
    /// the last row clicked (final review I5). Selecting only moves where the keyboard starts: a lesson still opens only
    /// on a click or on Enter or Space.
    /// </summary>
    private void RevealSelected()
    {
        if (DataContext is not MainViewModel { SelectedLesson: { } open }) return;
        Tree.UpdateLayout();
        foreach (var section in Tree.Items.OfType<ContentsSection>())
        {
            var row = section.Lessons.FirstOrDefault(l => l.Lesson.Id == open.Id);
            if (row == null || Tree.ItemContainerGenerator.ContainerFromItem(section) is not TreeViewItem sectionItem) continue;
            if (!sectionItem.IsExpanded) sectionItem.IsExpanded = true;
            sectionItem.UpdateLayout();
            if (sectionItem.ItemContainerGenerator.ContainerFromItem(row) is not TreeViewItem lessonItem) return;
            lessonItem.IsSelected = true;
            _openItem = lessonItem;
            lessonItem.BringIntoView();
            return;
        }
    }

    /// <summary>Opens every group, branches included (for the <c>contents</c> picture).</summary>
    public void ExpandAll() => ExpandAll(Tree);

    private static void ExpandAll(ItemsControl parent)
    {
        parent.UpdateLayout();
        foreach (object item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem container) continue;
            container.IsExpanded = true;
            ExpandAll(container);
        }
    }

    private void OnTreeMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source) return;
        if (Ancestor<ToggleButton>(source) != null) return;   // the arrow does its own opening and closing
        if (Ancestor<TreeViewItem>(source) is { } item) Activate(item);
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        if (e.OriginalSource is TreeViewItem item)
        {
            Activate(item);
            e.Handled = true;
        }
    }

    private void Activate(TreeViewItem item)
    {
        if (DataContext is not MainViewModel main) return;
        switch (item.DataContext)
        {
            case ContentsLesson lesson:
                main.NavigateToLessonCommand.Execute(lesson.Lesson.Id);
                if (lesson.Branches.Count > 0) item.IsExpanded = true;
                break;
            case LessonBranch branch:
                main.OpenBranchCommand.Execute(branch);
                break;
            case ContentsSection:
                item.IsExpanded = !item.IsExpanded;
                break;
        }
    }

    private static T? Ancestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (var d = start; d != null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T found) return found;
        return null;
    }
}
