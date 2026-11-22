using AIAnatomy.Core;

namespace AIAnatomy.Models.Training;

/// <summary>Random windows of ContextLength + 1 tokens from a text; the last part of the text is held back for validation.</summary>
public sealed class CorpusBatchSource : IBatchSource
{
    private const int MaxValidationWindows = 32;

    private readonly int[] _tokens;
    private readonly int _window;
    private readonly int _trainWindowStarts;   // training windows start in [0, _trainWindowStarts)

    /// <param name="rng">Used once here to pick validation windows when there are more than 32 possible.</param>
    public CorpusBatchSource(IReadOnlyList<int> corpusTokens, int contextLength, int vocabularySize, double validationFraction, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(corpusTokens);
        ArgumentNullException.ThrowIfNull(rng);
        if (CorpusValidator.ProblemForLength(corpusTokens.Count, contextLength) is { } problem)
            throw new ArgumentException(problem, nameof(corpusTokens));
        foreach (int id in corpusTokens)
            if (id < 0 || id >= vocabularySize)
                throw new ArgumentException($"The text has a token ({id}) outside the model's vocabulary of {vocabularySize}.", nameof(corpusTokens));

        _tokens = corpusTokens.ToArray();
        _window = contextLength + 1;
        int n = _tokens.Length;
        int trainEnd = Math.Max(_window, (int)Math.Floor(n * (1 - validationFraction)));
        _trainWindowStarts = trainEnd - _window + 1;
        // The last 10%; if that is shorter than one window it grows backwards just enough to hold one.
        int validationStart = Math.Min(trainEnd, n - _window);
        var starts = ChooseValidationStarts(rng, validationStart, n - _window - validationStart + 1);
        ValidationWindows = starts.Select(s => (IReadOnlyList<int>)_tokens[s..(s + _window)]).ToArray();
    }

    public IReadOnlyList<IReadOnlyList<int>> ValidationWindows { get; }

    public IReadOnlyList<int> NextTrainingWindow(SeededRandom rng)
    {
        int start = rng.NextInt(_trainWindowStarts);
        return _tokens[start..(start + _window)];
    }

    private static int[] ChooseValidationStarts(SeededRandom rng, int first, int possible)
    {
        if (possible <= MaxValidationWindows) return Enumerable.Range(first, possible).ToArray();
        var chosen = new SortedSet<int>();
        while (chosen.Count < MaxValidationWindows) chosen.Add(first + rng.NextInt(possible));
        return chosen.ToArray();
    }
}
