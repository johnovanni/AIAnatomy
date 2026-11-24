using System.Windows;
using System.Windows.Threading;

namespace AIAnatomy.App.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView() => InitializeComponent();

    // When the Reset progress question appears, the keyboard goes to "Keep it", the safe answer (re-review N2): the
    // Reset button that had it is disabled now.
    private void OnResetQuestionShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) Dispatcher.BeginInvoke(DispatcherPriority.Input, () => KeepProgressButton.Focus());
    }
}
