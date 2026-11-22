using System.Text;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;

namespace AIAnatomy.Models.Inspection;

/// <summary>One place in the text where a feed-forward neuron fired. <c>Context</c> is up to 12 characters ending at <c>Position</c>.</summary>
public sealed record NeuronActivation(int Position, double Value, string Context);

public static class NeuronProbe
{
    private const int ContextLength = 12;

    /// <summary>
    /// Runs <paramref name="text"/> through the model (in consecutive windows of the model's context length) and returns the
    /// <paramref name="count"/> positions where the chosen feed-forward neuron was most active, strongest first.
    /// </summary>
    public static IReadOnlyList<NeuronActivation> TopContexts(LanguageModel m, string text, int layer, int neuron, int count)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(text);
        var config = m.Transformer.Config;
        if (!config.UseFeedForward || config.Layers == 0)
            throw new ArgumentException("This model has no feed-forward layers, so it has no neurons to probe.", nameof(m));
        if (layer < 0 || layer >= config.Layers)
            throw new ArgumentOutOfRangeException(nameof(layer), $"Layer {layer} does not exist: the model has {config.Layers} layers.");
        if (neuron < 0 || neuron >= config.FeedForwardWidth)
            throw new ArgumentOutOfRangeException(nameof(neuron), $"Neuron {neuron} does not exist: layers have {config.FeedForwardWidth} neurons.");
        if (count <= 0) return Array.Empty<NeuronActivation>();

        var pieces = new List<string>();   // one entry per token, in the same order as Encode
        foreach (Rune rune in TextNormalizer.Normalize(text).EnumerateRunes()) pieces.Add(rune.ToString());
        var ids = m.Tokenizer.Encode(text);

        var found = new List<NeuronActivation>(ids.Count);
        for (int start = 0; start < ids.Count; start += config.ContextLength)
        {
            var window = ids.Skip(start).Take(config.ContextLength).ToArray();
            var hidden = m.Transformer.Forward(window).Trace.Blocks[layer].FeedForwardHidden
                ?? throw new InvalidOperationException("The block recorded no feed-forward activations.");
            for (int i = 0; i < window.Length; i++)
            {
                int position = start + i;
                int from = Math.Max(0, position - ContextLength + 1);
                found.Add(new NeuronActivation(position, hidden[i, neuron], string.Concat(pieces.Skip(from).Take(position - from + 1))));
            }
        }
        return found.OrderByDescending(a => a.Value).ThenBy(a => a.Position).Take(count).ToList();
    }
}
