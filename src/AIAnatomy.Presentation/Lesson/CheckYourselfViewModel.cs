using AIAnatomy.Lessons;
using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation;

/// <summary>The "Check yourself" questions at the end of a lesson page. No score is kept: the lesson is marked Checked when
/// every question has been answered (right or not) or when the learner skips, and nothing about the answers is saved.</summary>
public sealed partial class CheckYourselfViewModel : ObservableObject
{
    private readonly string _lessonId;
    private readonly IProgressService _progress;

    public CheckYourselfViewModel(Lesson lesson, IProgressService progress, Action<string> requestAnchor)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(requestAnchor);
        _lessonId = lesson.Id;
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Questions = lesson.Questions.Select(q => new QuestionViewModel(q, requestAnchor, OnAnswered)).ToList();
    }

    public IReadOnlyList<QuestionViewModel> Questions { get; }

    [ObservableProperty] private bool isSkipped;

    [RelayCommand]
    private void Skip()
    {
        IsSkipped = true;
        _progress.MarkChecked(_lessonId);
    }

    [RelayCommand]
    private void ShowAgain() => IsSkipped = false;

    private void OnAnswered()
    {
        if (Questions.All(q => q.ChosenIndex != null)) _progress.MarkChecked(_lessonId);
    }
}

/// <summary>One question: the learner may change the answer as often as they like; the reason is shown each time.</summary>
public sealed partial class QuestionViewModel : ObservableObject
{
    private readonly Question _question;
    private readonly Action<string> _requestAnchor;
    private readonly Action _answered;

    internal QuestionViewModel(Question question, Action<string> requestAnchor, Action answered)
    {
        _question = question;
        _requestAnchor = requestAnchor;
        _answered = answered;
        Options = question.Answers.Select((a, i) => new AnswerOption(a, i == question.CorrectIndex)).ToList();
    }

    public string Prompt => _question.Prompt;
    public IReadOnlyList<string> Answers => _question.Answers;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsRight), nameof(Feedback))]
    private int? chosenIndex;

    /// <summary>Null until an answer is chosen.</summary>
    public bool? IsRight => ChosenIndex == null ? null : ChosenIndex == _question.CorrectIndex;

    /// <summary>The answers as buttons; the chosen one says so in words ("✓ ... (your answer: right)" or "✗ ... (your answer:
    /// not quite)"), not by colour alone.</summary>
    public IReadOnlyList<AnswerOption> Options { get; }

    /// <summary>"Right" or "Not quite", the answer chosen, then the reason; words, so it never relies on colour or sound.
    /// Naming the answer makes the text differ for each choice, so the live region speaks again when the learner picks a
    /// second wrong answer (final review I1; it speaks only when its text changes).</summary>
    public string? Feedback => ChosenIndex is not { } chosen ? null
        : $"{(IsRight == true ? "Right" : "Not quite")}: you chose “{Answers[chosen]}”. {_question.Why}";

    [RelayCommand]
    private void Choose(int index)
    {
        if (index < 0 || index >= Answers.Count) return;
        ChosenIndex = index;
        for (int i = 0; i < Options.Count; i++) Options[i].IsChosen = i == index;
        _answered();
    }

    [RelayCommand]
    private void See() => _requestAnchor(_question.See);
}

/// <summary>One answer button's text. A tick only on a chosen answer that is right; a cross on a chosen answer that is
/// not, with words either way (final review I4: a tick is the sign for "correct", so a wrong choice never carries one).</summary>
public sealed partial class AnswerOption(string text, bool isCorrect) : ObservableObject
{
    public string Text { get; } = text;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Label))]
    private bool isChosen;

    public string Label => !IsChosen ? Text : isCorrect ? $"✓ {Text} (your answer: right)" : $"✗ {Text} (your answer: not quite)";
}
