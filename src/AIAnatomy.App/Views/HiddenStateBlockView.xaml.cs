using System.Windows;
using System.Windows.Controls;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

/// <summary>
/// One block of the memory lab's hidden-state map (rows are characters, columns units). The map is given a width at which
/// each square is at least as tall as a row label, so every character keeps its label; the lab scrolls it sideways when
/// the pane is narrower than that.
/// </summary>
public partial class HiddenStateBlockView : UserControl
{
    public HiddenStateBlockView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Resize();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) Resize();
    }

    private void Resize()
    {
        if (Map == null) return;
        int columns = (DataContext as HiddenStateBlock)?.Values.GetLength(1) ?? MemoryNetworkLabViewModel.UnitsPerBlock;
        double label = Math.Max(12, FontSize * 0.8);   // the charts' label size (ChartBase.LabelSize)
        double cell = Math.Ceiling(label * 1.45);       // a label's line height, with a little room
        Map.Width = Math.Ceiling(label * 3 + 12) + columns * cell + 2;
    }
}
