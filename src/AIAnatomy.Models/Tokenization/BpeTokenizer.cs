using System.Text;

namespace AIAnatomy.Models.Tokenization;

/// <summary>
/// Byte-pair-encoding tokenizer working on characters (decision D4).
/// Ids: 0 = unknown, then the base characters in ordinal order, then one id per merge in the order learned.
/// Text is split into words with the leading space attached (" the"); a newline is its own piece.
/// Merges never cross a word boundary.
/// </summary>
public sealed class BpeTokenizer : ITokenizer
{
    private const string Replacement = "�";

    private readonly string[] _tokenText;                       // index = id
    private readonly Dictionary<string, int> _baseIds = new();  // base character -> id
    private readonly Dictionary<(int Left, int Right), (int Rank, int Id)> _mergeLookup = new();
    private readonly List<(string Left, string Right)> _merges = new();

    private BpeTokenizer(string[] tokenText, int baseCount, List<(int Left, int Right)> mergeIds)
    {
        _tokenText = tokenText;
        for (int i = 1; i <= baseCount; i++) _baseIds[tokenText[i]] = i;
        for (int rank = 0; rank < mergeIds.Count; rank++)
        {
            _mergeLookup[mergeIds[rank]] = (rank, baseCount + 1 + rank);
            _merges.Add((tokenText[mergeIds[rank].Left], tokenText[mergeIds[rank].Right]));
        }
    }

    /// <summary>Learns up to <paramref name="merges"/> merges (fewer if no adjacent pairs are left).</summary>
    public static BpeTokenizer Train(string text, int merges)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(merges);
        string normalized = TextNormalizer.Normalize(text);

        var baseChars = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Rune r in normalized.EnumerateRunes()) baseChars.Add(r.ToString());
        var tokenText = new List<string> { Replacement };
        tokenText.AddRange(baseChars);
        var baseIds = new Dictionary<string, int>();
        for (int i = 1; i < tokenText.Count; i++) baseIds[tokenText[i]] = i;
        int baseCount = baseChars.Count;

        // Unique words in order of first occurrence, with their counts.
        var wordIndex = new Dictionary<string, int>();
        var words = new List<List<int>>();
        var counts = new List<int>();
        foreach (string word in SplitWords(normalized))
        {
            if (wordIndex.TryGetValue(word, out int w)) { counts[w]++; continue; }
            wordIndex[word] = words.Count;
            counts.Add(1);
            var symbols = new List<int>();
            foreach (Rune r in word.EnumerateRunes()) symbols.Add(baseIds[r.ToString()]);
            words.Add(symbols);
        }

        var mergeIds = new List<(int Left, int Right)>();
        for (int step = 0; step < merges; step++)
        {
            // Count adjacent pairs, remembering the order in which pairs first appear in the text.
            var pairCounts = new Dictionary<(int, int), int>();
            var firstSeen = new List<(int, int)>();
            for (int w = 0; w < words.Count; w++)
            {
                var s = words[w];
                for (int i = 0; i + 1 < s.Count; i++)
                {
                    var pair = (s[i], s[i + 1]);
                    if (pairCounts.TryGetValue(pair, out int c)) pairCounts[pair] = c + counts[w];
                    else { pairCounts[pair] = counts[w]; firstSeen.Add(pair); }
                }
            }

            (int Left, int Right) best = default;
            int bestCount = 0;
            foreach (var pair in firstSeen)
            {
                // Strictly greater: on a tie the pair seen first in the text stays.
                if (pairCounts[pair] > bestCount) { best = pair; bestCount = pairCounts[pair]; }
            }
            if (bestCount == 0) break;

            int newId = tokenText.Count;
            tokenText.Add(tokenText[best.Left] + tokenText[best.Right]);
            mergeIds.Add(best);
            foreach (var s in words) ApplyMerge(s, best, newId);
        }

        return new BpeTokenizer(tokenText.ToArray(), baseCount, mergeIds);
    }

    /// <summary>The merges in the order learned.</summary>
    public IReadOnlyList<(string Left, string Right)> Merges => _merges;

    public int VocabularySize => _tokenText.Length;

    public int UnknownId => 0;

    public IReadOnlyList<int> Encode(string text)
    {
        var result = new List<int>();
        foreach (string word in SplitWords(TextNormalizer.Normalize(text)))
        {
            var symbols = new List<int>();
            foreach (Rune r in word.EnumerateRunes())
                symbols.Add(_baseIds.TryGetValue(r.ToString(), out int id) ? id : UnknownId);

            // Apply merges in the order they were learned: always the lowest-ranked pair present.
            while (true)
            {
                int bestRank = int.MaxValue, bestId = 0, bestPos = -1;
                for (int i = 0; i + 1 < symbols.Count; i++)
                {
                    if (_mergeLookup.TryGetValue((symbols[i], symbols[i + 1]), out var m) && m.Rank < bestRank)
                    {
                        bestRank = m.Rank; bestId = m.Id; bestPos = i;
                    }
                }
                if (bestPos < 0) break;
                ApplyMerge(symbols, (symbols[bestPos], symbols[bestPos + 1]), bestId);
            }
            result.AddRange(symbols);
        }
        return result;
    }

    public string Decode(IReadOnlyList<int> ids)
    {
        var sb = new StringBuilder();
        foreach (int id in ids) sb.Append(TokenText(id));
        return sb.ToString();
    }

    /// <summary>Raw text of the piece (spaces and newlines are not made visible here).</summary>
    public string TokenText(int id)
    {
        if (id < 0 || id >= _tokenText.Length) throw new ArgumentOutOfRangeException(nameof(id));
        return _tokenText[id];
    }

    // Replaces each non-overlapping left-to-right occurrence of the pair by the new symbol.
    private static void ApplyMerge(List<int> symbols, (int Left, int Right) pair, int newId)
    {
        int write = 0;
        for (int read = 0; read < symbols.Count; read++)
        {
            if (read + 1 < symbols.Count && symbols[read] == pair.Left && symbols[read + 1] == pair.Right)
            {
                symbols[write++] = newId;
                read++;
            }
            else symbols[write++] = symbols[read];
        }
        symbols.RemoveRange(write, symbols.Count - write);
    }

    // A word starts at every space and runs to the next space or newline; each newline is its own piece.
    private static IEnumerable<string> SplitWords(string text)
    {
        var current = new StringBuilder();
        foreach (char ch in text)
        {
            if (ch == '\n')
            {
                if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                yield return "\n";
            }
            else if (ch == ' ')
            {
                if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                current.Append(ch);
            }
            else current.Append(ch);
        }
        if (current.Length > 0) yield return current.ToString();
    }
}
