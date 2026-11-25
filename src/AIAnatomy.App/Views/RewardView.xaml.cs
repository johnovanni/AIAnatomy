using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

public partial class RewardView : UserControl
{
    /// <summary>Below this width (in device-independent pixels), or below <see cref="StackBelowEms"/> times the text size if
    /// that is wider, the episodes screen goes under the maze, so neither is squeezed.</summary>
    public const double StackBelowWidth = 900, StackBelowEms = 50;

    public RewardView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Rearrange();
        Maze.ApplyRequested += OnApplyRequested;
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is RewardLabViewModel old) old.PropertyChanged -= OnLabChanged;
            if (e.NewValue is RewardLabViewModel now) now.PropertyChanged += OnLabChanged;
        };
    }

    // The run's last update comes just before it ends, so the episodes line is read out when the run has ended.
    private void OnLabChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RewardLabViewModel.IsRunning) && Lab is { IsRunning: false }) Announce(EpisodesLine);
    }

    private RewardLabViewModel? Lab => DataContext as RewardLabViewModel;

    private void OnApplyRequested(object? sender, MazeSquareEventArgs e) => Lab?.Apply(e.Cell);

    // The square line is read out when the keyboard moves the square, not on every pointer movement.
    private void OnReadoutUpdated(object? sender, DataTransferEventArgs e)
    {
        if (Maze.IsKeyboardFocused) Announce(sender as TextBlock);
    }

    // During a run the episodes line changes up to 20 times a second; it is read out once the run has ended.
    private void OnEpisodesUpdated(object? sender, DataTransferEventArgs e)
    {
        if (Lab is { IsRunning: false }) Announce(sender as TextBlock);
    }

    private static void Announce(TextBlock? text)
    {
        if (text is { Text.Length: > 0 } && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            (UIElementAutomationPeer.FromElement(text) ?? UIElementAutomationPeer.CreatePeerForElement(text))
                ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) Rearrange();
    }

    private void Rearrange()
    {
        // The chart grows with the text, so at 24 pt its axis labels leave room for the line (the Light 24 pt render).
        StepsChart.Height = Math.Max(220, FontSize * 11);
        if (ActualWidth > 0) Arrange(ActualWidth < Math.Max(StackBelowWidth, FontSize * StackBelowEms));
    }

    private void Arrange(bool stacked)
    {
        Gap.Width = new GridLength(stacked ? 0 : 16);
        FirstColumn.Width = new GridLength(stacked ? 1 : 3, GridUnitType.Star);
        SecondColumn.Width = stacked ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
        GapRow.Height = new GridLength(stacked ? 16 : 0);
        Grid.SetColumn(EpisodesPanel, stacked ? 0 : 2);
        Grid.SetRow(EpisodesPanel, stacked ? 2 : 0);
    }
}
