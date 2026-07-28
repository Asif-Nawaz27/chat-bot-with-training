namespace ChatBot.Data.Messaging;

/// <summary>
/// Thin JSON-over-Service-Bus publisher shared by both ChatBot.Api (sends
/// <see cref="TrainingJobMessage"/> to <see cref="ServiceBusOptions.TrainingQueueName"/>) and
/// ChatBot.Train (sends <see cref="TrainingStatusMessage"/> to <see cref="ServiceBusOptions.StatusQueueName"/>).
/// </summary>
public interface IServiceBusPublisher
{
    /// <summary>Serializes <paramref name="message"/> to JSON and sends it to <paramref name="queueName"/>.</summary>
    Task PublishAsync<T>(string queueName, T message);
}
