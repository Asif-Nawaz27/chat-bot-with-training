using System.Text;

namespace MiniGptChat.Chat;

/// <summary>
/// Tracks the running "User: ...\nBot: ...\n" transcript of a chat session and
/// builds the next prompt to feed the model, trimming old turns so the prompt
/// always fits inside the model's fixed-size context window.
/// </summary>
public class ConversationHistory
{
    private const string UserTurnPrefix = "User: ";
    private const string BotTurnPrefix = "Bot: ";

    private readonly StringBuilder _transcript = new();

    public void AddUserTurn(string message) => _transcript.Append(UserTurnPrefix).Append(message).Append('\n');

    public void AddBotTurn(string reply) => _transcript.Append(BotTurnPrefix).Append(reply).Append('\n');

    /// <summary>
    /// Returns the transcript so far (trimmed to at most <paramref name="maxContextChars"/>
    /// characters, dropping the oldest complete turns first) with a trailing "Bot: " so the
    /// model's next generated characters continue naturally as the bot's reply.
    /// </summary>
    public string BuildPromptForBot(int maxContextChars)
    {
        var trimmed = TrimToFit(_transcript.ToString(), maxContextChars);
        return trimmed + BotTurnPrefix;
    }

    /// <summary>
    /// Drops the oldest complete lines from <paramref name="text"/> until what remains is
    /// at or under <paramref name="maxChars"/> characters.
    /// </summary>
    private static string TrimToFit(string text, int maxChars)
    {
        while (text.Length > maxChars)
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline < 0 || firstNewline == text.Length - 1)
            {
                // No more full lines to drop; fall back to a hard cut from the front.
                return text[^maxChars..];
            }
            text = text[(firstNewline + 1)..];
        }
        return text;
    }
}
