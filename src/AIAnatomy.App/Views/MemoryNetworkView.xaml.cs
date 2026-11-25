using System.Windows;
using System.Windows.Controls;

namespace AIAnatomy.App.Views;

public partial class MemoryNetworkView : UserControl
{
    public MemoryNetworkView() => InitializeComponent();

    // The charts grow with the text, so at 24 pt their axis labels still leave room for the lines.
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != FontSizeProperty || MemoryChart == null) return;
        MemoryChart.Height = Math.Max(280, FontSize * 13);
        TrainingChart.Height = Math.Max(220, FontSize * 11);
    }
}
