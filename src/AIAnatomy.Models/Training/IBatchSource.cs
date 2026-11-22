using AIAnatomy.Core;

namespace AIAnatomy.Models.Training;

/// <summary>Supplies training and validation windows. A window of n+1 tokens means: inputs are the first n, targets the last n.</summary>
public interface IBatchSource
{
    /// <summary>The next training window, drawn with <paramref name="rng"/>.</summary>
    IReadOnlyList<int> NextTrainingWindow(SeededRandom rng);

    /// <summary>A fixed set of windows for measuring validation loss.</summary>
    IReadOnlyList<IReadOnlyList<int>> ValidationWindows { get; }
}
