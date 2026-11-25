using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;

namespace AIAnatomy.Models.Recurrent;

/// <summary>Writes text with a memory network, one character at a time, carrying its memory forward.</summary>
public static class RnnSampling
{
    /// <summary>
    /// Reads <paramref name="start"/> (characters outside the vocabulary become the unknown token), then writes
    /// <paramref name="count"/> new characters and returns only those, as <see cref="Sampling.Generate"/> does.
    /// Each character is drawn from the softmax of the logits at the given temperature (0 = always the top choice).
    /// </summary>
    public static string Generate(ElmanRnn rnn, CharTokenizer tokenizer, string start, int count, double temperature, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(rnn);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(rng);
        if (start.Length == 0) throw new ArgumentException("Type some text to begin", nameof(start));
        if (tokenizer.VocabularySize != rnn.Config.VocabularySize)
            throw new ArgumentException($"The network has {rnn.Config.VocabularySize} tokens but the tokenizer has {tokenizer.VocabularySize}.", nameof(tokenizer));
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var settings = new SamplingSettings(Temperature: temperature);

        Tensor? memory = null;
        foreach (int id in tokenizer.Encode(start)) memory = Detached(rnn.Step(memory, new[] { id }));
        var written = new int[count];
        for (int i = 0; i < count; i++)
        {
            var logits = rnn.Logits(memory!).Row(0);
            written[i] = Sampling.Sample(Sampling.Probabilities(logits, settings), rng);
            memory = Detached(rnn.Step(memory, new[] { written[i] }));
        }
        return tokenizer.Decode(written);
    }

    /// <summary>The same numbers without the history of how they were computed, so a long text does not keep every step in memory.</summary>
    private static Tensor Detached(Tensor t) => Tensor.FromArray(t.Data, t.Shape);
}
