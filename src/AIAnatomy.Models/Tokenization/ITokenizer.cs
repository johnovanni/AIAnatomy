namespace AIAnatomy.Models.Tokenization;

/// <summary>Turns text into integer token ids and back.</summary>
public interface ITokenizer
{
    /// <summary>Number of ids, including the unknown id 0.</summary>
    int VocabularySize { get; }

    IReadOnlyList<int> Encode(string text);

    string Decode(IReadOnlyList<int> ids);

    /// <summary>Raw text of one token; the unknown token gives U+FFFD.</summary>
    string TokenText(int id);

    /// <summary>Id of the unknown token. Always 0.</summary>
    int UnknownId { get; }
}
