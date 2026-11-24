namespace AIAnatomy.Presentation;

/// <summary>Says which screen (a framed output panel of a lab) has changed because of what the learner did.</summary>
public sealed class ScreenChangedEventArgs(string screen) : EventArgs
{
    public string Screen { get; } = screen;
}
