using System.Text;

namespace AIAnatomy.Models.Tokenization;

/// <summary>
/// One token per character. Id 0 is unknown; ids 1.. are the vocabulary characters.
/// Text is walked by Unicode scalar, so an emoji (surrogate pair) is one unknown token.
/// </summary>
public sealed class CharTokenizer : ITokenizer
{
    private const string Replacement = "�";
    private readonly char[] _vocabulary;
    private readonly Dictionary<char, int> _ids = new();

    private CharTokenizer(char[] vocabulary)
    {
        _vocabulary = vocabulary;
        for (int i = 0; i < vocabulary.Length; i++)
        {
            if (!_ids.TryAdd(vocabulary[i], i + 1))
                throw new ArgumentException($"Duplicate character U+{(int)vocabulary[i]:X4} in vocabulary.", nameof(vocabulary));
        }
    }

    /// <summary>Distinct characters of the normalised text in ordinal order. Characters outside the BMP are left out (unknown).</summary>
    public static CharTokenizer FromText(string text)
    {
        var chars = new SortedSet<char>();
        foreach (Rune rune in TextNormalizer.Normalize(text).EnumerateRunes())
        {
            if (rune.IsBmp) chars.Add((char)rune.Value);
        }
        return new CharTokenizer(chars.ToArray());
    }

    /// <summary>Rebuilds a tokenizer from a saved vocabulary (id 1 is the first character).</summary>
    public static CharTokenizer FromVocabulary(IReadOnlyList<char> chars)
    {
        ArgumentNullException.ThrowIfNull(chars);
        return new CharTokenizer(chars.ToArray());
    }

    public IReadOnlyList<char> Vocabulary => _vocabulary;

    public int VocabularySize => _vocabulary.Length + 1;

    public int UnknownId => 0;

    public IReadOnlyList<int> Encode(string text)
    {
        var ids = new List<int>(text.Length);
        foreach (Rune rune in TextNormalizer.Normalize(text).EnumerateRunes())
        {
            ids.Add(rune.IsBmp && _ids.TryGetValue((char)rune.Value, out int id) ? id : UnknownId);
        }
        return ids;
    }

    public string Decode(IReadOnlyList<int> ids)
    {
        var sb = new StringBuilder(ids.Count);
        foreach (int id in ids) sb.Append(TokenText(id));
        return sb.ToString();
    }

    public string TokenText(int id)
    {
        if (id == UnknownId) return Replacement;
        if (id < 0 || id > _vocabulary.Length) throw new ArgumentOutOfRangeException(nameof(id));
        return _vocabulary[id - 1].ToString();
    }
}
