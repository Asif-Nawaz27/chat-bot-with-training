namespace MiniGptChat.Chat;

/// <inheritdoc cref="IChatService"/>
public class ChatService : IChatService
{
    private readonly IChatModelLoader _modelLoader;
    private readonly IChatReplyService _replyService;

    public ChatService(IChatModelLoader modelLoader, IChatReplyService replyService)
    {
        _modelLoader = modelLoader;
        _replyService = replyService;
    }

    public void Run(GptConfig config)
    {
        if (!_modelLoader.CheckpointExists(config))
        {
            Console.WriteLine("No trained model found yet.");
            Console.WriteLine($"Run 'dotnet run -- train' first to create {config.ModelWeightsPath}, " +
                $"{config.VocabPath} and {config.ConfigPath}.");
            return;
        }

        Console.WriteLine("Loading trained model...");
        using var context = _modelLoader.Load(config);

        Console.WriteLine("Model loaded. Type a message and press Enter to chat.");
        Console.WriteLine("Type 'exit' or 'quit' to leave the chat.");
        Console.WriteLine();

        var history = new ConversationHistory();

        while (true)
        {
            var userInput = ReadUserMessage();
            if (userInput is null)
                break;

            var reply = _replyService.Reply(context, history, userInput);
            Console.WriteLine($"Bot: {reply}");
        }

        Console.WriteLine("Goodbye!");
    }

    /// <summary>
    /// Reads one non-blank line of user input, re-prompting on blank lines.
    /// Returns null when the session should end (exit/quit typed, or the input stream closed).
    /// </summary>
    private static string? ReadUserMessage()
    {
        while (true)
        {
            Console.Write("You: ");
            var line = Console.ReadLine();

            if (line is null)
                return null; // e.g. input stream closed (Ctrl+Z / Ctrl+D)

            var trimmed = line.Trim();
            if (trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (trimmed.Length > 0)
                return trimmed;
        }
    }
}
