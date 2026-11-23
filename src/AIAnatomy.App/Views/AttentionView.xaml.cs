using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace AIAnatomy.App.Views;

public partial class AttentionView : UserControl
{
    public AttentionView()
    {
        InitializeComponent();
        // The number boxes of the toy commit when you leave them; Enter commits too.
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key == Key.Enter && e.OriginalSource is TextBox { AcceptsReturn: false } box)
                BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateSource();
        }));
    }
}
