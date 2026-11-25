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
        PadRow.SizeChanged += (_, _) => PlacePadText();
        Pad.SizeChanged += (_, _) => PlacePadText();
    }

    /// <summary>The narrowest the text beside the pad may be, in font sizes (about 30 characters a line).</summary>
    internal const double MinTextWidthInFonts = 16;

    /// <summary>
    /// Puts the pad's text beside the pad when the column left over is at least <see cref="MinTextWidthInFonts"/> font sizes
    /// wide, and otherwise below it at the full width, so a large text size in a narrow pane never leaves a sliver of text.
    /// The width is set explicitly because a WrapPanel gives its children unlimited width, so text would never wrap.
    /// </summary>
    private void PlacePadText()
    {
        double room = PadRow.ActualWidth;
        if (!(room > 0)) return;
        const double gap = 16;
        double beside = Math.Floor(room - Pad.ActualWidth - gap - 1);
        bool fits = beside >= MinTextWidthInFonts * FontSize;
        double width = fits ? beside : Math.Floor(room - 1);
        var margin = fits ? new Thickness(gap, 0, 0, 0) : new Thickness(0, 8, 0, 0);
        if (!PadText.Width.Equals(width)) PadText.Width = width;
        if (PadText.Margin != margin) PadText.Margin = margin;
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
