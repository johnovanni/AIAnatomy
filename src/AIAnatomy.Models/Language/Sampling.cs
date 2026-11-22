using AIAnatomy.Core;

namespace AIAnatomy.Models.Language;

/// <summary>How the next character is chosen. TopK 0 = off; Temperature 0 = always the top choice; TopP 1 = off.</summary>
public sealed record SamplingSettings(double Temperature = 1.0, int TopK = 0, double TopP = 1.0);

public static class Sampling
{
    /// <summary>Softmax of the logits after temperature, then top-k, then top-p, renormalised to sum to 1.</summary>
    public static double[] Probabilities(double[] logits, SamplingSettings s)
    {
        ArgumentNullException.ThrowIfNull(logits);
        ArgumentNullException.ThrowIfNull(s);
        if (logits.Length == 0) throw new ArgumentException("Give at least one logit.", nameof(logits));
        if (s.Temperature < 0 || double.IsNaN(s.Temperature)) throw new ArgumentOutOfRangeException(nameof(s), "Temperature cannot be negative.");
        if (s.TopK < 0) throw new ArgumentOutOfRangeException(nameof(s), "Top-k cannot be negative.");
        if (!(s.TopP > 0 && s.TopP <= 1)) throw new ArgumentOutOfRangeException(nameof(s), "Top-p must be above 0 and at most 1.");

        int n = logits.Length;
        // Indices from most to least likely; ties keep the lower index first.
        var order = Enumerable.Range(0, n).OrderByDescending(i => logits[i]).ThenBy(i => i).ToArray();
        var p = new double[n];
        if (s.Temperature == 0)
        {
            p[order[0]] = 1;
            return p;
        }

        double max = logits[order[0]];
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            p[i] = Math.Exp((logits[i] - max) / s.Temperature);
            sum += p[i];
        }
        for (int i = 0; i < n; i++) p[i] /= sum;

        if (s.TopK > 0 && s.TopK < n)
        {
            for (int r = s.TopK; r < n; r++) p[order[r]] = 0;
            Renormalise(p);
        }

        if (s.TopP < 1)
        {
            double cumulative = 0;
            int keep = n;
            for (int r = 0; r < n; r++)
            {
                cumulative += p[order[r]];
                if (cumulative >= s.TopP - 1e-12) { keep = r + 1; break; }
            }
            for (int r = keep; r < n; r++) p[order[r]] = 0;
            Renormalise(p);
        }
        return p;
    }

    /// <summary>Picks an index with the given probabilities, using one random number.</summary>
    public static int Sample(double[] probabilities, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(probabilities);
        ArgumentNullException.ThrowIfNull(rng);
        double r = rng.NextDouble();
        double cumulative = 0;
        int lastPossible = -1;
        for (int i = 0; i < probabilities.Length; i++)
        {
            if (probabilities[i] <= 0) continue;
            lastPossible = i;
            cumulative += probabilities[i];
            if (r < cumulative) return i;
        }
        if (lastPossible < 0) throw new ArgumentException("No choice has a positive probability.", nameof(probabilities));
        return lastPossible; // rounding left a sliver above the cumulative total
    }

    /// <summary>
    /// Writes <paramref name="count"/> new characters after <paramref name="prompt"/> and returns only the new text.
    /// An empty prompt starts from a newline (the unknown token if the vocabulary has no newline).
    /// </summary>
    public static string Generate(LanguageModel m, string prompt, int count, SamplingSettings s, SeededRandom rng)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(prompt);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var ids = m.Tokenizer.Encode(prompt.Length == 0 ? "\n" : prompt).ToList();
        int promptLength = ids.Count;
        for (int i = 0; i < count; i++)
        {
            var logits = m.PredictTokens(ids).Forward.Logits;
            var last = logits.Row(logits.Shape[0] - 1);
            ids.Add(Sample(Probabilities(last, s), rng));
        }
        return m.Tokenizer.Decode(ids.Skip(promptLength).ToArray());
    }

    private static void Renormalise(double[] p)
    {
        double sum = p.Sum();
        for (int i = 0; i < p.Length; i++) p[i] /= sum;
    }
}
