namespace AIAnatomy.Lessons;

/// <summary>A named way into a lesson's lab or text; <see cref="Anchor"/> is what Check yourself questions can point to.</summary>
public sealed record LessonBranch(string Label, string Anchor);
