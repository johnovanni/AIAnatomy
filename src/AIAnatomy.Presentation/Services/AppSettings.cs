namespace AIAnatomy.Presentation.Services;

/// <summary>
/// User settings. BodyFontSize is in points (the other sizes follow from it); Theme is "Dark" or "Light";
/// ActiveModelName is the library name of the chosen model; IdeaMapLayout is "Radial" or "Loop";
/// ScaleVersion says which text scale the file was saved under (0 = the old, larger scale); LastLessonId is the lesson
/// that was open last, reopened at the next start (null in files from before it was kept).
/// </summary>
public sealed record AppSettings(
    double BodyFontSize = 13,
    string Theme = "Dark",
    string? ActiveModelName = null,
    string IdeaMapLayout = "Radial",
    int ScaleVersion = 0,
    string? LastLessonId = null);
