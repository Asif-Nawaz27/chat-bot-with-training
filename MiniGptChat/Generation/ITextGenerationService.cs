using MiniGptChat.Model;
using MiniGptChat.Tokenization;

namespace MiniGptChat.Generation;

/// <summary>
/// Autoregressive text generation: repeatedly asks the model "given everything
/// so far, what character comes next?", samples one character from its
/// predicted probability distribution, appends it, and repeats.
/// </summary>
public interface ITextGenerationService
{
    /// <summary>
    /// Generates a continuation of <paramref name="prompt"/> and returns just the
    /// newly generated text (the prompt itself is not included in the result).
    /// Stops early if <see cref="GenerationOptions.EndMarker"/> is produced.
    /// </summary>
    string Generate(MiniGptModel model, CharTokenizer tokenizer, GptConfig config, string prompt, GenerationOptions options);
}
