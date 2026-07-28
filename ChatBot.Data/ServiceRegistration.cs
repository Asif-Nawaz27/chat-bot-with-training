using Microsoft.Extensions.DependencyInjection;
using ChatBot.Data.Chat;
using ChatBot.Data.Corpus;
using ChatBot.Data.Generation;
using ChatBot.Data.Model;
using ChatBot.Data.Tokenization;
using ChatBot.Data.Training;

namespace ChatBot.Data;

/// <summary>
/// Registers every ChatBot.Data service - one interface/implementation pair per
/// concern - into an <see cref="IServiceCollection"/>. Shared by both
/// ChatBot.Cli (which builds its own <c>ServiceProvider</c>) and
/// ChatBot.Api (which calls this from its own DI setup), so both entry
/// points wire up identically.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddChatBotDataServices(this IServiceCollection services)
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
