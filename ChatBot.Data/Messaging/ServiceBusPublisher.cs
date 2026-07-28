using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace ChatBot.Data.Messaging;

/// <inheritdoc cref="IServiceBusPublisher"/>
public class ServiceBusPublisher : IServiceBusPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusPublisher(IOptions<ServiceBusOptions> options)
    {
        _client = new ServiceBusClient(options.Value.ConnectionString);
    }

    public async Task PublishAsync<T>(string queueName, T message)
    {
        var sender = _senders.GetOrAdd(queueName, _client.CreateSender);
        var body = JsonSerializer.Serialize(message);
        await sender.SendMessageAsync(new ServiceBusMessage(body));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
        await _client.DisposeAsync();
    }
}
