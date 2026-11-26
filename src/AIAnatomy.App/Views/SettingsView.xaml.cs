using System.Windows;
using System.Windows.Threading;

namespace AIAnatomy.App.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView() => InitializeComponent();

    // When the Reset progress question appears, the keyboard goes to "Keep it", the safe answer (re-review N2): the
    // Reset button that had it is disabled now. Then the question is said (Stage 2B ruling X): its text is fixed, so the
    // live-text rule (a changed text) never would; it comes after the focus move, so the button's name does not cut it off.
    private void OnResetQuestionShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => KeepProgressButton.Focus());
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => AIAnatomy.App.Controls.LiveText.Announce(ResetQuestionText));
    }
}
