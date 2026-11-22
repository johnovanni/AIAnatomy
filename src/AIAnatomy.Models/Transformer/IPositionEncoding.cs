using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>Tells the model where in the text each token sits.</summary>
public interface IPositionEncoding
{
    /// <summary>The [count, D] vectors for positions 0 .. count-1.</summary>
    Tensor Forward(int count);
}
