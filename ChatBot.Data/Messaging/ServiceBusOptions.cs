namespace ChatBot.Data.Messaging;

/// <summary>
/// Bound from the "AzureServiceBus" section of appsettings (see appsettings.Development.json /
/// local.settings.json, where the real connection string belongs - never commit a live one to
/// appsettings.json). <see cref="TrainingQueueName"/> carries training requests from the API to
/// ChatBot.Train; <see cref="StatusQueueName"/> carries progress/completion messages back.
/// </summary>
public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TrainingQueueName { get; set; } = "chat-bot";
    public string StatusQueueName { get; set; } = "chat-bot-status";
}
