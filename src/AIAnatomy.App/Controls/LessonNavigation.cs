using System.Windows;

namespace AIAnatomy.App.Controls;

/// <summary>Arguments of <see cref="LessonNavigation.NavigateRequestedEvent"/>: the lesson to open.</summary>
public sealed class NavigateRequestedEventArgs(RoutedEvent routedEvent, object source, string lessonId) : RoutedEventArgs(routedEvent, source)
{
    public string LessonId { get; } = lessonId;
}

public delegate void NavigateRequestedEventHandler(object sender, NavigateRequestedEventArgs e);

/// <summary>
/// The bubbling event a lesson link (in <see cref="MarkdownView"/>) or a linked box (in <see cref="NetworkDiagram"/>)
/// raises. MainWindow handles it once for the whole window and opens the lesson.
/// </summary>
public static class LessonNavigation
{
    public static readonly RoutedEvent NavigateRequestedEvent = EventManager.RegisterRoutedEvent(
        "NavigateRequested", RoutingStrategy.Bubble, typeof(NavigateRequestedEventHandler), typeof(LessonNavigation));
}
