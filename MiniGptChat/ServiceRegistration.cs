using Microsoft.Extensions.DependencyInjection;
using MiniGptChat.Chat;
using MiniGptChat.Corpus;
using MiniGptChat.Generation;
using MiniGptChat.Model;
using MiniGptChat.Tokenization;
using MiniGptChat.Training;

namespace MiniGptChat;

/// <summary>
/// Registers every MiniGptChat service - one interface/implementation pair per
/// concern - into an <see cref="IServiceCollection"/>. Shared by both
/// MiniGptChat.Cli (which builds its own <c>ServiceProvider</c>) and
/// MiniGptChat.Api (which calls this from its own DI setup), so both entry
/// points wire up identically.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddMiniGptChatServices(this IServiceCollection services)
    {
        services.AddSingleton<ITrainingDataProvider, FileTrainingDataProvider>();
        services.AddSingleton<ITokenizerService, CharTokenizerService>();
        services.AddSingleton<IModelService, ModelService>();
        services.AddSingleton<IBatchSampler, RandomBatchSampler>();
        services.AddSingleton<ITrainingService, TrainingService>();
        services.AddSingleton<ITextGenerationService, TextGenerationService>();

        services.AddSingleton<IChatModelLoader, ChatModelLoader>();
        services.AddSingleton<IChatReplyService, ChatReplyService>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<IChatSessionService, ChatSessionService>();

        return services;
    }
}
