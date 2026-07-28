using ChatBot.Data.Generation;

namespace ChatBot.Data.Chat;

/// <inheritdoc cref="IChatReplyService"/>
public class ChatReplyService : IChatReplyService
{
    private readonly ITextGenerationService _generationService;

    public ChatReplyService(ITextGenerationService generationService)
    {
        _generationService = generationService;
    }

    public string Reply(ChatModelContext context, ConversationHistory history, string userMessage)
    {
        history.AddUserTurn(userMessage);

        var maxHistoryChars = ComputeMaxHistoryChars(context.ModelConfig, context.Options);
        var prompt = history.BuildPromptForBot(maxHistoryChars);

        var reply = _generationService.Generate(context.Model, context.Tokenizer, context.ModelConfig, prompt, context.Options).Trim();
        if (reply.Length == 0)
            reply = "...";

        history.AddBotTurn(reply);
        return reply;
    }

    /// <summary>
    /// Reserves some room in the context window for the model's own reply, so we
    /// don't hand it a prompt that already fills the whole block size.
    /// </summary>
    private static int ComputeMaxHistoryChars(GptConfig modelConfig, GenerationOptions options) =>
        Math.Max(modelConfig.BlockSize - options.MaxNewTokens, modelConfig.BlockSize / 2);
}
