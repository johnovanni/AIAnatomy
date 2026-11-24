using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace AIAnatomy.App.Views;

public partial class CheckYourselfView : UserControl
{
    public CheckYourselfView() => InitializeComponent();

    // The feedback text changed: tell screen readers (the live region's text is not announced by a changed Text alone).
    private void OnFeedbackUpdated(object? sender, DataTransferEventArgs e)
    {
        // The peer may not exist yet the first time (its panel has only just become visible), so create it.
        if (sender is TextBlock { Text.Length: > 0 } text && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            (UIElementAutomationPeer.FromElement(text) ?? UIElementAutomationPeer.CreatePeerForElement(text))
                ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    // Skip and Show again hide the button that was pressed, so keyboard focus moves to the control that takes its place.
    private void OnSkipClicked(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { UpdateLayout(); ShowAgainButton.Focus(); });

    private void OnShowAgainClicked(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            UpdateLayout();
            FirstButtonIn(QuestionList)?.Focus();
        });

    private static Button? FirstButtonIn(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button b) return b;
            if (FirstButtonIn(child) is { } found) return found;
        }
        return null;
    }
}
