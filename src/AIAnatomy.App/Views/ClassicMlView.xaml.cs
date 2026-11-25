using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

public partial class ClassicMlView : UserControl
{
    /// <summary>Below this width (in device-independent pixels), or below <see cref="StackBelowEms"/> times the text size if
    /// that is wider, the questions go under the plot, so neither the plot nor the indented questions get squeezed.</summary>
    public const double StackBelowWidth = 900, StackBelowEms = 50;

    public ClassicMlView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Rearrange();
        Plot.AddRequested += OnAddRequested;
        Plot.RemoveRequested += OnRemoveRequested;
    }

    private ClassicMlLabViewModel? Lab => DataContext as ClassicMlLabViewModel;

    // A click or Enter adds a point of the chosen class there; a right-click or Delete removes the nearest. What happened is
    // said under the plot (and read out), and a refusal at 200 points shows above it.
    private void OnAddRequested(object? sender, PlotPlaceEventArgs e)
    {
        Lab?.AddPoint(e.X, e.Y);
        Announce();
    }

    private void OnRemoveRequested(object? sender, PlotPlaceEventArgs e)
    {
        Lab?.RemoveNearest(e.X, e.Y);
        Announce();
    }

    // The action line is said every time, even when its text is the same as before (two points removed in a row).
    private void Announce()
    {
        if (Lab is { ActionText.Length: > 0 } && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            (UIElementAutomationPeer.FromElement(ActionLine) ?? UIElementAutomationPeer.CreatePeerForElement(ActionLine))
                ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) Rearrange();
    }

    private void Rearrange()
    {
        if (ActualWidth > 0) Arrange(ActualWidth < Math.Max(StackBelowWidth, FontSize * StackBelowEms));
    }

    private void Arrange(bool stacked)
    {
        Gap.Width = new GridLength(stacked ? 0 : 16);
        FirstColumn.Width = new GridLength(stacked ? 1 : 3, GridUnitType.Star);
        SecondColumn.Width = stacked ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
        GapRow.Height = new GridLength(stacked ? 16 : 0);
        Grid.SetColumn(QuestionsPanel, stacked ? 0 : 2);
        Grid.SetRow(QuestionsPanel, stacked ? 2 : 0);
    }
}
