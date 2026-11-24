using System.Windows;

namespace AIAnatomy.App.Views;

public partial class RulesVsLearningView : System.Windows.Controls.UserControl
{
    /// <summary>Below this width (in device-independent pixels) the two screens stack one above the other.</summary>
    public const double StackBelowWidth = 520;

    public RulesVsLearningView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width < StackBelowWidth);
    }

    private void Arrange(bool stacked)
    {
        Gap.Width = new GridLength(stacked ? 0 : 16);
        SecondColumn.Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        GapRow.Height = new GridLength(stacked ? 16 : 0);
        System.Windows.Controls.Grid.SetColumn(LearnedPanel, stacked ? 0 : 2);
        System.Windows.Controls.Grid.SetRow(LearnedPanel, stacked ? 2 : 0);
    }
}
