using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation;

namespace AIAnatomy.App.Views;

/// <summary>"1" for the first step, "2." and so on: a Try this step's number from its zero-based position.</summary>
public sealed class NumberStepConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index ? $"{index + 1}." : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// The lesson page: one scroller holding the path, header band, Try this, lab and explanation, Check yourself, the
/// connection chips and Back / Next. It scrolls to a named place (<see cref="LessonAnchor"/>) when the view-model asks, but
/// only for a request made after it was shown: an old request is never replayed on load.
/// </summary>
public partial class LessonPageView : UserControl
{
    private MainViewModel? _main;
    private int _lastSerial;

    public LessonPageView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Attach(e.NewValue as MainViewModel);
    }

    /// <summary>The width the lab has on the page now (for the full-lab picture).</summary>
    public double LabWidth => LabContent.ActualWidth;

    /// <summary>The slot Check yourself fills (Task 10).</summary>
    public ContentControl CheckSlot => CheckYourselfSlot;

    private void Attach(MainViewModel? main)
    {
        if (_main != null) _main.PropertyChanged -= OnMainChanged;
        _main = main;
        // Whatever was asked before this page was bound is old news.
        _lastSerial = main?.AnchorRequest?.Serial ?? 0;
        if (_main != null) _main.PropertyChanged += OnMainChanged;
        ConnectionNote.Text = "";
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            PageScroller.ScrollToTop();
            ConnectionNote.Text = "";
        }
        else if (e.PropertyName == nameof(MainViewModel.AnchorRequest) && _main?.AnchorRequest is { } request && request.Serial != _lastSerial)
        {
            _lastSerial = request.Serial;
            // After the new lesson's lab and text are in the page and laid out.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => ScrollTo(request.Anchor));
        }
    }

    /// <summary>Scrolls to the named place on the page (the formula one opens the formula first). An unknown name goes to the lab.</summary>
    public void ScrollTo(string anchor)
    {
        // A lesson with no formula has nothing to open: the explanation is the nearest place.
        if (anchor == LessonAnchor.Formula && FormulaExpander.Visibility != Visibility.Visible) anchor = LessonAnchor.Explanation;
        if (anchor == LessonAnchor.Formula) FormulaExpander.IsExpanded = true;
        UpdateLayout();
        var target = LessonAnchor.Find(this, anchor) ?? LessonAnchor.Find(this, LessonAnchor.Lab);
        target?.BringIntoView();
    }

    // The sentence under the chips follows the chip with the mouse or the keyboard. Looked at a moment later, once the
    // mouse or focus has really moved (the old chip still reports its old state while it is being left).
    private void OnChipFocusChanged(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var chips = Chips().ToList();
            var shown = chips.FirstOrDefault(c => c.IsMouseOver) ?? chips.FirstOrDefault(c => c.IsKeyboardFocused);
            ConnectionNote.Text = (shown?.DataContext as ConnectionChip)?.Sentence ?? "";
        });

    private IEnumerable<Button> Chips()
    {
        var list = new List<Button>();
        Collect(this, list);
        return list;

        static void Collect(DependencyObject parent, List<Button> into)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is Button { DataContext: ConnectionChip } b) into.Add(b);
                else Collect(child, into);
            }
        }
    }
}
