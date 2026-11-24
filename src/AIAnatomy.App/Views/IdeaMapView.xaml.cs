using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AIAnatomy.App.Controls;
using AIAnatomy.Lessons;
using AIAnatomy.Presentation.IdeaMap;

namespace AIAnatomy.App.Views;

/// <summary>
/// The idea map: bubbles (buttons, in contents order so Tab follows it) and lines on a canvas the size of the layout.
/// The bubbles are measured at the current text size and handed to the view-model's <c>Arrange</c>; this happens again
/// when the text size or the mode changes, never when a progress mark changes (the mark text is updated in place, and
/// every lesson bubble was sized for its widest mark). Colours are all theme resources, so a theme switch needs no redraw.
/// </summary>
public partial class IdeaMapView : UserControl
{
    private const double LineThickness = 1.5, ChosenLineThickness = 3.5, ArrowSize = 9;
    private const string LoopId = ConnectionList.WholeLoop;

    private IdeaMapViewModel? _map;
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _heads = new(StringComparer.Ordinal);
    private readonly Canvas _highlights = new() { IsHitTestVisible = false };
    private bool _measurePending;

    public IdeaMapView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Attach(e.NewValue as IdeaMapViewModel);
        Loaded += (_, _) => ScheduleMeasure();
        // The probe's size follows the body text size resource; a change means the bubbles must be measured again.
        DependencyPropertyDescriptor.FromProperty(TextBlock.FontSizeProperty, typeof(TextBlock))
            .AddValueChanged(SizeProbe, (_, _) => ScheduleMeasure());
    }

    /// <summary>Diagnostic renders: the map is shown whole (no scroll bars) so a picture can include all of it.</summary>
    public void ShowWholeMap()
    {
        MapScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        MapScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private void Attach(IdeaMapViewModel? map)
    {
        if (_map != null)
        {
            _map.PropertyChanged -= OnMapChanged;
            _map.MarksChanged -= OnMarksChanged;
        }
        _map = map;
        if (_map == null) return;
        _map.PropertyChanged += OnMapChanged;
        _map.MarksChanged += OnMarksChanged;
        SyncToggles();
        SyncChoice();
        SyncCard();
        ScheduleMeasure();
    }

    private void SyncCard()
    {
        var card = _map?.Card;
        CardBody.Visibility = card == null ? Visibility.Collapsed : Visibility.Visible;
        ConnectsHeading.Visibility = card?.Sentences.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        OpenLessonButton.Visibility = card?.LessonId != null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IdeaMapViewModel.Layout): Redraw(); break;
            case nameof(IdeaMapViewModel.Bubbles): ScheduleMeasure(); break;   // a new map (mode); marks come as MarksChanged
            case nameof(IdeaMapViewModel.Mode): SyncToggles(); break;
            case nameof(IdeaMapViewModel.ChosenId):
            case nameof(IdeaMapViewModel.ChosenConnections): SyncChoice(); break;
            case nameof(IdeaMapViewModel.Card): SyncCard(); break;
        }
    }

    // A mark changed: the text is updated in place. Every lesson bubble was measured with room for its widest mark, so
    // no size changes and nothing is measured or laid out again (final review I2).
    private void OnMarksChanged(object? sender, EventArgs e)
    {
        if (_map == null) return;
        foreach (var bubble in _map.Bubbles)
        {
            if (_heads.TryGetValue(bubble.Id, out var head)) head.Text = HeadText(bubble);
            if (_buttons.TryGetValue(bubble.Id, out var button)) AutomationProperties.SetName(button, bubble.AutomationName);
        }
    }

    private static string HeadText(IdeaBubble bubble) =>
        (bubble.Number ?? "") + (bubble.MarkText.Length > 0 ? "  " + bubble.MarkText : "");

    private void OnRadialClick(object sender, RoutedEventArgs e) { if (_map != null) _map.Mode = MapMode.Radial; SyncToggles(); }
    private void OnLoopClick(object sender, RoutedEventArgs e) { if (_map != null) _map.Mode = MapMode.Loop; SyncToggles(); }

    private void SyncToggles()
    {
        RadialToggle.IsChecked = _map?.Mode == MapMode.Radial;
        LoopToggle.IsChecked = _map?.Mode == MapMode.Loop;
    }

    // ---- Measuring -------------------------------------------------------------------------------------------------

    // Coalesced: a mode change fires several property changes, and the view-model is not re-entered from inside them.
    private void ScheduleMeasure()
    {
        if (_measurePending || _map == null) return;
        _measurePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _measurePending = false;
            Measure();
        });
    }

    /// <summary>Builds a button for every bubble (sections and the centre too), measures them all and asks for a layout.</summary>
    private void Measure()
    {
        if (_map == null) return;
        MapCanvas.Children.Clear();
        _highlights.Children.Clear();
        _buttons.Clear();
        _heads.Clear();

        var sizes = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        foreach (var bubble in _map.Bubbles)
        {
            var button = MakeBubble(bubble);
            MapCanvas.Children.Add(button);        // in the tree, so it inherits the font and resources before it is measured
            button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            sizes[bubble.Id] = (Math.Ceiling(button.DesiredSize.Width), Math.Ceiling(button.DesiredSize.Height));
            _buttons[bubble.Id] = button;
        }
        // Line labels (Loop's flow labels) are measured as the chips they are drawn as, so the layout keeps room for them.
        var labelSizes = new Dictionary<string, (double Width, double Height)>(StringComparer.Ordinal);
        foreach (var text in _map.LineLabels)
        {
            var chip = MakeFlowChip(text);
            MapCanvas.Children.Add(chip);
            chip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            labelSizes[text] = (Math.Ceiling(chip.DesiredSize.Width), Math.Ceiling(chip.DesiredSize.Height));
            MapCanvas.Children.Remove(chip);
        }
        _map.Arrange(sizes, labelSizes);   // raises Layout, which draws
    }

    private Button MakeBubble(IdeaBubble bubble)
    {
        double small = (double)FindResource("SmallFontSize");
        var text = new StackPanel();
        if (bubble.Number != null || bubble.MarkText.Length > 0)
        {
            // Marks are words and symbols as well as colour, the contents tree's own: ✓ opened, ✔ checked, nothing before
            // a lesson is opened; "later" for what is not built (final review I1).
            TextBlock Head(string content)
            {
                var t = new TextBlock { FontWeight = FontWeights.SemiBold, Text = content };
                t.SetResourceReference(TextBlock.FontSizeProperty, "SmallFontSize");
                t.SetResourceReference(TextBlock.FontFamilyProperty, "NumberFont");
                return t;
            }
            var head = Head(HeadText(bubble));
            _heads[bubble.Id] = head;
            if (bubble.IsLesson && !bubble.IsLater)
            {
                // Hidden copies with every mark keep room for the widest one, so a new mark never changes the size.
                var line = new Grid();
                foreach (string mark in IdeaBubble.LessonMarkTexts)
                {
                    var room = Head($"{bubble.Number}  {mark}");
                    room.Visibility = Visibility.Hidden;
                    line.Children.Add(room);
                }
                line.Children.Add(head);
                text.Children.Add(line);
            }
            else text.Children.Add(head);
        }
        var label = new TextBlock { Text = bubble.Label, TextWrapping = TextWrapping.Wrap, MaxWidth = small * 11 };
        label.SetResourceReference(TextBlock.FontSizeProperty, "SmallFontSize");
        text.Children.Add(label);

        var button = new Button
        {
            Content = text,
            Tag = bubble.Id,
            Style = (Style)FindResource("BubbleButton"),
            Padding = new Thickness(10, 5, 10, 5),
        };
        button.SetResourceReference(BackgroundProperty, "ScreenBrush");
        button.SetResourceReference(ForegroundProperty, bubble.IsLater ? "MutedTextBrush" : "ScreenTextBrush");
        button.SetResourceReference(BorderBrushProperty, bubble.IsLater ? "PartLaterBrush" : PartResource.BrushKey(bubble.Part, "BorderBrush"));
        AutomationProperties.SetName(button, bubble.AutomationName);
        if (bubble.IsLesson) AutomationProperties.SetHelpText(button, "Enter chooses it; Enter again opens the lesson.");
        button.Click += (_, _) =>
        {
            _map?.Choose(bubble.Id);
            button.BringIntoView();
        };
        // Enter on the chosen bubble opens its lesson (final review M6); Space and the mouse only choose.
        button.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Enter || _map == null) return;
            _map.ChooseOrOpen(bubble.Id);
            button.BringIntoView();
            e.Handled = true;
        };
        button.GotKeyboardFocus += (_, _) => button.BringIntoView();
        return button;
    }

    // ---- Drawing ---------------------------------------------------------------------------------------------------

    private void Redraw()
    {
        var layout = _map?.Layout;
        if (_map == null || layout == null || _map.Bubbles.Any(b => !_buttons.ContainsKey(b.Id)))
        {
            // No layout yet, or one for a map whose bubbles are not built: the next measure draws.
            MapCanvas.Children.Clear();
            return;
        }

        MapCanvas.Children.Clear();
        MapCanvas.Width = layout.Width;
        MapCanvas.Height = layout.Height;

        if (layout.Group is { } g)
        {
            var frame = new Rectangle { Width = g.Width, Height = g.Height, RadiusX = 12, RadiusY = 12, StrokeThickness = 2, StrokeDashArray = [5, 3] };
            frame.SetResourceReference(Shape.StrokeProperty, "BorderBrush");
            Place(frame, g.X, g.Y);
        }

        foreach (var edge in layout.Edges.Where(_map.IsStructural))
            MapCanvas.Children.Add(MakeLine(edge, "BorderBrush", LineThickness, arrow: _map.Mode == MapMode.Loop));

        MapCanvas.Children.Add(_highlights);

        // Flow labels (Loop): an opaque chip on each line, where the layout kept room for it (LabelCentre); a line
        // without a kept place (no size was given) falls back to the clearest point along it.
        var taken = layout.Nodes.Select(n => new Rect(n.X, n.Y, n.Width, n.Height)).ToList();
        foreach (var edge in layout.Edges.Where(_map.IsStructural))
        {
            if (_map.FlowLabel(edge) is not { Length: > 0 } text || edge.Points.Count < 2) continue;
            var chip = MakeFlowChip(text);
            MapCanvas.Children.Add(chip);
            chip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            MapCanvas.Children.Remove(chip);
            var size = chip.DesiredSize;
            var spot = edge.LabelCentre is { } c
                ? new Rect(c.X - size.Width / 2, c.Y - size.Height / 2, size.Width, size.Height)
                : ChipSpot(edge, size, taken);
            taken.Add(spot);
            Place(chip, spot.X, spot.Y);
        }

        if (layout.Group is { } gr)
        {
            // The label sits on its own opaque chip, over any lines that enter the frame's top padding.
            var chip = new Border { Padding = new Thickness(8, 2, 8, 2), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1.5) };
            chip.SetResourceReference(Border.BackgroundProperty, "ScreenBrush");
            chip.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var label = new TextBlock { FontWeight = FontWeights.SemiBold };
            label.SetResourceReference(TextBlock.FontSizeProperty, "SmallFontSize");
            label.SetResourceReference(TextBlock.ForegroundProperty, "ScreenTextBrush");
            label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(IdeaMapViewModel.GroupLabel)) { Source = _map });
            chip.Child = label;
            Place(chip, gr.X + 10, gr.Y + 6);
        }

        foreach (var bubble in _map.Bubbles)   // contents order = Tab order
        {
            var node = layout.Nodes.Single(n => n.Id == bubble.Id);
            Place(_buttons[bubble.Id], node.X, node.Y);
        }
        SyncChoice();
    }

    private static Border MakeFlowChip(string text)
    {
        // A plate of its own colour with a thin edge, so it reads as a label over the lines in both themes.
        var chip = new Border { Padding = new Thickness(5, 1, 5, 1), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), IsHitTestVisible = false };
        chip.SetResourceReference(Border.BackgroundProperty, "LabelPlateBrush");
        chip.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap };
        label.SetResourceReference(TextBlock.FontSizeProperty, "SmallFontSize");
        label.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        chip.Child = label;
        return chip;
    }

    // The chip goes at the line's midpoint, or the nearest point along it where it covers no bubble or earlier chip.
    private static Rect ChipSpot(RoutedEdge edge, Size size, List<Rect> taken)
    {
        var pts = edge.Points.Select(p => new Point(p.X, p.Y)).ToList();
        double total = 0;
        for (int i = 1; i < pts.Count; i++) total += (pts[i] - pts[i - 1]).Length;
        Rect? first = null;
        foreach (double t in new[] { 0.5, 0.42, 0.58, 0.34, 0.66, 0.26, 0.74 })
        {
            double target = total * t, run = 0;
            var at = pts[^1];
            for (int i = 1; i < pts.Count; i++)
            {
                double seg = (pts[i] - pts[i - 1]).Length;
                if (run + seg >= target && seg > 0) { at = pts[i - 1] + (pts[i] - pts[i - 1]) * ((target - run) / seg); break; }
                run += seg;
            }
            var rect = new Rect(at.X - size.Width / 2, at.Y - size.Height / 2, size.Width, size.Height);
            first ??= rect;
            if (!taken.Any(r => r.Width > 0 && r.IntersectsWith(rect))) return rect;
        }
        return first!.Value;
    }

    private void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        MapCanvas.Children.Add(element);
    }

    private static UIElement MakeLine(RoutedEdge edge, string brushKey, double thickness, bool arrow)
    {
        var points = edge.Points.Select(p => new Point(p.X, p.Y)).ToList();
        var figure = new PathFigure { StartPoint = points[0], IsClosed = false };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        var path = new Path { Data = new PathGeometry([figure]), StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
        path.SetResourceReference(Shape.StrokeProperty, brushKey);
        if (!arrow || points.Count < 2) return path;

        // A small arrowhead at the end, pointing along the last stretch of the line.
        var tip = points[^1];
        var back = points.Take(points.Count - 1).Reverse().FirstOrDefault(p => (p - tip).Length > 3, points[^2]);
        var dir = tip - back;
        if (dir.Length < 1e-6) return path;
        dir.Normalize();
        var side = new Vector(-dir.Y, dir.X);
        var head = new Polygon { Points = [tip, tip - dir * ArrowSize + side * (ArrowSize / 2.5), tip - dir * ArrowSize - side * (ArrowSize / 2.5)], IsHitTestVisible = false };
        head.SetResourceReference(Shape.FillProperty, brushKey);
        var both = new Canvas { IsHitTestVisible = false };
        both.Children.Add(path);
        both.Children.Add(head);
        return both;
    }

    /// <summary>Draws the chosen bubble's connection lines above the structure and marks the chosen bubble.</summary>
    private void SyncChoice()
    {
        EmptyCard.Visibility = _map?.ChosenId == null ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (id, button) in _buttons)
        {
            bool chosen = id == _map?.ChosenId;
            button.SetResourceReference(BackgroundProperty, chosen ? "AccentBrush" : "ScreenBrush");
            var bubble = _map?.Bubbles.FirstOrDefault(b => b.Id == id);
            button.SetResourceReference(ForegroundProperty, chosen ? "AccentTextBrush" : bubble?.IsLater == true ? "MutedTextBrush" : "ScreenTextBrush");
        }

        _highlights.Children.Clear();
        if (_map?.Layout is not { } layout || _map.ChosenId == null) return;
        string centre = _map.Mode == MapMode.Radial ? _map.Bubbles[0].Id : LoopId;   // the whole loop is the centre bubble in Radial
        foreach (var c in _map.ChosenConnections)
        {
            string to = c.To == LoopId ? centre : c.To;
            foreach (var edge in layout.Edges.Where(e => (e.From == c.From && e.To == to) || (e.From == to && e.To == c.From)))
                _highlights.Children.Add(MakeLine(edge, "ConnectionBrush", ChosenLineThickness, arrow: false));
        }
    }
}
