using MiniGptChat.Generation;
using MiniGptChat.Model;
using MiniGptChat.Tokenization;

namespace MiniGptChat.Chat;

/// <summary>
/// Everything needed to generate a reply: the loaded model, its tokenizer, the
/// architecture it was built with, and the sampling settings to use. Bundled
/// together so both the console chat loop and the API's session service can
/// load this once and reuse it for every message.
/// </summary>
public class ChatModelContext : IDisposable
{
    public MiniGptModel Model { get; }
    public CharTokenizer Tokenizer { get; }
    public GptConfig ModelConfig { get; }
    public GenerationOptions Options { get; }

    public ChatModelContext(MiniGptModel model, CharTokenizer tokenizer, GptConfig modelConfig, GenerationOptions options)
    {
        Model = model;
        Tokenizer = tokenizer;
        ModelConfig = modelConfig;
        Options = options;
    }

    public void Dispose() => Model.Dispose();
}
