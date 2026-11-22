using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Transformers;

namespace AIAnatomy.Models.Language;

/// <summary>What one prediction used: the forward pass, the tokens it read, and whether older tokens had to be dropped.</summary>
public sealed record PredictionResult(ForwardResult Forward, IReadOnlyList<int> UsedTokenIds, bool WasTruncated, int DroppedTokenCount);

/// <summary>A transformer together with the tokenizer it was trained with.</summary>
public sealed class LanguageModel
{
    public LanguageModel(Transformer transformer, CharTokenizer tokenizer, ModelInfo info)
    {
        ArgumentNullException.ThrowIfNull(transformer);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(info);
        if (transformer.Config.VocabularySize != tokenizer.VocabularySize)
            throw new ArgumentException(
                $"The model has {transformer.Config.VocabularySize} tokens but the tokenizer has {tokenizer.VocabularySize}.", nameof(tokenizer));
        Transformer = transformer;
        Tokenizer = tokenizer;
        Info = info;
    }

    public Transformer Transformer { get; }
    public CharTokenizer Tokenizer { get; }
    public ModelInfo Info { get; }

    /// <summary>Runs the model on <paramref name="text"/>. Only the last <c>ContextLength</c> tokens are used.</summary>
    public PredictionResult Predict(string text, ForwardOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) throw new ArgumentException("Type some text to begin", nameof(text));
        return PredictTokens(Tokenizer.Encode(text), options);
    }

    internal PredictionResult PredictTokens(IReadOnlyList<int> ids, ForwardOptions? options = null)
    {
        int context = Transformer.Config.ContextLength;
        int dropped = Math.Max(0, ids.Count - context);
        var used = dropped == 0 ? ids : ids.Skip(dropped).ToArray();
        return new PredictionResult(Transformer.Forward(used, options), used, dropped > 0, dropped);
    }
}
