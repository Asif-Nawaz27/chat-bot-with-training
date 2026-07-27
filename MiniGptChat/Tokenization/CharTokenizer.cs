namespace MiniGptChat.Tokenization;

/// <summary>
/// A character-level tokenizer: the "vocabulary" is simply the set of distinct
/// characters that appear in the training text (letters, digits, punctuation,
/// newline, etc). Each character maps to an integer id and back.
///
/// Character-level tokenization is the simplest possible tokenizer to build
/// from scratch - no external tokenizer library, no subword merging rules,
/// just a lookup table. Its downside is that sequences are long (one token per
/// character), but for a small learning project that's a fine trade-off.
///
/// This class only holds the vocabulary mapping and does the encode/decode
/// logic. Building a vocabulary from text and reading/writing it to disk are
/// handled separately by <see cref="ITokenizerService"/> - keeping this class
/// focused on one job (character &lt;-&gt; id) and I/O out of it.
/// </summary>
public class CharTokenizer
{
    public IReadOnlyDictionary<char, long> CharToId { get; }
    public IReadOnlyDictionary<long, char> IdToChar { get; }

    public int VocabSize => CharToId.Count;

    public CharTokenizer(IReadOnlyDictionary<char, long> charToId, IReadOnlyDictionary<long, char> idToChar)
    {
        CharToId = charToId;
        IdToChar = idToChar;
    }

    /// <summary>Converts a string into a sequence of token ids.</summary>
    public long[] Encode(string text)
    {
        var ids = new long[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            // Unknown characters (not seen while building the vocabulary) fall back
            // to the id for a space if possible, otherwise id 0. This keeps chat mode
            // robust if the user types a character the model never saw during training.
            if (CharToId.TryGetValue(text[i], out var id))
            {
                ids[i] = id;
            }
            else if (CharToId.TryGetValue(' ', out var spaceId))
            {
                ids[i] = spaceId;
            }
            else
            {
                ids[i] = 0;
            }
        }
        return ids;
    }

    /// <summary>Converts a sequence of token ids back into a string.</summary>
    public string Decode(IEnumerable<long> ids)
    {
        var chars = ids.Select(id => IdToChar.TryGetValue(id, out var c) ? c : '?');
        return new string(chars.ToArray());
    }
}
