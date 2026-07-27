namespace MiniGptChat.Chat;

/// <summary>
/// Multi-session chat for stateless callers like an HTTP API: each session keeps
/// its own <see cref="ConversationHistory"/>, while the (comparatively expensive
/// to load) model itself is loaded once and shared across every session.
/// </summary>
public interface IChatSessionService
{
    /// <summary>True if a trained checkpoint is available to chat with.</summary>
    bool ModelReady(GptConfig config);

    /// <summary>Starts a new, empty conversation and returns its session id.</summary>
    string StartSession();

    /// <summary>True if <paramref name="sessionId"/> refers to a session started by <see cref="StartSession"/>.</summary>
    bool SessionExists(string sessionId);

    /// <summary>
    /// Sends a message in an existing session and returns the bot's reply.
    /// Throws <see cref="KeyNotFoundException"/> if the session id is unknown.
    /// </summary>
    string SendMessage(GptConfig config, string sessionId, string message);

    /// <summary>
    /// Drops the cached, already-loaded model so the next message reloads it from
    /// disk. Must be called after retraining - otherwise every existing and new
    /// chat session would keep talking to the weights that were in memory before
    /// retraining, since the model is normally loaded once and reused forever.
    /// </summary>
    void InvalidateModel();
}
