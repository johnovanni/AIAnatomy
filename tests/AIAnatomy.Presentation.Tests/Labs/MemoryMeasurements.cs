using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>
/// Per-window loss-by-position curves of the shipped Fables transformer and the shipped memory network, on the 32 shared
/// validation windows and on all 292 held-back windows, worked out once for the tests that check the numbers the memory
/// lab and lesson 4.8 quote. "Paired" statistics compare each window on both networks.
/// </summary>
internal static class MemoryMeasurements
{
    public sealed record Curves(double[][] Transformer, double[][] Memory);

    private static readonly Lazy<(Curves Shared, Curves HeldBack)> All = new(Measure);

    public static Curves Shared => All.Value.Shared;
    public static Curves HeldBack => All.Value.HeldBack;

    /// <summary>The mean and standard error of the values.</summary>
    public static (double Mean, double Error) MeanAndError(IReadOnlyList<double> values)
    {
        double mean = values.Average();
        double variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
        return (mean, Math.Sqrt(variance / values.Count));
    }

    /// <summary>Each window's mean loss on the transformer minus the same window's on the memory network.</summary>
    public static double[] PairedGaps(Curves c) => c.Transformer.Zip(c.Memory, (t, m) => t.Average() - m.Average()).ToArray();

    /// <summary>Each window's mean loss with 9 to 16 characters read minus its mean loss with 33 to 64 read.</summary>
    public static double[] Gains(double[][] curves) => curves.Select(c => c[8..16].Average() - c[32..64].Average()).ToArray();

    private static (Curves, Curves) Measure()
    {
        LanguageModel fables;
        using (var env = new TestEnvironment())
        {
            var library = env.NewLibrary();
            fables = library.Load(library.List().Single(e => e.IsShipped && e.Name == ActiveModelService.DefaultModelName));
        }
        ElmanRnn rnn;
        using (var stream = File.OpenRead(Path.Combine(TestEnvironment.Content, "models", "family", "memory.aimodel"))) rnn = ElmanRnn.Load(stream).Rnn;
        string text = File.ReadAllText(Path.Combine(TestEnvironment.Content, "texts", "aesop-fables.txt"));
        var shared = MemoryTraining.FablesSource(fables.Tokenizer, text).ValidationWindows;
        var heldBack = MemoryTraining.HeldBackWindows(fables.Tokenizer, text);
        return (PerWindow(fables, rnn, shared), PerWindow(fables, rnn, heldBack));
    }

    private static Curves PerWindow(LanguageModel fables, ElmanRnn rnn, IReadOnlyList<IReadOnlyList<int>> windows)
    {
        var transformer = new double[windows.Count][];
        var memory = new double[windows.Count][];
        // The transformer's forward pass only reads its weights (ContextLoss runs windows side by side too); the memory
        // network's windows run one after another.
        Parallel.For(0, windows.Count, w => transformer[w] = ContextLoss.LossByPosition(fables.Transformer, new[] { windows[w] }));
        for (int w = 0; w < windows.Count; w++) memory[w] = ContextLoss.LossByPosition(rnn, new[] { windows[w] });
        return new Curves(transformer, memory);
    }
}
