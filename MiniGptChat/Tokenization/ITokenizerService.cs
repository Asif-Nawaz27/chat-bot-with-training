namespace MiniGptChat.Tokenization;

/// <summary>
/// Builds and persists <see cref="CharTokenizer"/> instances. Separated from
/// <see cref="CharTokenizer"/> itself so the tokenizer's encode/decode logic
/// stays free of file I/O and JSON concerns.
/// </summary>
public interface ITokenizerService
{
    /// <summary>Scans <paramref name="text"/> and builds a vocabulary from its distinct characters.</summary>
    CharTokenizer BuildFromText(string text);

    /// <summary>Saves a tokenizer's vocabulary mapping to disk as JSON.</summary>
    void Save(CharTokenizer tokenizer, string path);

    /// <summary>Loads a previously saved vocabulary mapping from disk.</summary>
    CharTokenizer Load(string path);
}
