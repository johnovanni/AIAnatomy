using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>
/// Fixed wave patterns: PE(pos, 2i) = sin(pos / 10000^(2i/d)), PE(pos, 2i+1) = cos(pos / 10000^(2i/d)).
/// Nothing here is learned.
/// </summary>
public sealed class SinusoidalPositions : Module, IPositionEncoding
{
    private readonly int _width;

    public SinusoidalPositions(int width) => _width = width;

    public Tensor Forward(int count)
    {
        var t = Tensor.Zeros(count, _width);
        for (int pos = 0; pos < count; pos++)
            for (int j = 0; j < _width; j++)
            {
                int pair = j / 2 * 2; // the "2i" in the formula
                double angle = pos / Math.Pow(10000.0, (double)pair / _width);
                t.Data[pos * _width + j] = j % 2 == 0 ? Math.Sin(angle) : Math.Cos(angle);
            }
        return t;
    }

    public override IEnumerable<Parameter> Parameters() => Enumerable.Empty<Parameter>();
}
