using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Navigation;
using AIAnatomy.Presentation;

namespace AIAnatomy.App.Views;

public partial class SourcesView : System.Windows.Controls.UserControl
{
    public SourcesView() => InitializeComponent();

    // Opens a source's web page in the user's browser. Only http(s) links are opened. If no browser is set up, or
    // opening is blocked, a plain status line gives the address instead of the error window (final review M10).
    private void OnOpenLink(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri.Scheme is not ("https" or "http")) return;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            if (DataContext is MainViewModel viewModel) viewModel.ShowLinkNotOpened(e.Uri.AbsoluteUri);
        }
    }
}
