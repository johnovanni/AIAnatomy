using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>One learnable vector per position, as in GPT-2.</summary>
public sealed class LearnedPositions : Module, IPositionEncoding
{
    private readonly Parameter _table;

    public LearnedPositions(string name, int contextLength, int width, SeededRandom rng) =>
        _table = Weights(name, rng, contextLength, width);

    public Tensor Forward(int count) => Ops.Rows(_table.Value, Enumerable.Range(0, count).ToArray());

    public override IEnumerable<Parameter> Parameters() { yield return _table; }
}
