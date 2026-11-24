namespace AIAnatomy.Presentation;

/// <summary>
/// One connection of a lesson, as a chip on its page: the idea at the other end (a lesson id, a map-only idea id, or
/// <see cref="AIAnatomy.Lessons.ConnectionList.WholeLoop"/>), its name, its part key (null unless it is a lesson with one)
/// and the sentence that explains the link.
/// </summary>
public sealed record ConnectionChip(string OtherId, string Label, string? Part, string Sentence, bool IsWholeLoop);
