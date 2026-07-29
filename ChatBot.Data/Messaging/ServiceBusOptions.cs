namespace ChatBot.Data.Messaging;

/// <summary>
/// Bound from the "AzureServiceBus" section of appsettings (see appsettings.Development.json /
/// local.settings.json, where the real connection string belongs - never commit a live one to
/// appsettings.json). <see cref="TrainingQueueName"/> carries training requests from the API to
/// ChatBot.Train; progress/completion messages come back via HTTP callback instead
/// (see <see cref="TrainingCallbackOptions"/>).
/// </summary>
public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TrainingQueueName { get; set; } = "chat-bot";
}
