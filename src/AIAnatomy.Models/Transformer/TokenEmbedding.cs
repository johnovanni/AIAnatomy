using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>A table with one learnable row (a vector of ModelWidth numbers) per token.</summary>
public sealed class TokenEmbedding : Module
{
    public TokenEmbedding(string name, int vocabularySize, int width, SeededRandom rng) =>
        Table = Weights(name, rng, vocabularySize, width);

    public Parameter Table { get; }

    /// <summary>The [T, D] matrix of rows for the given token ids.</summary>
    public Tensor Forward(IReadOnlyList<int> ids) => Ops.Rows(Table.Value, ids);

    public override IEnumerable<Parameter> Parameters() { yield return Table; }
}
