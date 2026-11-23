using System.Windows;
using System.Windows.Controls;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

public partial class TrainView : UserControl
{
    public TrainView()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(OwnTextBox, OnPasting);
    }

    /// <summary>
    /// A paste that would make the text longer than the box allows is refused as a whole (the box would otherwise cut it
    /// short without a word), and the lab says why.
    /// </summary>
    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (DataContext is not TrainLabViewModel lab || e.DataObject.GetData(DataFormats.UnicodeText) is not string pasted) return;
        long length = (long)OwnTextBox.Text.Length - OwnTextBox.SelectionLength + pasted.Length;
        if (length <= OwnTextBox.MaxLength) return;
        e.CancelCommand();
        lab.Settings.RefusePaste((int)Math.Min(length, int.MaxValue));
    }
}
