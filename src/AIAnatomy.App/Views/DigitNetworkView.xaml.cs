using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

/// <summary>
/// Lesson 4.7's lab view. A finished stroke (or the pad being cleared) hands the drawing to the lab; when the lab goes back to
/// a test digit (Previous, Next, a digit, Reset), the pad is emptied so it never shows a drawing the lab is not using.
/// </summary>
public partial class DigitNetworkView : UserControl
{
    private DigitNetworkLabViewModel? _lab;
    private bool _clearing;

    public DigitNetworkView()
    {
        InitializeComponent();
        Pad.StrokesChanged += OnStrokesChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_lab != null) _lab.PropertyChanged -= OnLabChanged;
        _lab = e.NewValue as DigitNetworkLabViewModel;
        if (_lab != null) _lab.PropertyChanged += OnLabChanged;
    }

    private void OnLabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DigitNetworkLabViewModel.UsingDrawing) || _lab is not { UsingDrawing: false } || Pad.IsEmpty) return;
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
}
