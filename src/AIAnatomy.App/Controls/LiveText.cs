using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace AIAnatomy.App.Controls;

/// <summary>
/// Makes a <see cref="TextBlock"/> a polite live region whose new text is read out. Set <c>c:LiveText.IsLive="True"</c> on a
/// result line (bound or not): it sets <c>AutomationProperties.LiveSetting</c> to Polite and raises LiveRegionChanged whenever
/// the Text changes to a different, non-empty value (a changed Text alone is not announced by screen readers). It never
/// raises on layout or when the value is unchanged. The block is read with its own text, so give it no
/// <c>AutomationProperties.Name</c>; put a description in <c>AutomationProperties.HelpText</c> or a visible label.
/// </summary>
public static class LiveText
{
    public static readonly DependencyProperty IsLiveProperty =
        DependencyProperty.RegisterAttached("IsLive", typeof(bool), typeof(LiveText), new PropertyMetadata(false, OnIsLiveChanged));

    // The text last seen, so that an event with an unchanged value never announces.
    private static readonly DependencyProperty LastTextProperty =
        DependencyProperty.RegisterAttached("LastText", typeof(string), typeof(LiveText), new PropertyMetadata(null));

    public static bool GetIsLive(DependencyObject o) => (bool)o.GetValue(IsLiveProperty);
    public static void SetIsLive(DependencyObject o, bool v) => o.SetValue(IsLiveProperty, v);

    /// <summary>True when the text has changed to something there is to say (a cleared line says nothing).</summary>
    public static bool ShouldAnnounce(string? previous, string? current) =>
        !string.IsNullOrEmpty(current) && !string.Equals(previous, current, StringComparison.Ordinal);

    private static readonly DependencyPropertyDescriptor? TextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    private static void OnIsLiveChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not TextBlock text) return;
        if ((bool)e.NewValue)
        {
            text.SetValue(AutomationProperties.LiveSettingProperty, AutomationLiveSetting.Polite);
            text.Loaded += OnLoaded;
            text.Unloaded += OnUnloaded;
            if (text.IsLoaded) Attach(text);
        }
        else
        {
            text.Loaded -= OnLoaded;
            text.Unloaded -= OnUnloaded;
            Detach(text);
            text.ClearValue(AutomationProperties.LiveSettingProperty);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Attach((TextBlock)sender);
    private static void OnUnloaded(object sender, RoutedEventArgs e) => Detach((TextBlock)sender);

    private static void Attach(TextBlock text)
    {
        Detach(text);   // never twice
        text.SetValue(LastTextProperty, text.Text);
        TextDescriptor?.AddValueChanged(text, OnTextChanged);
    }

    private static void Detach(TextBlock text) => TextDescriptor?.RemoveValueChanged(text, OnTextChanged);

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBlock text) return;
        string? previous = (string?)text.GetValue(LastTextProperty);
        text.SetValue(LastTextProperty, text.Text);
        if (!ShouldAnnounce(previous, text.Text) || !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)) return;
        // The peer may not exist yet (the block may only just have become visible), so create it.
        (UIElementAutomationPeer.FromElement(text) ?? UIElementAutomationPeer.CreatePeerForElement(text))
            ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
