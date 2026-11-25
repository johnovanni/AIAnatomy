using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

/// <summary>
/// Lesson 4.6's lab view. A finished stroke (or the pad being cleared) hands the drawing to the lab; when the lab goes back to
/// a test digit (Previous, Next, a digit, Reset), the pad is emptied so it never shows a drawing the lab is not using.
/// </summary>
public partial class FiltersView : UserControl
{
    private FiltersLabViewModel? _lab;
    private bool _clearing;

    public FiltersView()
    {
        InitializeComponent();
        SizeBoxes();
        Pad.StrokesChanged += OnStrokesChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_lab != null) _lab.PropertyChanged -= OnLabChanged;
        _lab = e.NewValue as FiltersLabViewModel;
        if (_lab != null) _lab.PropertyChanged += OnLabChanged;
    }

    private void OnLabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FiltersLabViewModel.UsingDrawing) || _lab is not { UsingDrawing: false } || Pad.IsEmpty) return;
        _clearing = true;
        try { Pad.Clear(); }
        finally { _clearing = false; }
    }

    private void OnStrokesChanged(object? sender, EventArgs e)
    {
        if (_clearing || _lab == null) return;
        _lab.UseDrawing(DrawingPad.ToDigitStrokes(Pad.Strokes));
    }

    private void OnClearPad(object sender, RoutedEventArgs e) => Pad.Clear();

    /// <summary>Half-typed text ("−") does not stay in a box the keyboard has left.</summary>
    private void OnFilterBoxLeft(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FilterCell cell }) cell.ShowValue();
    }

    // The number boxes grow with the text, so "−0.766" fits at 24 pt.
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) SizeBoxes();
    }

    private void SizeBoxes() => Resources["FilterBoxWidth"] = Math.Max(80, FontSize * 5);
}
