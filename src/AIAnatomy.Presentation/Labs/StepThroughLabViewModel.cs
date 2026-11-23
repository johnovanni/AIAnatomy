using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// One prediction followed through every part of the model, with the real numbers. Each step shows the row of the
/// <c>ForwardTrace</c> at the chosen token; nothing is computed again here except the probabilities, which come
/// from <see cref="Sampling.Probabilities"/> at temperature 1, as everywhere else.
/// </summary>
public sealed partial class StepThroughLabViewModel : LabViewModel
{
    public const string Id = "StepThroughLab";
    private const int TopCount = 10, WeightsPerHead = 5, LogitCount = 15;

    private readonly IActiveModelService _models;
    private string _lastDefault;
    private bool _moveToEnd = true;
    private bool _syncing;

    public StepThroughLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _lastDefault = text = LabText.StartingText(models.Current);
        Refresh();
    }

    public override string LabId => Id;

    [ObservableProperty] private string text;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    /// <summary>0-based position of the token that is followed; the picker shows it from 1.</summary>
    [ObservableProperty] private int positionIndex;
    [ObservableProperty] private IReadOnlyList<string> positionChoices = [];
    [ObservableProperty] private IReadOnlyList<PipelineStage> stages = [];
    /// <summary>0-based; the step marked "current step".</summary>
    [ObservableProperty] private int stageIndex;
    [ObservableProperty] private string stageCaption = "";

    public int StageCount => Stages.Count;

    partial void OnTextChanged(string value) { _moveToEnd = true; Refresh(); }
    // Refresh sets PositionIndex itself (under _syncing), and the picker can push -1 for a moment when its list is replaced:
    // neither may start another, nested Refresh.
    partial void OnPositionIndexChanged(int value)
    {
        if (_syncing || value < 0) return;
        Refresh();
    }

    partial void OnStagesChanged(IReadOnlyList<PipelineStage> value)
    {
        OnPropertyChanged(nameof(StageCount));
        NotifyButtons();
    }

    partial void OnStageIndexChanged(int value)
    {
        UpdateCaption();
        NotifyButtons();
    }

    private void NotifyButtons()
    {
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next()
    {
        if (Stages.Count == 0) return;
        StageIndex = Math.Min(StageIndex + 1, Stages.Count - 1);
    }
    private bool CanNext() => StageIndex < Stages.Count - 1;

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private void Previous() => StageIndex = Math.Max(StageIndex - 1, 0);
    private bool CanPrevious() => StageIndex > 0;

    public override void Reset()
    {
        StageIndex = 0;
        _lastDefault = LabText.StartingText(_models.Current);
        _moveToEnd = true;
        if (Text == _lastDefault) Refresh(); else Text = _lastDefault;
    }

    public override void OnActiveModelChanged()
    {
        if (Text == _lastDefault) { _lastDefault = LabText.StartingText(_models.Current); Text = _lastDefault; }
        _moveToEnd = true;
        Refresh();
    }

    private void SetChoices(IReadOnlyList<string> choices)
    {
        // Replaced only when the entries change, so a drop-down being used is not reset.
        if (!PositionChoices.SequenceEqual(choices)) PositionChoices = choices;
    }

    private void UpdateCaption() =>
        StageCaption = Stages.Count == 0 ? "" : $"Step {Math.Clamp(StageIndex, 0, Stages.Count - 1) + 1} of {Stages.Count}";

    private void Refresh()
    {
        var model = _models.Current;
        var config = model.Transformer.Config;
        if (string.IsNullOrEmpty(Text))
        {
            Message = LabText.EmptyPrompt;
            Notice = null;
            ShownText = "";
            SetChoices([]);
            Stages = [];
            StageIndex = 0;
            UpdateCaption();
            return;
        }
        Message = null;
        var prediction = model.Predict(Text);
        Notice = prediction.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
        var ids = prediction.UsedTokenIds;
        ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(ids));
        SetChoices(ids.Select((id, i) => $"{i + 1}:{LabText.Visible(model.Tokenizer.TokenText(id))}").ToList());
        int position = _moveToEnd ? ids.Count - 1 : Math.Clamp(PositionIndex, 0, ids.Count - 1);
        _moveToEnd = false;
        if (position != PositionIndex) { _syncing = true; PositionIndex = position; _syncing = false; }

        var list = Build(model, prediction, position);
        StageIndex = Math.Clamp(StageIndex, 0, list.Count - 1);
        Stages = list;
        UpdateCaption();
    }

    private List<PipelineStage> Build(LanguageModel model, PredictionResult prediction, int p)
    {
        var trace = prediction.Forward.Trace;
        var tokenizer = model.Tokenizer;
        string token = PositionChoices[p];
        var list = new List<PipelineStage>
        {
            new("Your text as tokens, each with its id",
                "The text is cut into tokens (here one character each), and every token has an id number. We follow the token marked \"followed\".",
                null, prediction.UsedTokenIds.Select((id, i) =>
                    new LabeledValue($"{i + 1}:{LabText.Visible(tokenizer.TokenText(id))}" + (i == p ? "  (followed)" : ""), id)).ToList()),
            new($"Token embedding of {token}", "The token's id picks one row of a learned table: a list of numbers that stands for this character.",
                trace.TokenEmbeddings.Row(p), null),
            new($"Position embedding of place {p + 1}",
                "The place in the text also has a list of numbers: learned during training, or a fixed pattern of sine and cosine waves, depending on the model.",
                trace.PositionEmbeddings.Row(p), null),
            new("Token plus position", "The two lists are added number by number. This is the vector that enters the first block.",
                trace.Input.Row(p), null),
        };
        for (int b = 0; b < trace.Blocks.Count; b++)
        {
            var block = trace.Blocks[b];
            string n = $"Block {b + 1}: ";
            list.Add(new(n + "layer norm before attention",
                "The vector is rescaled so its numbers average 0 and have a spread of 1, then a learned scale and shift are applied.", block.Norm1Out.Row(p), null));
            var items = new List<LabeledValue>();
            for (int h = 0; h < block.Heads.Count; h++)
            {
                var row = block.Heads[h].Weights.Row(p);
                foreach (int k in Enumerable.Range(0, p + 1).OrderByDescending(k => row[k]).ThenBy(k => k).Take(WeightsPerHead))
                    items.Add(new($"head {h + 1} looks at {PositionChoices[k]}", row[k]));
            }
            list.Add(new(n + "where each head looks",
                $"Each head spreads attention over the tokens up to this one (its weights add up to 1). Shown: up to {WeightsPerHead} largest weights of each head.", null, items));
            list.Add(new(n + "attention output", "The heads' results are joined and mixed by the output projection into one vector.", block.AttentionOut.Row(p), null));
            list.Add(new(n + "residual after attention",
                "The attention output is added to the vector that entered the block. Nothing is replaced, only added to.", block.ResidualMid.Row(p), null));
            if (block.Norm2Out == null) continue;
            list.Add(new(n + "layer norm before feed-forward", "The same kind of tidying again, with its own learned scale and shift.", block.Norm2Out.Row(p), null));
            var hidden = block.FeedForwardHidden!.Row(p);
            list.Add(new(n + $"feed-forward hidden layer ({TopCount} largest)",
                $"The vector is widened into {hidden.Length} neurons. Shown: the {TopCount} with the largest size (ignoring the sign), largest first, each labelled with its neuron number (counted from 1, as in the Feed-forward lab), after the GELU activation.",
                null, Enumerable.Range(0, hidden.Length).OrderByDescending(i => Math.Abs(hidden[i])).ThenBy(i => i).Take(TopCount)
                    .Select(i => new LabeledValue($"neuron {i + 1}", hidden[i])).ToList()));
            list.Add(new(n + "feed-forward output", "The neurons are combined back into a vector of the model's width.", block.FeedForwardOut!.Row(p), null));
            list.Add(new(n + "residual after feed-forward", "That output is added to the running vector. This is the block's result.", block.ResidualOut.Row(p), null));
        }
        var logits = trace.Logits.Row(p);
        var probabilities = Sampling.Probabilities(logits, new SamplingSettings(1.0));
        list.Add(new("Final layer norm", "One last tidying of the vector before the output layer.", trace.FinalNorm.Row(p), null));
        // The 15 highest scores, as the Sampling lab shows; all of them (in vocabulary order) are in the tooltip (final review M5).
        list.Add(new($"Logits ({LogitCount} highest)",
            $"The output layer gives one score for each of the {logits.Length} tokens in the vocabulary. Higher means the model finds it more likely. " +
            $"Shown: the {LogitCount} highest scores, highest first. Point at this step to see all {logits.Length} scores, in vocabulary order.",
            null,
            Enumerable.Range(0, logits.Length).OrderByDescending(i => logits[i]).ThenBy(i => i).Take(LogitCount)
                .Select(i => new LabeledValue(LabText.Visible(tokenizer.TokenText(i)), logits[i])).ToList(),
            logits.Select((v, i) => new LabeledValue(LabText.Visible(tokenizer.TokenText(i)), v)).ToList()));
        list.Add(new($"Probabilities ({TopCount} largest)",
            $"The scores are turned into probabilities that add up to 1 (softmax, temperature 1). Shown: the {TopCount} most likely next tokens.",
            null, Enumerable.Range(0, probabilities.Length).OrderByDescending(i => probabilities[i]).ThenBy(i => i).Take(TopCount)
                .Select(i => new LabeledValue(LabText.Visible(tokenizer.TokenText(i)), probabilities[i])).ToList()));
        return list;
    }
}
