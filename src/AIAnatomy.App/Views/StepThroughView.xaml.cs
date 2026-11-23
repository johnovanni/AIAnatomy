using System.Windows.Input;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

public partial class StepThroughView : System.Windows.Controls.UserControl
{
    public StepThroughView() => InitializeComponent();

    /// <summary>Left or Page Up goes to the previous step, Right or Page Down to the next, while the steps have focus.</summary>
    private void OnPipelineKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not StepThroughLabViewModel lab) return;
        var command = e.Key switch
        {
            Key.Right or Key.PageDown => lab.NextCommand,
            Key.Left or Key.PageUp => lab.PreviousCommand,
            _ => null,
        };
        if (command == null) return;
        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }
}
