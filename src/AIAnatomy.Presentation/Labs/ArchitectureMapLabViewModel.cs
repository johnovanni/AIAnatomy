using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>The whole transformer as boxes that link to their lessons, with live next-character probabilities for typed text.</summary>
public sealed partial class ArchitectureMapLabViewModel : LabViewModel
{
    public const string Id = "ArchitectureMapLab";
    public const string DefaultText = "The fox and the ";
    private const int TopCount = 10;

    private readonly IActiveModelService _models;
    private readonly Func<string, Task> _navigate;

    public ArchitectureMapLabViewModel(IActiveModelService models, Func<string, Task> navigate)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        Refresh();
    }

    public override string LabId => Id;

    [ObservableProperty] private string text = DefaultText;
    [ObservableProperty] private string shownText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? notice;
    [ObservableProperty] private IReadOnlyList<LabeledValue> top10 = [];
    [ObservableProperty] private IReadOnlyList<NetworkNode> nodes = [];
    [ObservableProperty] private IReadOnlyList<NetworkEdge> edges = [];

    partial void OnTextChanged(string value) => Refresh();

    public override void Reset() => Text = DefaultText;

    public override void OnActiveModelChanged() => Refresh();

    /// <summary>What a click on a box does: opens that box's lesson (boxes without a lesson do nothing).</summary>
    [RelayCommand]
    private Task OpenNode(string? nodeId)
    {
        var link = Nodes.FirstOrDefault(n => n.Id == nodeId)?.LinkLessonId;
        return string.IsNullOrEmpty(link) ? Task.CompletedTask : _navigate(link);
    }

    private void Refresh()
    {
        var model = _models.Current;
        var config = model.Transformer.Config;
        string? top = null;
        int tokenCount = 0;

        if (string.IsNullOrEmpty(Text))
        {
            Message = LabText.EmptyPrompt;
            Notice = null;
            ShownText = "";
            Top10 = [];
        }
        else
        {
            var result = model.Predict(Text);
            Message = null;
            Notice = result.WasTruncated ? LabText.TruncationNote(config.ContextLength) : null;
            tokenCount = result.UsedTokenIds.Count;
            ShownText = LabText.ShowNewlines(model.Tokenizer.Decode(model.Tokenizer.Encode(Text)));
            var logits = result.Forward.Logits;
            var probabilities = Sampling.Probabilities(logits.Row(logits.Shape[0] - 1), new SamplingSettings(1.0));
            Top10 = Enumerable.Range(0, probabilities.Length)
                .OrderByDescending(i => probabilities[i]).ThenBy(i => i)
                .Take(TopCount)
                .Select(i => new LabeledValue(LabText.Visible(model.Tokenizer.TokenText(i)), probabilities[i]))
                .ToList();
            top = $"{Top10[0].Label} {NumberText.Percent(Top10[0].Value)}";
        }

        BuildDiagram(config, tokenCount, top);
    }

    private void BuildDiagram(AIAnatomy.Models.Transformers.TransformerConfig config, int tokenCount, string? top)
    {
        string layers = config.Layers == 1 ? "1 layer" : $"{config.Layers} layers";
        var list = new List<NetworkNode>
        {
            new("text", "Text", 0.10, 0.10, string.IsNullOrEmpty(Text) ? null : $"{LabText.CountScalars(Text)} characters", null),
            new("tokens", "Tokens", 0.32, 0.10, tokenCount > 0 ? $"{NumberText.Short(tokenCount)} ids" : null, "tokens"),
            new("embed", "Embeddings", 0.56, 0.10, $"{config.ModelWidth} numbers each", "embeddings"),
            new("position", "Position", 0.84, 0.10, $"{config.ContextLength} places", "position"),
            new("block", $"Block × {config.Layers}", 0.50, 0.36, layers, null),
            new("attention", "Attention", 0.10, 0.60, null, "attention"),
            new("heads", "Multi-head", 0.31, 0.60, $"{config.Heads} heads each", "multi-head"),
        };
        var links = new List<NetworkEdge>
        {
            new("text", "tokens", "split"), new("tokens", "embed", "look up"), new("embed", "block", "add"),
            new("position", "block", "add"), new("block", "attention", "inside"), new("attention", "heads", null),
        };
        string lastInBlock = "heads";
        if (config.UseFeedForward)
        {
            list.Add(new("feedforward", "Feed-forward", 0.57, 0.60, $"{config.FeedForwardWidth} hidden", "feed-forward"));
            links.Add(new("heads", "feedforward", null));
            lastInBlock = "feedforward";
        }
        list.Add(new("residual", "Residual stream", 0.86, 0.60, null, "residual-stream"));
        links.Add(new(lastInBlock, "residual", null));
        list.Add(new("norm", "Final norm", 0.86, 0.86, null, null));
        list.Add(new("output", "Output", 0.58, 0.86, $"{config.VocabularySize} scores", "output-sampling"));
        list.Add(new("probabilities", "Probabilities", 0.26, 0.86, top, "output-sampling"));
        links.Add(new("residual", "norm", null));
        links.Add(new("norm", "output", null));
        links.Add(new("output", "probabilities", "softmax"));
        Nodes = list;
        Edges = links;
    }
}
