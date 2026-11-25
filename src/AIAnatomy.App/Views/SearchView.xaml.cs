using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

public partial class SearchView : UserControl
{
    /// <summary>Below this width (in device-independent pixels) the look-ahead goes under the board.</summary>
    public const double StackBelowWidth = 760;

    private SearchLabViewModel? _lab;

    public SearchView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width < StackBelowWidth);
        DataContextChanged += (_, _) => Watch(DataContext as SearchLabViewModel);
        Unloaded += (_, _) => Watch(null);
        Loaded += (_, _) => Watch(DataContext as SearchLabViewModel);
    }

    private void Watch(SearchLabViewModel? lab)
    {
        if (_lab != null) _lab.PropertyChanged -= OnLabChanged;
        _lab = lab;
        if (_lab != null) _lab.PropertyChanged += OnLabChanged;
        ShowBoardExtras();
    }

    private void OnLabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SearchLabViewModel.Board) or nameof(SearchLabViewModel.CountText)) ShowBoardExtras();
    }

    // The line through a winning row, column or diagonal, from the first square's centre to the last's; and the
    // "nothing yet" line while the computer has not moved.
    private void ShowBoardExtras()
    {
        bool counted = !string.IsNullOrEmpty(_lab?.CountText);
        CountLine.Visibility = counted ? Visibility.Visible : Visibility.Collapsed;
        Waiting.Visibility = counted ? Visibility.Collapsed : Visibility.Visible;
        if (_lab?.Board.WinningLine is not { } line)
        {
            WinLine.Visibility = Visibility.Collapsed;
            return;
        }
        static double Centre(int index) => index * 100 + 50;
        WinLine.X1 = Centre(line[0] % 3);
        WinLine.Y1 = Centre(line[0] / 3);
        WinLine.X2 = Centre(line[2] % 3);
        WinLine.Y2 = Centre(line[2] / 3);
        WinLine.Visibility = Visibility.Visible;
    }

    private void Arrange(bool stacked)
    {
        Gap.Width = new GridLength(stacked ? 0 : 16);
        FirstColumn.Width = stacked ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        SecondColumn.Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        GapRow.Height = new GridLength(stacked ? 16 : 0);
        Grid.SetColumn(LookAheadPanel, stacked ? 0 : 2);
        Grid.SetRow(LookAheadPanel, stacked ? 2 : 0);
    }

    // A move's replies are searched when it first opens, so until then the item has no children and the TreeView's own
    // Right arrow (and +) would not open it. These keys open such a move here; once it has children, WPF's keys work.
    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Right or Key.Add)) return;
        if (Keyboard.FocusedElement is TreeViewItem { DataContext: LookAheadNode { CanExpand: true, IsExpanded: false } node })
        {
            node.IsExpanded = true;
            e.Handled = true;
        }
    }
}
