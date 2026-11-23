using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>The whole transformer as boxes that link to their lessons, with live next-character probabilities for typed text.</summary>
public sealed partial class ArchitectureMapLabViewModel : LabViewModel
{
    public const string Id = "ArchitectureMapLab";
    public const string DefaultText = "The fox and the ";
    private const int TopCount = 10;

    private readonly IActiveModelService _models;

    public ArchitectureMapLabViewModel(IActiveModelService models)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
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
    /// <summary>The line under the diagram that says what the short arrows between the stream and the sublayers mean (R37).</summary>
    [ObservableProperty] private string diagramCaption = "";

    partial void OnTextChanged(string value) => Refresh();

    public override void Reset() => Text = DefaultText;

    public override void OnActiveModelChanged() => Refresh();

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
            // Only the tokens the model uses, with spaces as ␣ as the caption says (final review I4).
            ShownText = LabText.Visible(model.Tokenizer.Decode(result.UsedTokenIds));
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

    // One block in the pre-norm design (D3): the residual stream runs through the whole block; the attention sublayer
    // (several heads side by side) and the feed-forward sublayer each read a normalised copy of it and add their result back.
    private void BuildDiagram(AIAnatomy.Models.Transformers.TransformerConfig config, int tokenCount, string? top)
    {
        string layers = config.Layers == 1 ? "1 layer" : $"{config.Layers} layers";
        string heads = config.Heads == 1 ? "1 head" : $"{config.Heads} heads";
        var list = new List<NetworkNode>
        {
            new("text", "Text", 0.22, 0.06, string.IsNullOrEmpty(Text) ? null : $"{LabText.CountScalars(Text)} characters", null),
            new("tokens", "Tokens", 0.70, 0.06, tokenCount > 0 ? $"{NumberText.Short(tokenCount)} ids" : null, "tokens"),
            new("embed", "Embeddings", 0.22, 0.18, $"{config.ModelWidth} numbers each", "embeddings"),
            new("position", "Position", 0.76, 0.18, $"{config.ContextLength} places", "position"),
            new("block", $"Block × {config.Layers}", 0.50, 0.30, layers, null),
            new("residual", "Residual stream", 0.50, 0.43, "runs through the block", "residual-stream"),
            new("multihead", "Multi-head attention", 0.22, 0.57, $"{heads}, norm first", "multi-head"),
            new("attention", "Attention", 0.22, 0.71, "one head", "attention"),
        };
        var links = new List<NetworkEdge>
        {
            new("text", "tokens", "split"), new("tokens", "embed", "look up"), new("embed", "block", "add"),
            new("position", "block", "add"), new("block", "residual", "inside"),
            // The head count is in the Multi-head box itself: a label on this short arrow was pushed beside the
            // "after the last block" arrow and seemed to name it.
            new("attention", "multihead", null),
            // Each sublayer reads a normalised copy of the stream and adds its result back (final review M1); the diagram
            // draws a pair of opposite arrows side by side. They carry no text: on such short arrows the labels were pushed
            // beside other arrows and seemed to name them, so DiagramCaption says it instead (R37).
            new("residual", "multihead", null),
            new("multihead", "residual", null),
        };
        if (config.UseFeedForward)
        {
            list.Add(new("feedforward", "Feed-forward", 0.78, 0.57, $"norm first, {config.FeedForwardWidth} hidden", "feed-forward"));
            links.Add(new("residual", "feedforward", null));
            links.Add(new("feedforward", "residual", null));
        }
        list.Add(new("norm", "Final norm", 0.50, 0.77, null, null));
        list.Add(new("output", "Output", 0.27, 0.90, $"{config.VocabularySize} scores", "output-sampling"));
        list.Add(new("probabilities", "Probabilities", 0.73, 0.90, top, "output-sampling"));
        links.Add(new("residual", "norm", "after the last block"));
        links.Add(new("norm", "output", null));
        links.Add(new("output", "probabilities", "softmax"));
        Nodes = list;
        Edges = links;
        DiagramCaption = config.UseFeedForward
            ? "Attention and feed-forward each read a normalised copy of the residual stream and add their result back."
            : "Attention reads a normalised copy of the residual stream and adds its result back.";
    }
}
