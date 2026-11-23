namespace AIAnatomy.Presentation.Labs;

/// <summary>Stands in for a lesson whose lab is not built yet.</summary>
public sealed class ComingLaterLabViewModel : LabViewModel
{
    public const string Id = "ComingLaterLab";

    public override string LabId => Id;
    public string Title { get; init; } = "Coming later";
    public string Message { get; init; } = "This lab is being built.";

    public override void Reset() { }
}
