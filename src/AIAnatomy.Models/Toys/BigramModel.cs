using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;

namespace AIAnatomy.Models.Toys;

/// <summary>
/// The simplest learned language model: a table with one row of scores per previous character.
/// Starts all zero (every next character equally likely) and learns from every adjacent pair of the text.
/// </summary>
public sealed class BigramModel
{
    private readonly CharTokenizer _tokenizer;
    private readonly Parameter _table;

    /// <summary>The seed is kept for a uniform constructor shape; the all-zero start needs no randomness.</summary>
    public BigramModel(CharTokenizer tokenizer, int seed)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        Seed = seed;
        int v = tokenizer.VocabularySize;
        _table = new Parameter("bigram_logits", Tensor.Zeros(v, v).AsParameter());
    }

    public int Seed { get; }

    /// <summary>The [V,V] table of scores: row = previous character, column = next character.</summary>
    public Tensor Logits => _table.Value;

    /// <summary>Full-batch gradient descent on all adjacent pairs. Returns the loss of the last step.</summary>
    public double TrainSteps(IReadOnlyList<int> corpus, int steps, double rate)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        if (corpus.Count < 2) throw new ArgumentException("Need at least two tokens to form a pair.", nameof(corpus));
        var previous = new int[corpus.Count - 1];
        var next = new int[corpus.Count - 1];
        for (int i = 0; i < previous.Length; i++) { previous[i] = corpus[i]; next[i] = corpus[i + 1]; }

        var optimizer = new Sgd(new[] { _table }, rate);
        double loss = double.NaN;
        for (int s = 0; s < steps; s++)
        {
            optimizer.ZeroGrad();
            var l = Losses.CrossEntropy(Ops.Rows(_table.Value, previous), next);
            l.Backward();
            optimizer.Step();
            loss = l.Item();
        }
        return loss;
    }

    /// <summary>Writes <paramref name="count"/> new characters at temperature 1, each from the row of the character before it.</summary>
    public string Generate(string start, int count, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(rng);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var ids = _tokenizer.Encode(start.Length == 0 ? "\n" : start);
        int last = ids[^1];
        var settings = new SamplingSettings(1.0);
        var written = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            last = Sampling.Sample(Sampling.Probabilities(_table.Value.Row(last), settings), rng);
            written.Add(last);
        }
        return _tokenizer.Decode(written);
    }
}
