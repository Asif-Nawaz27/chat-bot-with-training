using System.Text.Json;

namespace MiniGptChat.Tokenization;

/// <inheritdoc cref="ITokenizerService"/>
public class CharTokenizerService : ITokenizerService
{
    public CharTokenizer BuildFromText(string text)
    {
        // Sorting the distinct characters makes the id assignment deterministic,
        // so re-building a tokenizer from the same text always yields the same mapping.
        var distinctChars = text.Distinct().OrderBy(c => c).ToList();

        var charToId = new Dictionary<char, long>();
        var idToChar = new Dictionary<long, char>();
        for (int i = 0; i < distinctChars.Count; i++)
        {
            charToId[distinctChars[i]] = i;
            idToChar[i] = distinctChars[i];
        }

        return new CharTokenizer(charToId, idToChar);
    }

    public void Save(CharTokenizer tokenizer, string path)
    {
        EnsureDirectoryExists(path);

        // JSON object keys must be strings, so we store the character's integer code
        // point rather than the raw char (avoids escaping issues with quotes, newlines, etc).
        var serializable = tokenizer.CharToId.ToDictionary(kv => ((int)kv.Key).ToString(), kv => kv.Value);
        var json = JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public CharTokenizer Load(string path)
    {
        var json = File.ReadAllText(path);
        var serializable = JsonSerializer.Deserialize<Dictionary<string, long>>(json)
            ?? throw new InvalidDataException($"Could not parse vocabulary file at {path}");

        var charToId = new Dictionary<char, long>();
        var idToChar = new Dictionary<long, char>();
        foreach (var (key, id) in serializable)
        {
            var c = (char)int.Parse(key);
            charToId[c] = id;
            idToChar[id] = c;
        }

        return new CharTokenizer(charToId, idToChar);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
