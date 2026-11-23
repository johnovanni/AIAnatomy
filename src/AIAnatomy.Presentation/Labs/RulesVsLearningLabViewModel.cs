using AIAnatomy.Core;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Toys;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// Hand-written rules beside a bigram model that learns the next letter from the fables, a chunk of training steps
/// at a time, so the output can be watched turning from gibberish into letter patterns.
/// </summary>
public sealed partial class RulesVsLearningLabViewModel : LabViewModel
{
    public const string Id = "RulesVsLearningLab";
    public const string DefaultStart = "The ";
    public const int StepsPerChunk = 20;
    public const int DefaultChunksPerRun = 20;
    public const int OutputLength = 120;

    /// <summary>
    /// Plain gradient descent on the mean loss moves a rarely used row very little, so a large rate is needed
    /// (decision D27). 20 is stable on the fables: the loss falls every chunk.
    /// </summary>
    public const double LearningRate = 20.0;

    private const int CorpusCharacters = 6000;

    private readonly RuleBasedWriter _writer = new();
    private readonly CharTokenizer _tokenizer;
    private readonly IReadOnlyList<int> _corpus;
    private readonly double _untrainedLoss;
    private BigramModel _model;
    private int _generation;
    private List<(double X, double Y)> _lossPoints = new();

    public RulesVsLearningLabViewModel(Func<string> fablesText)
    {
        ArgumentNullException.ThrowIfNull(fablesText);
        string text = fablesText();
        _tokenizer = CharTokenizer.FromText(text);
        _corpus = _tokenizer.Encode(text.Length > CorpusCharacters ? text[..CorpusCharacters] : text);
        _model = new BigramModel(_tokenizer, Seed);
        _untrainedLoss = CurrentLoss();
        Rules = _writer.Rules.Select((r, i) => $"{i + 1}. {r.Description}").ToList();
        ResetState();
    }

    /// <summary>The numbered rules, in the order the writer tries them.</summary>
    public IReadOnlyList<string> Rules { get; }

    [ObservableProperty] private string startText = DefaultStart;
    [ObservableProperty] private string ruleOutput = "";
    [ObservableProperty] private string learnedOutput = "";
    [ObservableProperty] private int stepsTrained;
    [ObservableProperty] private string lossText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool isLearning;
    [ObservableProperty] private IReadOnlyList<LineSeries> lossSeries = [];

    /// <summary>How many 20-step chunks one press of Learn trains (a test can lower it).</summary>
    public int ChunksPerRun { get; set; } = DefaultChunksPerRun;

    /// <summary>The loss of the untrained table: every next letter equally likely.</summary>
    public double UntrainedLoss => _untrainedLoss;

    /// <summary>The loss after the latest chunk (the untrained loss before any training).</summary>
    public double Loss => _lossPoints[^1].Y;

    public override string LabId => Id;

    partial void OnStartTextChanged(string value) => Refresh();

    protected override void OnSeedEdited() => Refresh();

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LearnAsync(CancellationToken token)
    {
        int generation = _generation;
        var model = _model;
        IsLearning = true;
        try
        {
            for (int chunk = 0; chunk < ChunksPerRun && !token.IsCancellationRequested; chunk++)
            {
                double loss = await Task.Run(() => model.TrainSteps(_corpus, StepsPerChunk, LearningRate), CancellationToken.None);
                if (generation != _generation) return;   // Reset (or leaving the lab) happened meanwhile
                StepsTrained += StepsPerChunk;
                _lossPoints.Add((StepsTrained, loss));
                Refresh();
                if (!double.IsFinite(loss)) break;
            }
        }
        finally
        {
            if (generation == _generation) IsLearning = false;
        }
    }

    public override void Reset()
    {
        LearnCancelCommand.Execute(null);
        _generation++;
        IsLearning = false;
        Seed = 1;
        StartText = DefaultStart;
        ResetState();
    }

    public override void Deactivate()
    {
        LearnCancelCommand.Execute(null);
        _generation++;
    }

    private void ResetState()
    {
        _model = new BigramModel(_tokenizer, Seed);
        StepsTrained = 0;
        _lossPoints = new List<(double X, double Y)> { (0, _untrainedLoss) };
        Refresh();
    }

    private double CurrentLoss()
    {
        var previous = _corpus.Take(_corpus.Count - 1).ToArray();
        var next = _corpus.Skip(1).ToArray();
        return Losses.CrossEntropy(Ops.Rows(_model.Logits, previous), next).Item();
    }

    private void Refresh()
    {
        LossSeries = [new LineSeries("loss", _lossPoints.ToList())];
        LossText = StepsTrained == 0
            ? $"Loss {NumberText.Short(_untrainedLoss)}: nothing learned yet, so every letter is equally likely."
            : $"Loss {NumberText.Short(Loss)} after {NumberText.Short(StepsTrained)} steps (lower is better; {NumberText.Short(_untrainedLoss)} is pure guessing).";
        if (string.IsNullOrEmpty(StartText))
        {
            Message = LabText.EmptyPrompt;
            RuleOutput = LearnedOutput = "";
            return;
        }
        Message = null;
        RuleOutput = LabText.ShowNewlines(_writer.Write(StartText, OutputLength));
        LearnedOutput = LabText.ShowNewlines(_model.Generate(StartText, OutputLength, new SeededRandom(Seed)));
    }
}
