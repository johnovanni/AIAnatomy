using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AIAnatomy.App.Controls;
using AIAnatomy.Presentation.IdeaMap;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.App.Views;

/// <summary>
/// Lesson 4.1's lab: the family tree as bubbles (buttons, in file order so Tab follows it) and lines on a canvas the size
/// of the layout, top-down. The bubbles are measured at the current text size and handed to the view-model's
/// <c>Arrange</c>; this happens again when the text size changes, never when a bubble is chosen (the chosen bubble trades
/// padding for a thicker edge, so its size does not change). Enter on the chosen bubble opens its lesson; Space and the
/// mouse only choose. Lessons open through <see cref="LessonNavigation.NavigateRequestedEvent"/>.
/// </summary>
public partial class FamilyTreeView : UserControl
{
    private const double LineThickness = 2, Edge = 3, ChosenEdge = 6;
    private static readonly Thickness BubblePadding = new(10, 5, 10, 5);
    private static readonly Thickness ChosenPadding = new(10 - (ChosenEdge - Edge), 5 - (ChosenEdge - Edge), 10 - (ChosenEdge - Edge), 5 - (ChosenEdge - Edge));

    private FamilyTreeLabViewModel? _lab;
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private bool _measurePending;

    public FamilyTreeView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Attach(e.NewValue as FamilyTreeLabViewModel);
        Loaded += (_, _) => ScheduleMeasure();
        // The probe's size follows the body text size resource; a change means the bubbles must be measured again.
        DependencyPropertyDescriptor.FromProperty(TextBlock.FontSizeProperty, typeof(TextBlock))
            .AddValueChanged(SizeProbe, (_, _) => ScheduleMeasure());
    }

    private void Attach(FamilyTreeLabViewModel? lab)
    {
        if (_lab != null) _lab.PropertyChanged -= OnLabChanged;
        _lab = lab;
        if (_lab == null) return;
        _lab.PropertyChanged += OnLabChanged;
        SyncCard();
        ScheduleMeasure();
    }

    private void OnLabChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FamilyTreeLabViewModel.Layout): Redraw(); break;
            case nameof(FamilyTreeLabViewModel.ChosenId): SyncChoice(); break;
            case nameof(FamilyTreeLabViewModel.Card): SyncCard(); break;
        }
    }

    private void SyncCard()
    {
        bool chosen = _lab?.Card != null;
        CardBody.Visibility = chosen ? Visibility.Visible : Visibility.Collapsed;
        EmptyCard.Visibility = chosen ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnOpenLessonClick(object sender, RoutedEventArgs e)
    {
        if (_lab?.Card?.LessonId is { } id) Open(id);
    }

    private void Open(string lessonId) =>
        RaiseEvent(new NavigateRequestedEventArgs(LessonNavigation.NavigateRequestedEvent, this, lessonId));

    // ---- Measuring -------------------------------------------------------------------------------------------------

    // Coalesced, and never run from inside the view-model's own property changes.
    private void ScheduleMeasure()
    {
        if (_measurePending || _lab == null) return;
        _measurePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _measurePending = false;
            Measure();
        });
    }

    /// <summary>Builds a button for every bubble, measures them all at the current text size and asks for a layout.</summary>
    private void Measure()
    {
        if (_lab == null) return;
        TreeCanvas.Children.Clear();
        _buttons.Clear();
        var sizes = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        foreach (var bubble in _lab.Bubbles)
        {
            var button = MakeBubble(bubble, isRoot: _buttons.Count == 0);
            TreeCanvas.Children.Add(button);   // in the tree, so it inherits the font and resources before it is measured
            button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            sizes[bubble.Id] = (Math.Ceiling(button.DesiredSize.Width), Math.Ceiling(button.DesiredSize.Height));
            _buttons[bubble.Id] = button;
        }
        _lab.Arrange(sizes);   // raises Layout, which draws
    }

    private Button MakeBubble(FamilyBubble bubble, bool isRoot)
    {
        double small = (double)FindResource("SmallFontSize");
        var text = new StackPanel();
        if (bubble.HeadText.Length > 0)
        {
            // The lesson number, and "later" in words for a lesson not built yet (not colour alone).
            var head = new TextBlock { FontWeight = FontWeights.SemiBold, Text = bubble.HeadText };
            head.SetResourceReference(TextBlock.FontSizeProperty, "SmallFontSize");
            head.SetResourceReference(TextBlock.FontFamilyProperty, "NumberFont");
            text.Children.Add(head);
        }
        var label = new TextBlock { Text = bubble.Label, TextWrapping = TextWrapping.Wrap, MaxWidth = small * (isRoot ? 18 : 10) };
        label.SetResourceReference(TextBlock.FontSizeProperty, "SmallFontSize");
        if (!bubble.HasLesson) label.FontWeight = FontWeights.SemiBold;   // the branches and groups
        text.Children.Add(label);

        var button = new Button
        {
            Content = text,
            Tag = bubble.Id,
            Style = (Style)FindResource("FamilyBubbleButton"),
            Padding = BubblePadding,
            BorderThickness = new Thickness(Edge),
        };
        button.SetResourceReference(BorderBrushProperty, bubble.IsLater ? "PartLaterBrush" : PartResource.BrushKey(bubble.Part, "BorderBrush"));
        AutomationProperties.SetName(button, bubble.AutomationName);
        if (bubble.HasLesson) AutomationProperties.SetHelpText(button, "Enter chooses it; Enter again opens the lesson.");
        button.Click += (_, _) =>   // the mouse and Space
        {
            _lab?.Choose(bubble.Id);
            button.BringIntoView();
        };
        // Enter on the chosen bubble opens its lesson; Space and the mouse only choose (as on the idea map).
        button.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || _lab == null) return;
            if (_lab.ChooseOrOpen(bubble.Id) is { } lesson) Open(lesson);
            button.BringIntoView();
            e.Handled = true;
        };
        button.GotKeyboardFocus += (_, _) => button.BringIntoView();
        StyleBubble(button, bubble, chosen: false);
        return button;
    }

    private static void StyleBubble(Button button, FamilyBubble bubble, bool chosen)
    {
        button.SetResourceReference(BackgroundProperty, chosen ? "AccentBrush" : "ScreenBrush");
        button.SetResourceReference(ForegroundProperty, chosen ? "AccentTextBrush" : bubble.IsLater ? "MutedTextBrush" : "ScreenTextBrush");
        // The thicker edge takes its room from the padding, so the bubble's size, and the layout, stay as they are.
        button.BorderThickness = new Thickness(chosen ? ChosenEdge : Edge);
        button.Padding = chosen ? ChosenPadding : BubblePadding;
    }

    // ---- Drawing ---------------------------------------------------------------------------------------------------

    private void Redraw()
    {
        var layout = _lab?.Layout;
        TreeCanvas.Children.Clear();
        if (_lab == null || layout == null || _lab.Bubbles.Any(b => !_buttons.ContainsKey(b.Id))) return;   // the next measure draws

        TreeCanvas.Width = layout.Width;
        TreeCanvas.Height = layout.Height;
        foreach (var edge in layout.Edges) TreeCanvas.Children.Add(MakeLine(edge));
        foreach (var bubble in _lab.Bubbles)   // file order = Tab order
        {
            var node = layout.Nodes.Single(n => n.Id == bubble.Id);
            var button = _buttons[bubble.Id];
            Canvas.SetLeft(button, node.X);
            Canvas.SetTop(button, node.Y);
            TreeCanvas.Children.Add(button);
        }
        SyncChoice();
    }

    private static Path MakeLine(RoutedEdge edge)
    {
        var points = edge.Points.Select(p => new Point(p.X, p.Y)).ToList();
        var figure = new PathFigure { StartPoint = points.Count > 0 ? points[0] : default, IsClosed = false };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        var path = new Path { Data = new PathGeometry([figure]), StrokeThickness = LineThickness, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
        path.SetResourceReference(Shape.StrokeProperty, "BorderBrush");
        return path;
    }

    private void SyncChoice()
    {
        if (_lab == null) return;
        foreach (var bubble in _lab.Bubbles)
            if (_buttons.TryGetValue(bubble.Id, out var button)) StyleBubble(button, bubble, bubble.Id == _lab.ChosenId);
    }
}
