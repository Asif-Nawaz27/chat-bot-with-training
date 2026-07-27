namespace MiniGptChat.Chat;

/// <summary>Loads a trained checkpoint into a ready-to-use <see cref="ChatModelContext"/>.</summary>
public interface IChatModelLoader
{
    /// <summary>True if a full checkpoint (weights + vocab + config) exists on disk.</summary>
    bool CheckpointExists(GptConfig config);

    /// <summary>
    /// Loads the saved architecture, vocabulary and weights, and builds the model in
    /// inference (eval) mode along with default generation settings.
    /// </summary>
    ChatModelContext Load(GptConfig config);
}
