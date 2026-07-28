namespace ChatBot.Data.Chat;

/// <summary>
/// Produces one bot reply for a conversation: appends the user's message to the
/// history, builds a prompt that fits the model's context window, generates a
/// reply, and records it back into the history.
/// </summary>
public interface IChatReplyService
{
    string Reply(ChatModelContext context, ConversationHistory history, string userMessage);
}
