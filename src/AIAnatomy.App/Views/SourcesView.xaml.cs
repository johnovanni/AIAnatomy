using System.Diagnostics;
using System.Windows.Navigation;

namespace AIAnatomy.App.Views;

public partial class SourcesView : System.Windows.Controls.UserControl
{
    public SourcesView() => InitializeComponent();

    // Opens a source's web page in the user's browser. Only http(s) links are opened.
    private void OnOpenLink(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri.Scheme is "https" or "http")
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
