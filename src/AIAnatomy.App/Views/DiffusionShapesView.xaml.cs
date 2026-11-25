using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

/// <summary>
/// Lesson 4.9's lab view. A finished stroke (or the pad being cleared) hands the pad's lines to the lab, which uses only the
/// last one; when the lab goes back to a preset shape (a preset chosen, or Reset), the pad is emptied so it never shows a
/// drawing the lab is not using. Play is driven by the lab's own clock; nothing here moves by itself.
/// </summary>
public partial class DiffusionShapesView : UserControl
{
    private DiffusionShapesLabViewModel? _lab;
    private bool _clearing;

    public DiffusionShapesView()
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
    /// wide, and otherwise below it at the full width (as in lesson 4.7's view). The width is set explicitly because a
    /// WrapPanel gives its children unlimited width, so text would never wrap.
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
        _lab = e.NewValue as DiffusionShapesLabViewModel;
        if (_lab != null) _lab.PropertyChanged += OnLabChanged;
    }

    private void OnLabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DiffusionShapesLabViewModel.IsDrawnShape) || _lab is not { IsDrawnShape: false } || Pad.IsEmpty) return;
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
