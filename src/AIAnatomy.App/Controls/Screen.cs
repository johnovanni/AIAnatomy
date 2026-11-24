using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AIAnatomy.Presentation;

namespace AIAnatomy.App.Controls;

/// <summary>
/// A framed output panel of a lab. When the lab view-model says this screen's value changed (ScreenChanged with this
/// <see cref="ScreenName"/>), the edge, which rests dimmed in the lesson's part colour (with a faint glow in the Dark theme), brightens to
/// full for under a second. Pulses closer than 333 ms are dropped, and nothing animates when Windows "Show animations" is off.
/// </summary>
public class Screen : ContentControl
{
    /// <summary>The lesson's part key (for example "attention"), inherited by every screen below the lesson page; null gives the neutral edge.</summary>
    public static readonly DependencyProperty PartBrushProperty = DependencyProperty.RegisterAttached(
        "PartBrush", typeof(string), typeof(Screen),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnPartChanged));

    public static string? GetPartBrush(DependencyObject o) => (string?)o.GetValue(PartBrushProperty);
    public static void SetPartBrush(DependencyObject o, string? v) => o.SetValue(PartBrushProperty, v);

    public static readonly DependencyProperty ScreenNameProperty =
        DependencyProperty.Register(nameof(ScreenName), typeof(string), typeof(Screen), new PropertyMetadata(""));

    /// <summary>How much of the light-up is showing, 0 to 1 (animated; set to 1 by hand to show the lit look).</summary>
    public static readonly DependencyProperty LitProperty =
        DependencyProperty.Register(nameof(Lit), typeof(double), typeof(Screen), new PropertyMetadata(0.0, (o, e) => ((Screen)o).OnLitChanged()));

    /// <summary>Opacity of the part-coloured edge: dimmed at rest, full when lit (read by the template).</summary>
    public static readonly DependencyProperty EdgeOpacityProperty =
        DependencyProperty.Register(nameof(EdgeOpacity), typeof(double), typeof(Screen), new PropertyMetadata(RestEdgeOpacity));

    public double EdgeOpacity { get => (double)GetValue(EdgeOpacityProperty); private set => SetValue(EdgeOpacityProperty, value); }

    /// <summary>Thickness of the edge overlay: 2 px at rest, 4 px when fully lit (it grows inward over the padding, so nothing shifts).</summary>
    public static readonly DependencyProperty EdgeThicknessProperty =
        DependencyProperty.Register(nameof(EdgeThickness), typeof(Thickness), typeof(Screen), new PropertyMetadata(new Thickness(RestEdgeWidth)));

    public Thickness EdgeThickness { get => (Thickness)GetValue(EdgeThicknessProperty); private set => SetValue(EdgeThicknessProperty, value); }

    public const double RestEdgeWidth = 2;
    public const double LitEdgeWidth = 4;

    /// <summary>How strong the edge is when the screen is not lit.</summary>
    public const double RestEdgeOpacity = 0.55;

    private void OnLitChanged()
    {
        double lit = Math.Clamp(Lit, 0, 1);
        EdgeOpacity = RestEdgeOpacity + (1 - RestEdgeOpacity) * lit;
        EdgeThickness = new Thickness(RestEdgeWidth + (LitEdgeWidth - RestEdgeWidth) * lit);
    }

    public string ScreenName { get => (string)GetValue(ScreenNameProperty); set => SetValue(ScreenNameProperty, value); }
    public double Lit { get => (double)GetValue(LitProperty); set => SetValue(LitProperty, value); }

    /// <summary>Pulses closer together than this are dropped (never more than 3 a second).</summary>
    public const int MinPulseGapMs = 333;
    private static readonly Duration PulseDuration = new(TimeSpan.FromMilliseconds(800));

    /// <summary>The theme's glow strength (<c>ScreenGlowOpacity</c>: 0.45 in Dark, 0 in Light), followed as a resource so a
    /// theme switch redraws the glow at once (final review M2).</summary>
    private static readonly DependencyProperty GlowOpacityProperty = DependencyProperty.Register(
        "GlowOpacity", typeof(double), typeof(Screen), new PropertyMetadata(0.0, (o, _) => ((Screen)o).UpdateEffect()));

    /// <summary>The edge's part brush as a resource, followed so a theme switch recolours the glow at once (M2).</summary>
    private static readonly DependencyProperty GlowBrushProperty = DependencyProperty.Register(
        "GlowBrush", typeof(Brush), typeof(Screen), new PropertyMetadata(null, (o, _) => ((Screen)o).UpdateEffect()));

    private static readonly System.Windows.Data.IValueConverter ReadableName = new ReadableNameConverter();

    private LabViewModel? _lab;
    private Border? _glow;
    private long _lastPulseTicks = long.MinValue;

    static Screen()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Screen), new FrameworkPropertyMetadata(typeof(Screen)));
        FocusableProperty.OverrideMetadata(typeof(Screen), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Screen), new FrameworkPropertyMetadata(false));
    }

    public Screen()
    {
        // Named in words after the screen ("Real heads screen", final review M11) unless the XAML gives it a name of its
        // own (a local value replaces this binding).
        SetBinding(AutomationProperties.NameProperty, new System.Windows.Data.Binding(nameof(ScreenName))
        {
            Source = this,
            Converter = ReadableName,
        });
        SetResourceReference(GlowOpacityProperty, "ScreenGlowOpacity");
        SetResourceReference(GlowBrushProperty, PartResource.BrushKey(null, "AccentBrush"));
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) => { if (IsLoaded) Attach(); };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _glow = GetTemplateChild("PART_Glow") as Border;
        UpdateGlow();
    }

    private static void OnPartChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is Screen s) s.UpdateGlow();
    }

    private void Attach()
    {
        var lab = FindLab();
        if (ReferenceEquals(lab, _lab)) return;
        Detach();
        _lab = lab;
        if (_lab != null) _lab.ScreenChanged += OnScreenChanged;
        UpdateGlow();
    }

    private void Detach()
    {
        if (_lab != null) _lab.ScreenChanged -= OnScreenChanged;
        _lab = null;
    }

    // The lab view-model is this control's DataContext or that of the nearest ancestor that has one.
    private LabViewModel? FindLab()
    {
        DependencyObject? o = this;
        while (o != null)
        {
            if (o is FrameworkElement { DataContext: LabViewModel lab }) return lab;
            o = LogicalTreeHelper.GetParent(o) ?? (o is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(o) : null);
        }
        return null;
    }

    // Called on whatever thread the lab reported from.
    private void OnScreenChanged(object? sender, ScreenChangedEventArgs e)
    {
        if (e.Screen != ScreenName) return;
        if (Dispatcher.CheckAccess()) Pulse();
        else Dispatcher.BeginInvoke(Pulse);
    }

    /// <summary>Lights the screen up once (unless animations are off or the last pulse was under 333 ms ago).</summary>
    public void Pulse()
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        long now = Environment.TickCount64;
        if (_lastPulseTicks != long.MinValue && now - _lastPulseTicks < MinPulseGapMs) return;
        _lastPulseTicks = now;
        UpdateGlow();
        // From the current value (no snap to 0 when a pulse comes during the last one); back to rest when done.
        var up = new DoubleAnimationUsingKeyFrames { Duration = PulseDuration, FillBehavior = FillBehavior.Stop };
        up.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.15)));
        up.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        BeginAnimation(LitProperty, up);
    }

    // The overlay takes the part's colour (AccentBrush with no part) as a resource reference, and so does GlowBrush, whose
    // change (a new part, or a theme switch) rebuilds the glow; so does a change of the theme's glow strength.
    private void UpdateGlow()
    {
        string key = PartResource.BrushKey(GetPartBrush(this), "AccentBrush");
        SetResourceReference(GlowBrushProperty, key);
        _glow?.SetResourceReference(Border.BorderBrushProperty, key);
        UpdateEffect();
    }

    // A faint glow at rest (the whole edge, glow included, is dimmed with it) that brightens with the edge; none in Light.
    private void UpdateEffect()
    {
        if (_glow == null) return;
        Color color = ((GetValue(GlowBrushProperty) ?? _glow.BorderBrush) as SolidColorBrush)?.Color ?? Colors.White;
        double opacity = (double)GetValue(GlowOpacityProperty);
        _glow.Effect = opacity > 0 ? new DropShadowEffect { Color = color, BlurRadius = 18, ShadowDepth = 0, Opacity = opacity } : null;
    }

    private sealed class ReadableNameConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            ScreenNames.Readable(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
