using System.Collections.ObjectModel;
using System.ComponentModel;
using AIAnatomy.Models.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// One line of the editable rule list. <see cref="Number"/> is its 1-based place in the list (the number the trace uses).
/// <see cref="Error"/> is the parse error or the rule set's problem, in words; <see cref="IsUsed"/> is false for a line
/// with either, and for an empty line.
/// </summary>
public sealed partial class RuleLine : ObservableObject
{
    internal RuleLine(int number, string text)
    {
        this.number = number;
        this.text = text;
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Label), nameof(DeleteToolTip))] private int number;
    [ObservableProperty] private string text;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UnusedText), nameof(StatusText))] private string? error;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UnusedText), nameof(StatusText))] private bool isUsed;

    /// <summary>"Rule 3", the box's name for a screen reader.</summary>
    public string Label => $"Rule {Number}";

    public string DeleteToolTip => $"Delete rule {Number}";

    /// <summary>"Not used" in words for a line that is left out, or null for a line in use.</summary>
    public string? UnusedText => IsUsed ? null : string.IsNullOrWhiteSpace(Text) ? "Empty, so not used." : "Not used.";

    /// <summary>What the view shows under a line that is left out, as one text for screen readers (the box's help text):
    /// "Not used. Put "then" before the conclusion.", "Empty, so not used.", or null for a line in use.</summary>
    public string? StatusText => UnusedText is not { } unused ? null : Error is { } error ? $"{unused} {error}" : unused;

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(UnusedText));
        OnPropertyChanged(nameof(StatusText));
    }
}

/// <summary>One yes/no question: a fact the rules use that no rule concludes. <see cref="Prompt"/> is "Has hair?".</summary>
public sealed partial class QuestionAnswer : ObservableObject
{
    internal QuestionAnswer(string fact, bool isYes)
    {
        Fact = fact;
        this.isYes = isYes;
        Prompt = fact.Length == 0 ? "?" : char.ToUpperInvariant(fact[0]) + fact[1..] + "?";
    }

    public string Fact { get; }
    public string Prompt { get; }
    [ObservableProperty] private bool isYes;
}

/// <summary>One fired rule in the trace: "Rule 5: mammal and eats meat, so carnivore." <see cref="RuleNumber"/> is the line number.</summary>
public sealed record TraceLine(int Pass, int RuleNumber, string Text);

/// <summary>The fired rules of one pass. <see cref="Heading"/> ("Pass 2") is null when the whole trace is one pass.</summary>
public sealed record TracePass(int Pass, string? Heading, IReadOnlyList<TraceLine> Lines);

/// <summary>
/// Lesson 4.2's lab: an animal-identifying expert system. The rules are an editable list of lines; the questions are the
/// facts the rules ask about; forward chaining gives the trace and the answer. Everything is worked out again at once
/// after each change (it is tiny). Screens: "trace" and "answer".
/// </summary>
/// <remarks>
/// Only lines that parse go into the <see cref="RuleSet"/>, so a rule's index there is not its line number. The lab keeps
/// the line of each rule-set index and maps every rule number the model gives (fired rules, problems) back to it, so the
/// trace and the list always show the same numbers.
/// </remarks>
public sealed partial class ExpertSystemLabViewModel : LabViewModel
{
    public const string Id = "ExpertSystemLab";
    public const int MaxRules = 40;
    public const string OpeningAnimal = "tiger";
    private const string TraceScreen = "trace", AnswerScreen = "answer";

    // The facts answered yes, remembered even while their question is gone (a typo in a rule hides a question; fixing it
    // brings the question back with its answer). Only the current questions' answers go to the chainer.
    private readonly HashSet<string> _remembered = new(StringComparer.Ordinal);
    private bool _quiet;

    public ExpertSystemLabViewModel()
    {
        Animals = AnimalRules.Descriptions.Select(d => d.Animal).ToList();
        LoadDefaults();
        ReportScreens();   // the baselines
    }

    public override string LabId => Id;

    public ObservableCollection<RuleLine> Rules { get; } = new();

    /// <summary>One question per askable fact, in the rule set's <see cref="RuleSet.AskableFacts"/> order.</summary>
    public ObservableCollection<QuestionAnswer> Questions { get; } = new();

    /// <summary>"At most 40 rules.", beside Add rule.</summary>
    public string RuleLimitText => $"At most {MaxRules} rules.";

    /// <summary>The animals there is a Describe button for (cheetah … albatross, bat).</summary>
    public IReadOnlyList<string> Animals { get; }

    [ObservableProperty] private IReadOnlyList<TraceLine> trace = [];
    [ObservableProperty] private IReadOnlyList<TracePass> tracePasses = [];
    [ObservableProperty] private string? traceMessage;
    [ObservableProperty] private string answerText = "";

    /// <summary>"Answered for a tiger." after Describe; "Your own answers." once an answer is changed by hand.</summary>
    [ObservableProperty] private string describedText = "";

    /// <summary>Answers every question as the animal would: yes for its facts, no for the rest.</summary>
    [RelayCommand]
    public void Describe(string animal)
    {
        var description = AnimalRules.Descriptions.FirstOrDefault(d => d.Animal == animal);
        if (description.Animal == null) throw new ArgumentException($"There is no animal called '{animal}'.", nameof(animal));
        _remembered.Clear();
        _remembered.UnionWith(description.Yes);
        _quiet = true;
        try
        {
            foreach (var q in Questions) q.IsYes = _remembered.Contains(q.Fact);
        }
        finally { _quiet = false; }
        DescribedText = $"Answered for {Article(animal)} {animal}.";
        Recompute();
    }

    /// <summary>Applies an edit to a line (the view calls it on Enter or when the box loses focus) and works everything out again.</summary>
    public void CommitRule(RuleLine line, string text)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!Rules.Contains(line)) throw new ArgumentException("That line is not in this lab's rule list.", nameof(line));
        line.Text = text ?? "";
        Recompute();
    }

    /// <summary>Adds an empty line at the end (not used until a rule is typed into it).</summary>
    [RelayCommand(CanExecute = nameof(CanAddRule))]
    public void AddRule()
    {
        if (!CanAddRule()) return;
        Rules.Add(new RuleLine(Rules.Count + 1, ""));
        Recompute();
    }

    private bool CanAddRule() => Rules.Count < MaxRules;

    /// <summary>
    /// Where the keyboard focus goes after a delete: the line that moved up into the deleted one's place, or the line
    /// before it when the last line was deleted, or null when no lines are left (the view then focuses Add rule).
    /// </summary>
    public static int? FocusIndexAfterDelete(int deletedIndex, int countAfter) =>
        countAfter == 0 ? null : Math.Clamp(deletedIndex, 0, countAfter - 1);

    /// <summary>Deletes a line; the lines after it move up a number.</summary>
    [RelayCommand]
    public void DeleteRule(RuleLine? line)
    {
        if (line == null || !Rules.Remove(line)) return;
        Recompute();
    }

    public override void Reset()
    {
        LoadDefaults();
        ReportScreens();
    }

    private void LoadDefaults()
    {
        Rules.Clear();
        foreach (var text in AnimalRules.DefaultRuleTexts) Rules.Add(new RuleLine(Rules.Count + 1, text));
        foreach (var q in Questions) q.PropertyChanged -= OnQuestionChanged;
        Questions.Clear();
        Describe(OpeningAnimal);
    }

    private void OnQuestionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_quiet || e.PropertyName != nameof(QuestionAnswer.IsYes) || sender is not QuestionAnswer q) return;
        if (q.IsYes) _remembered.Add(q.Fact); else _remembered.Remove(q.Fact);
        DescribedText = "Your own answers.";
        Recompute();
    }

    private void Recompute()
    {
        // Number the lines, parse each one, and keep the line of each rule that goes into the rule set.
        var parsed = new List<Rule>();
        var lineOf = new List<RuleLine>();
        for (int i = 0; i < Rules.Count; i++)
        {
            var line = Rules[i];
            line.Number = i + 1;
            if (string.IsNullOrWhiteSpace(line.Text)) { line.Error = null; continue; }
            var parse = RuleText.Parse(line.Text);
            line.Error = parse.Error;
            if (parse.Rule != null) { parsed.Add(parse.Rule); lineOf.Add(line); }
        }

        var set = new RuleSet(parsed);
        foreach (var group in set.Problems.GroupBy(p => p.RuleNumber))
            lineOf[group.Key - 1].Error = string.Join(" ", group.Select(p => p.Message));
        foreach (var line in Rules)
            line.IsUsed = line.Error == null && !string.IsNullOrWhiteSpace(line.Text);

        SyncQuestions(set.AskableFacts);

        var yes = Questions.Where(q => q.IsYes).Select(q => q.Fact).ToHashSet(StringComparer.Ordinal);
        var result = ForwardChainer.Run(set, yes);
        Trace = result.Fired.Select(f => new TraceLine(f.Pass, lineOf[f.RuleNumber - 1].Number, TextOf(lineOf[f.RuleNumber - 1].Number, f.Rule))).ToList();
        bool severalPasses = Trace.Select(t => t.Pass).Distinct().Count() > 1;
        TracePasses = Trace.GroupBy(t => t.Pass)
            .Select(g => new TracePass(g.Key, severalPasses ? $"Pass {g.Key}" : null, g.ToList())).ToList();
        TraceMessage = Trace.Count == 0 ? "No rule fired." : null;
        AnswerText = AnswerFor(result.Answers);
        AddRuleCommand.NotifyCanExecuteChanged();
        ReportScreens();
    }

    // Keeps the question objects for facts still asked (so their answers and bindings stay), adds new ones and removes
    // the rest, ending in the askable facts' order.
    private void SyncQuestions(IReadOnlyList<string> facts)
    {
        var keep = Questions.ToDictionary(q => q.Fact, StringComparer.Ordinal);
        for (int i = Questions.Count - 1; i >= 0; i--)
        {
            if (facts.Contains(Questions[i].Fact)) continue;
            Questions[i].PropertyChanged -= OnQuestionChanged;
            Questions.RemoveAt(i);
        }
        for (int i = 0; i < facts.Count; i++)
        {
            if (keep.TryGetValue(facts[i], out var existing))
            {
                int at = Questions.IndexOf(existing);
                if (at != i) Questions.Move(at, i);
            }
            else
            {
                var q = new QuestionAnswer(facts[i], _remembered.Contains(facts[i]));
                q.PropertyChanged += OnQuestionChanged;
                Questions.Insert(i, q);
            }
        }
    }

    private void ReportScreens()
    {
        ReportScreen(TraceScreen, Trace.Select(t => $"{t.Pass}|{t.Text}").ToArray());
        ReportScreen(AnswerScreen, AnswerText);
    }

    /// <summary>"Rule 5: mammal and eats meat, so carnivore."</summary>
    private static string TextOf(int number, Rule rule) =>
        $"Rule {number}: {string.Join(" and ", rule.Conditions.Select(c => (c.Negated ? "not " : "") + c.Fact))}, so {rule.Conclusion}.";

    private static string AnswerFor(IReadOnlyList<string> answers) => answers.Count switch
    {
        0 => "No animal fits these answers.",
        1 => $"Answer: {answers[0]}.",
        _ => $"The rules give more than one answer: {string.Join(", ", answers.Take(answers.Count - 1))} and {answers[^1]}.",
    };

    private static string Article(string word) => word.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(word[0])) ? "an" : "a";
}
