using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

public partial class ExpertSystemView : UserControl
{
    /// <summary>Below this width (in device-independent pixels) the two screens stack one above the other.</summary>
    public const double StackBelowWidth = 520;

    public ExpertSystemView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width < StackBelowWidth);
    }

    private ExpertSystemLabViewModel? Lab => DataContext as ExpertSystemLabViewModel;

    private void Arrange(bool stacked)
    {
        Gap.Width = new GridLength(stacked ? 0 : 16);
        FirstColumn.Width = new GridLength(stacked ? 1 : 3, GridUnitType.Star);
        SecondColumn.Width = stacked ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
        GapRow.Height = new GridLength(stacked ? 16 : 0);
        Grid.SetColumn(AnswerPanel, stacked ? 0 : 2);
        Grid.SetRow(AnswerPanel, stacked ? 2 : 0);
    }

    // A rule edit is applied on Enter or when the box loses focus; Escape puts back the rule as it was.
    private void OnRuleKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not RuleLine line) return;
        if (e.Key == Key.Enter)
        {
            Commit(box, line);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            box.Text = line.Text;
            box.CaretIndex = box.Text.Length;
            e.Handled = true;
        }
    }

    private void OnRuleLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box && box.DataContext is RuleLine line) Commit(box, line);
    }

    private void Commit(TextBox box, RuleLine line)
    {
        // A box whose line has gone (deleted, or replaced by Reset) has nothing to apply.
        if (Lab is not { } lab || !lab.Rules.Contains(line) || box.Text == line.Text) return;
        lab.CommitRule(line, box.Text);
    }

    // The new empty rule gets the keyboard focus, ready to type into (the command has run by the time this is called).
    private void OnAddRuleClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FocusRule(RuleList.Items.Count - 1));

    // Click is raised before the button's command runs, so the deleted line's place is read here; once the list has been
    // laid out again the focus goes to the line now in that place, the one before it, or Add rule when none are left.
    private void OnDeleteRuleClick(object sender, RoutedEventArgs e)
    {
        if (Lab is not { } lab || sender is not FrameworkElement { DataContext: RuleLine line }) return;
        int index = lab.Rules.IndexOf(line);
        if (index < 0) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (ExpertSystemLabViewModel.FocusIndexAfterDelete(index, lab.Rules.Count) is { } next) FocusRule(next);
            else AddRuleButton.Focus();
        });
    }

    private void FocusRule(int index)
    {
        if (index < 0 || index >= RuleList.Items.Count) return;
        if (RuleList.ItemContainerGenerator.ContainerFromIndex(index) is DependencyObject item && Find<TextBox>(item) is { } box)
            box.Focus();
    }

    private static T? Find<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (Find<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
}
