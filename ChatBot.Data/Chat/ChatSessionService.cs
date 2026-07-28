using System.Collections.Concurrent;

namespace ChatBot.Data.Chat;

/// <inheritdoc cref="IChatSessionService"/>
public class ChatSessionService : IChatSessionService, IDisposable
{
    private readonly IChatModelLoader _modelLoader;
    private readonly IChatReplyService _replyService;
    private readonly ConcurrentDictionary<string, ConversationHistory> _sessions = new();

    // Guards both lazy model loading and generation itself: TorchSharp inference is
    // read-only with respect to the model's weights, but keeping this simple and
    // single-threaded avoids any doubt for a learning project running on CPU anyway.
    private readonly object _sync = new();
    private ChatModelContext? _context;

    public ChatSessionService(IChatModelLoader modelLoader, IChatReplyService replyService)
    {
        _modelLoader = modelLoader;
        _replyService = replyService;
    }

    public bool ModelReady(GptConfig config) => _modelLoader.CheckpointExists(config);

    public string StartSession()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = new ConversationHistory();
        return sessionId;
    }

    public bool SessionExists(string sessionId) => _sessions.ContainsKey(sessionId);

    public string SendMessage(GptConfig config, string sessionId, string message)
    {
        if (!_sessions.TryGetValue(sessionId, out var history))
            throw new KeyNotFoundException($"Unknown chat session '{sessionId}'.");

        lock (_sync)
        {
            var context = GetOrLoadContext(config);
            return _replyService.Reply(context, history, message);
        }
    }

    private ChatModelContext GetOrLoadContext(GptConfig config)
    {
        return _context ??= _modelLoader.Load(config);
    }

    public void InvalidateModel()
    {
        lock (_sync)
        {
            _context?.Dispose();
            _context = null;
        }
    }

    public void Dispose() => _context?.Dispose();
}
