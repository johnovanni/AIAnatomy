namespace AIAnatomy.Lessons;

/// <summary>One Check yourself question. <see cref="See"/> is an anchor in the lesson; <see cref="CheckedBy"/>
/// names the unit test that computes the real answer, when there is one.</summary>
public sealed record Question(
    string Prompt, IReadOnlyList<string> Answers, int CorrectIndex, string Why, string See, string? CheckedBy);
